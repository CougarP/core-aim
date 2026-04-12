using Core_Aim.Data;
using Core_Aim.Services.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Core_Aim.Services.AI
{
    public class InferenceService : IDisposable
    {
        private readonly AppSettingsService _appSettings;

        private InferenceSession? _session;
        private string _inputName = "";
        private string _outputName = "";

        private readonly object _accessLock = new object();

        public string ActiveProvider { get; private set; } = "Unknown";

        /// <summary>Caminho do modelo actualmente carregado (null se nenhum).</summary>
        public string? LoadedModelPath { get; private set; }

        public bool IsModelLoaded
        {
            get { lock (_accessLock) { return _session != null; } }
        }

        public int ModelInputWidth { get; private set; } = 640;
        public int ModelInputHeight { get; private set; } = 640;

        private float ConfidenceThreshold => GetConfidenceThresholdFromSettings();
        private const float DefaultIouThreshold = 0.45f;

        private readonly RunOptions _runOptions = new RunOptions();

        // ==========================================
        // BUFFERS DE MEMÓRIA
        // ==========================================
        private float[]? _inputArr;
        private DenseTensor<float>? _tensor;
        private List<NamedOnnxValue>? _inputs;

        private Mat? _cropBuffer;

        // _outputArr removido: span directo ao tensor elimina a cópia — zero LOH por inferência

        private readonly List<YoloDetectionResult> _cachedResults  = new List<YoloDetectionResult>(100);
        private readonly List<YoloDetectionResult> _nmsWorkingCopy = new List<YoloDetectionResult>(100);
        private readonly List<YoloDetectionResult> _nmsKept        = new List<YoloDetectionResult>(100);
        private readonly List<YoloDetectionResult> _filteredReuse  = new List<YoloDetectionResult>(100);

        public InferenceService(AppSettingsService appSettings)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        }

        public bool LoadModel(out string error)
        {
            lock (_accessLock)
            {
                error = string.Empty;
                try
                {
                    var baseDir   = AppContext.BaseDirectory;
                    var modelsDir = Path.Combine(baseDir, "Models");
                    var modelFile = _appSettings.SelectedModel ?? "";
                    var modelPath = Path.Combine(modelsDir, modelFile);

                    // Se o mesmo modelo já está carregado, reutiliza a sessão GPU —
                    // evita destruir o contexto CUDA/DML a cada stop+start.
                    // Ainda chama EnsureReusableBuffers+Warmup para garantir estado limpo.
                    if (_session != null && LoadedModelPath == modelPath)
                    {
                        Debug.WriteLine($"[InferenceService] Sessão reutilizada: {modelFile}");
                        EnsureReusableBuffers(ModelInputWidth, ModelInputHeight);
                        Warmup();
                        return true;
                    }

                    InternalDispose();

                    if (!File.Exists(modelPath))
                    {
                        error = $"Modelo não encontrado: {modelPath}";
                        return false;
                    }

                    int gpuId = _appSettings.GpuDeviceId; // 0 = GPU 0, 1 = GPU 1, etc.

                    var so = new SessionOptions();
                    so.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                    so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    // IMPORTANTE: NÃO adicionar CPU aqui — ORT já usa CPU como fallback automático.
                    // Adicionamos apenas o provider GPU com maior prioridade.

                    bool gpuOk = false;

                    // Tenta CUDA primeiro (requer OnnxRuntime.Gpu — não instalado = exception)
                    try
                    {
                        so.AppendExecutionProvider_CUDA(gpuId);
                        ActiveProvider = $"CUDA GPU{gpuId}";
                        gpuOk = true;
                        Console.WriteLine($"[InferenceService] CUDA GPU{gpuId} activado.");
                    }
                    catch (Exception exCuda)
                    {
                        Console.WriteLine($"[InferenceService] CUDA indisponível: {exCuda.Message}");
                    }

                    // Se CUDA falhou, tenta DirectML (pacote OnnxRuntime.DirectML instalado)
                    if (!gpuOk)
                    {
                        try
                        {
                            so.AppendExecutionProvider_DML(gpuId);
                            ActiveProvider = $"DirectML GPU{gpuId}";
                            gpuOk = true;
                            Console.WriteLine($"[InferenceService] DirectML GPU{gpuId} activado.");
                        }
                        catch (Exception exDml)
                        {
                            Console.WriteLine($"[InferenceService] DirectML falhou: {exDml.Message}");
                        }
                    }

                    if (!gpuOk)
                    {
                        ActiveProvider = "CPU";
                        Console.WriteLine("[InferenceService] AVISO: nenhum provider GPU disponível, usando CPU!");
                    }

                    if (gpuOk)
                    {
                        // Com GPU: CPU só processa pre/post-processing.
                        // 1 thread evita overhead de thread-pool e competição com DML.
                        so.IntraOpNumThreads = 1;
                        so.InterOpNumThreads = 1;
                    }

                    Debug.WriteLine($"[InferenceService] Provider Ativo: {ActiveProvider}");

                    _session = new InferenceSession(modelPath, so);

                    var inputKeys = _session.InputMetadata.Keys.ToList();
                    _inputName = inputKeys.FirstOrDefault(k => k == "images") ?? inputKeys.First();
                    DetectModelSize();
                    _outputName = _session.OutputMetadata.Keys.FirstOrDefault() ?? throw new Exception("Sem outputs.");

                    EnsureReusableBuffers(ModelInputWidth, ModelInputHeight);
                    Warmup();

                    LoadedModelPath = modelPath;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    InternalDispose();
                    return false;
                }
            }
        }

        // DML/CUDA fazem JIT dos kernels nos primeiros frames — 8 warmups eliminam
        // os picos de latência iniciais e deixam o pipeline no estado "quente".
        private void Warmup(int count = 8)
        {
            if (_session == null || _inputs == null) return;
            for (int i = 0; i < count; i++)
                try { using var outs = _session.Run(_inputs, new[] { _outputName }, _runOptions); } catch { }
            Console.WriteLine($"[InferenceService] Warmup ({count}x) concluído — {ActiveProvider}");
        }

        private void DetectModelSize()
        {
            var dims = _session!.InputMetadata[_inputName].Dimensions.ToArray();
            if (dims.Length == 4 && dims[2] > 0 && dims[3] > 0) { ModelInputHeight = dims[2]; ModelInputWidth = dims[3]; return; }
            ModelInputHeight = 640; ModelInputWidth = 640;
        }

        public List<YoloDetectionResult> Detect(Mat bgrFrame, out Mat vis)
        {
            vis = new Mat(); // Placeholder

            lock (_accessLock)
            {
                _cachedResults.Clear();

                if (_session == null || bgrFrame.Empty()) return new List<YoloDetectionResult>();

                try
                {
                    // 1. RECORTE CENTRAL (CROP) - Sem redimensionamento
                    var (cropOffsetX, cropOffsetY) = PrepareCenterCrop(bgrFrame, ModelInputWidth, ModelInputHeight);

                    // 2. Inferência
                    using var outs = _session.Run(_inputs!, new[] { _outputName }, _runOptions);
                    var first = outs.FirstOrDefault();
                    if (first == null) return new List<YoloDetectionResult>();

                    // Acesso directo ao span do tensor — zero cópias, zero alocações LOH.
                    var outShape = _session.OutputMetadata[_outputName].Dimensions.ToArray();
                    float confTh = ConfidenceThreshold;

                    if (first.Value is DenseTensor<float> dt)
                    {
                        // Caminho quente: span directo, sem CopyTo
                        ParseAndFilter(dt.Buffer.Span, outShape, confTh, cropOffsetX, cropOffsetY, bgrFrame.Width, bgrFrame.Height);
                    }
                    else
                    {
                        // Fallback genérico (outros tipos de tensor)
                        var span2 = first.AsEnumerable<float>().ToArray().AsSpan();
                        ParseAndFilter(span2, outShape, confTh, cropOffsetX, cropOffsetY, bgrFrame.Width, bgrFrame.Height);
                    }

                    var results = Nms(_cachedResults, DefaultIouThreshold);

                    return results;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Inference] Erro: {ex.Message}");
                    return new List<YoloDetectionResult>();
                }
            }
        }

        // ============================================================================
        // LÓGICA CENTER CROP (PIXEL PERFECT)
        // ============================================================================
        private unsafe (int cropX, int cropY) PrepareCenterCrop(Mat bgr, int netW, int netH)
        {
            int w = bgr.Width;
            int h = bgr.Height;

            // Calcula o canto superior esquerdo do corte
            int cropX = (w - netW) / 2;
            int cropY = (h - netH) / 2;

            if (cropX < 0) cropX = 0;
            if (cropY < 0) cropY = 0;

            // Define a região de corte
            var rect = new OpenCvSharp.Rect(cropX, cropY, netW, netH);

            // Garante buffer de recorte
            if (_cropBuffer == null || _cropBuffer.Width != netW || _cropBuffer.Height != netH)
            {
                _cropBuffer?.Dispose();
                _cropBuffer = new Mat(netH, netW, MatType.CV_8UC3);
            }

            // Copia a região da tela para o buffer
            using (var roiMat = new Mat(bgr, rect))
            {
                roiMat.CopyTo(_cropBuffer);
            }

            // Joga para o Tensor
            ProcessBitmapToTensorUnsafe(_cropBuffer, _inputArr!, netW, netH);

            return (cropX, cropY);
        }

        private unsafe void ProcessBitmapToTensorUnsafe(Mat src, float[] inputArr, int netW, int netH)
        {
            int width  = src.Width;
            int height = src.Height;
            nint ptrSrc = (nint)src.DataPointer;
            int  stride = (int)src.Step();

            int   planeSize = netW * netH;
            float norm      = 1.0f / 255.0f;

            // Paralelo por linha — em 640×640 ganha ~0.3-0.6ms no CPU pre-processing.
            // GCHandle fixa o array para que o GC não o mova enquanto as tasks correm.
            fixed (float* ptrBase = inputArr)
            {
                float* pR = ptrBase;
                float* pG = ptrBase + planeSize;
                float* pB = ptrBase + 2 * planeSize;
                nint   ps = ptrSrc;   // captura em variável local (sem ptr directo no lambda unsafe)
                int    st = stride;

                Parallel.For(0, height, y =>
                {
                    byte* rowSrc       = (byte*)(ps + y * st);
                    int   destOffset   = y * netW;
                    float* pRowR = pR + destOffset;
                    float* pRowG = pG + destOffset;
                    float* pRowB = pB + destOffset;

                    for (int x = 0; x < width; x++)
                    {
                        pRowR[x] = rowSrc[2] * norm;
                        pRowG[x] = rowSrc[1] * norm;
                        pRowB[x] = rowSrc[0] * norm;
                        rowSrc  += 3;
                    }
                });
            }
        }

        // ==========================================================
        // PARSERS (Usando Offset X/Y)
        // ==========================================================
        private void ParseAndFilter(ReadOnlySpan<float> data, int[] shape, float confTh, int offX, int offY, int origW, int origH)
        {
            if (shape.Length != 3) return;
            int dim1 = shape[1];
            int dim2 = shape[2];
            bool isNC = dim1 > dim2;
            if (isNC) ParseNC(data, dim1, dim2, confTh, offX, offY, origW, origH);
            else ParseCN(data, dim1, dim2, confTh, offX, offY, origW, origH);
        }

        private void ParseCN(ReadOnlySpan<float> data, int numProps, int numAnchors, float confTh, int offX, int offY, int origW, int origH)
        {
            int numClasses = numProps - 4;
            for (int i = 0; i < numAnchors; i++)
            {
                float maxScore = 0;
                int bestClass = 0;
                for (int cls = 0; cls < numClasses; cls++)
                {
                    float score = data[(4 + cls) * numAnchors + i];
                    if (score > maxScore) { maxScore = score; bestClass = cls; }
                }
                if (maxScore < confTh) continue;
                AddDetection(data[i], data[numAnchors + i], data[2 * numAnchors + i], data[3 * numAnchors + i], maxScore, bestClass, offX, offY, origW, origH);
            }
        }

        private void ParseNC(ReadOnlySpan<float> data, int numAnchors, int numProps, float confTh, int offX, int offY, int origW, int origH)
        {
            int numClasses = numProps - 4;
            for (int i = 0; i < numAnchors; i++)
            {
                int offset = i * numProps;
                float maxScore = 0;
                int bestClass = 0;
                for (int cls = 0; cls < numClasses; cls++)
                {
                    float score = data[offset + 4 + cls];
                    if (score > maxScore) { maxScore = score; bestClass = cls; }
                }
                if (maxScore < confTh) continue;
                AddDetection(data[offset], data[offset + 1], data[offset + 2], data[offset + 3], maxScore, bestClass, offX, offY, origW, origH);
            }
        }

        private void AddDetection(float cx, float cy, float w, float h, float score, int cls, int offX, int offY, int origW, int origH)
        {
            // Coordenadas Locais (dentro do 640x640)
            float xLocal = cx - w * 0.5f;
            float yLocal = cy - h * 0.5f;

            // Coordenadas Globais (SOMAMOS o offset do corte)
            float xGlobal = xLocal + offX;
            float yGlobal = yLocal + offY;

            _cachedResults.Add(new YoloDetectionResult
            {
                Label = cls.ToString(),
                ClassId = cls,
                Confidence = score,
                BoundingBox = new RectangleF(xGlobal, yGlobal, w, h)
            });
        }

        // Nms: usa buffers pré-alocados — zero alocações por chamada
        private List<YoloDetectionResult> Nms(List<YoloDetectionResult> dets, float iouTh)
        {
            _nmsKept.Clear();
            if (dets.Count == 0) return _nmsKept;

            // Cópia de trabalho (não modifica _cachedResults)
            _nmsWorkingCopy.Clear();
            _nmsWorkingCopy.AddRange(dets);
            _nmsWorkingCopy.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            while (_nmsWorkingCopy.Count > 0)
            {
                var current = _nmsWorkingCopy[0];
                _nmsKept.Add(current);
                _nmsWorkingCopy.RemoveAt(0);
                for (int i = _nmsWorkingCopy.Count - 1; i >= 0; i--)
                    if (CalculateIoU(current, _nmsWorkingCopy[i]) > iouTh)
                        _nmsWorkingCopy.RemoveAt(i);
            }
            return _nmsKept;
        }

        private float CalculateIoU(YoloDetectionResult a, YoloDetectionResult b)
        {
            var r1 = a.BoundingBox;
            var r2 = b.BoundingBox;
            float interX0 = Math.Max(r1.X, r2.X);
            float interY0 = Math.Max(r1.Y, r2.Y);
            float interX1 = Math.Min(r1.Right, r2.Right);
            float interY1 = Math.Min(r1.Bottom, r2.Bottom);
            float w = Math.Max(0, interX1 - interX0);
            float h = Math.Max(0, interY1 - interY0);
            float inter = w * h;
            return inter / (r1.Width * r1.Height + r2.Width * r2.Height - inter + 1e-6f);
        }

        private float GetConfidenceThresholdFromSettings()
        {
            try { return (float)_appSettings.ConfidenceThreshold; } catch { return 0.25f; }
        }

        private void EnsureReusableBuffers(int netW, int netH)
        {
            int totalSize = 3 * netW * netH;
            if (_inputArr == null || _inputArr.Length != totalSize)
            {
                _inputArr = new float[totalSize];
                _tensor = new DenseTensor<float>(_inputArr, new[] { 1, 3, netH, netW });
                _inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, _tensor) };

                _cropBuffer?.Dispose();
                _cropBuffer = null;
            }
        }

        private void InternalDispose()
        {
            _session?.Dispose();   _session    = null;
            _cropBuffer?.Dispose(); _cropBuffer = null;
            _inputArr  = null;
            _tensor    = null;
            _inputs    = null;
            _cachedResults.Clear();
            _nmsWorkingCopy.Clear();
            _nmsKept.Clear();
            _filteredReuse.Clear();
            LoadedModelPath = null;
        }

        public void Dispose()
        {
            lock (_accessLock) { InternalDispose(); }
        }
    }
}