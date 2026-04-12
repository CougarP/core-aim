using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Core_Aim.Services.Camera
{
    /// <summary>
    /// Captura nativa via Media Foundation IMFSourceReader (synchronous pull).
    /// Suporta NV12/YUY2/RGB24/RGB32 com conversão para BGR via OpenCV.
    /// 4K@60fps, MF_LOW_LATENCY, zero-copy do buffer MF para Mat.
    /// </summary>
    internal sealed class MfSourceReaderCapture : IDisposable
    {
        private IMFSourceReader? _reader;
        private IMFMediaSource? _source;
        private Thread?         _thread;
        private volatile bool   _running;
        private Action<Mat>?    _onFrame;
        private bool            _mfStarted;

        // Formato negociado
        private int  _width;
        private int  _height;
        private int  _stride;
        private Guid _subtype;

        /// <summary>FPS nativo negociado pelo driver.</summary>
        public int NegotiatedFps { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // Inicialização
        // ─────────────────────────────────────────────────────────────────

        public bool Start(int deviceIndex, int w, int h, int fps, Action<Mat> onFrame)
        {
            _onFrame = onFrame;

            int hr = MfInterop.MFStartup(MfInterop.MF_VERSION);
            if (hr < 0)
            {
                Console.WriteLine($"[MF Native] MFStartup falhou: 0x{hr:X8}");
                return false;
            }
            _mfStarted = true;

            // 1. Enumera dispositivos de vídeo via MF
            hr = MfInterop.MFCreateAttributes(out var enumAttr, 1);
            if (hr < 0) { Cleanup(); return false; }

            enumAttr.SetGUID(MfInterop.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
                             MfInterop.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);

            hr = MfInterop.MFEnumDeviceSources(enumAttr, out IntPtr ppActivate, out uint count);
            Marshal.ReleaseComObject(enumAttr);

            if (hr < 0 || count == 0)
            {
                Console.WriteLine($"[MF Native] Nenhum dispositivo encontrado (hr=0x{hr:X8}, count={count})");
                Cleanup();
                return false;
            }

            // Lê array de ponteiros IMFActivate*
            var activates = new IMFActivate[count];
            for (int i = 0; i < (int)count; i++)
            {
                IntPtr ptr = Marshal.ReadIntPtr(ppActivate, i * IntPtr.Size);
                activates[i] = (IMFActivate)Marshal.GetObjectForIUnknown(ptr);
            }
            CoTaskMemFree(ppActivate);

            // Log dispositivos
            Console.WriteLine($"[MF Native] {count} dispositivo(s) encontrado(s):");
            for (int i = 0; i < (int)count; i++)
            {
                string name = GetActivateName(activates[i]);
                Console.WriteLine($"  [{i}] {name}");
            }

            if (deviceIndex < 0 || deviceIndex >= (int)count)
            {
                Console.WriteLine($"[MF Native] Índice {deviceIndex} inválido.");
                ReleaseActivates(activates);
                Cleanup();
                return false;
            }

            // 2. Ativa o dispositivo selecionado → IMFMediaSource
            hr = activates[deviceIndex].ActivateObject(
                typeof(IMFMediaSource).GUID, out object sourceObj);

            // Liberta todos os IMFActivate (já não precisamos)
            ReleaseActivates(activates);

            if (hr < 0)
            {
                Console.WriteLine($"[MF Native] ActivateObject falhou: 0x{hr:X8}");
                Cleanup();
                return false;
            }
            _source = (IMFMediaSource)sourceObj;

            // 3. Cria SourceReader com low latency
            hr = MfInterop.MFCreateAttributes(out var readerAttr, 3);
            if (hr < 0) { Cleanup(); return false; }

            readerAttr.SetUINT32(MfInterop.MF_LOW_LATENCY, 1);
            readerAttr.SetUINT32(MfInterop.MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, 0);
            readerAttr.SetUINT32(MfInterop.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);
            readerAttr.SetUINT32(MfInterop.MF_SOURCE_READER_DISCONNECT_MEDIASOURCE_ON_SHUTDOWN, 1);

            hr = MfInterop.MFCreateSourceReaderFromMediaSource(_source, readerAttr, out _reader);
            Marshal.ReleaseComObject(readerAttr);

            if (hr < 0 || _reader == null)
            {
                Console.WriteLine($"[MF Native] CreateSourceReader falhou: 0x{hr:X8}");
                Cleanup();
                return false;
            }

            // 4. Seleciona o melhor media type nativo (NV12 > YUY2 > RGB)
            if (!NegotiateMediaType(w, h, fps))
            {
                Console.WriteLine("[MF Native] Nenhum media type compatível encontrado.");
                Cleanup();
                return false;
            }

            // 5. Inicia thread de leitura
            _running = true;
            _thread = new Thread(ReadLoop)
            {
                Name         = "MF_Native_Read",
                IsBackground = true,
                Priority     = ThreadPriority.Highest
            };
            _thread.Start();

            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // Negociação de media type
        // ─────────────────────────────────────────────────────────────────

        private bool NegotiateMediaType(int targetW, int targetH, int targetFps)
        {
            if (_reader == null) return false;

            // Formatos preferidos em ordem de eficiência (raw > compressed)
            Guid[] preferred = {
                MfInterop.MFVideoFormat_NV12,
                MfInterop.MFVideoFormat_YUY2,
                MfInterop.MFVideoFormat_UYVY,
                MfInterop.MFVideoFormat_RGB24,
                MfInterop.MFVideoFormat_RGB32,
                MfInterop.MFVideoFormat_MJPG,
            };

            // Enumera todos os media types nativos
            IMFMediaType? bestType = null;
            long bestScore = long.MaxValue;
            Guid bestSubtype = Guid.Empty;
            int bestW = 0, bestH = 0;
            double bestFps = 0;

            Console.WriteLine($"[MF Native] Formatos disponíveis (pedido: {targetW}×{targetH}@{targetFps}fps):");

            for (uint i = 0; ; i++)
            {
                int hr = _reader.GetNativeMediaType(
                    MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, i, out var mt);

                if (hr == MfInterop.MF_E_NO_MORE_TYPES || hr < 0) break;

                try
                {
                    mt.GetGUID(MfInterop.MF_MT_SUBTYPE, out Guid subtype);
                    mt.GetUINT64(MfInterop.MF_MT_FRAME_SIZE, out ulong frameSize);
                    var (mw, mh) = MfInterop.Unpack2x32(frameSize);

                    double mfps = 0;
                    try
                    {
                        mt.GetUINT64(MfInterop.MF_MT_FRAME_RATE, out ulong frameRate);
                        var (num, den) = MfInterop.Unpack2x32(frameRate);
                        if (den > 0) mfps = num / (double)den;
                    }
                    catch { }

                    string fcc = SubtypeToString(subtype);
                    Console.WriteLine($"  [{i}] {mw}×{mh} @{mfps:0.##}fps [{fcc}]");

                    // Score: preferência de formato × resolução × fps
                    int formatPriority = Array.IndexOf(preferred, subtype);
                    if (formatPriority < 0) formatPriority = 100; // formato desconhecido

                    long dw = (long)mw - targetW;
                    long dh = (long)mh - targetH;
                    long resDiff = dw * dw + dh * dh;
                    double fpsDiff = Math.Abs(mfps - targetFps);

                    long score = (long)formatPriority * 100_000_000L
                               + resDiff * 1000L
                               + (long)(fpsDiff * 100);

                    if (score < bestScore)
                    {
                        bestScore   = score;
                        bestType    = mt;
                        bestSubtype = subtype;
                        bestW       = (int)mw;
                        bestH       = (int)mh;
                        bestFps     = mfps;
                        mt = null!; // don't release — we keep it
                    }
                }
                finally
                {
                    if (mt != null) Marshal.ReleaseComObject(mt);
                }
            }

            if (bestType == null) return false;

            // Aplica o media type selecionado
            int setHr = _reader.SetCurrentMediaType(
                MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, IntPtr.Zero, bestType);

            if (setHr < 0)
            {
                Console.WriteLine($"[MF Native] SetCurrentMediaType falhou: 0x{setHr:X8}");
                Marshal.ReleaseComObject(bestType);
                return false;
            }

            _width   = bestW;
            _height  = bestH;
            _subtype = bestSubtype;

            // Lê stride real do tipo aplicado
            try
            {
                _reader.GetCurrentMediaType(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, out var current);
                current.GetUINT32(MfInterop.MF_MT_DEFAULT_STRIDE, out uint stride);
                _stride = (int)stride;
                Marshal.ReleaseComObject(current);
            }
            catch
            {
                // Calcula stride padrão baseado no formato
                _stride = CalculateDefaultStride(_subtype, _width);
            }

            NegotiatedFps = (int)Math.Round(bestFps);
            Console.WriteLine($"[MF Native] Selecionado: {_width}×{_height} @{bestFps:0.##}fps " +
                              $"[{SubtypeToString(_subtype)}] stride={_stride}");

            Marshal.ReleaseComObject(bestType);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // Loop de leitura síncrono (pull model — menor latência que async)
        // ─────────────────────────────────────────────────────────────────

        private void ReadLoop()
        {
            while (_running && _reader != null)
            {
                try
                {
                    int hr = _reader.ReadSample(
                        MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                        0, // dwControlFlags
                        out _,
                        out uint streamFlags,
                        out _,
                        out IMFSample? sample);

                    if (hr < 0 || !_running) break;

                    // Stream end or error
                    if ((streamFlags & 0x100) != 0) // MF_SOURCE_READERF_ENDOFSTREAM
                    {
                        Console.WriteLine("[MF Native] End of stream.");
                        break;
                    }
                    if ((streamFlags & 0x200) != 0) // MF_SOURCE_READERF_ERROR
                    {
                        Console.WriteLine("[MF Native] Stream error.");
                        break;
                    }

                    if (sample == null) continue;

                    try
                    {
                        ProcessSample(sample);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sample);
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                        Console.WriteLine($"[MF Native] ReadLoop error: {ex.Message}");
                    break;
                }
            }
        }

        private void ProcessSample(IMFSample sample)
        {
            int hr = sample.ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
            if (hr < 0) return;

            try
            {
                // Tenta IMF2DBuffer para acesso com stride correto
                IntPtr dataPtr = IntPtr.Zero;
                int pitch = 0;
                bool is2D = false;
                IMF2DBuffer? buf2D = null;

                try
                {
                    buf2D = (IMF2DBuffer)buffer;
                    hr = buf2D.Lock2D(out dataPtr, out pitch);
                    if (hr >= 0) is2D = true;
                }
                catch
                {
                    buf2D = null;
                }

                if (!is2D)
                {
                    hr = buffer.Lock(out dataPtr, out _, out _);
                    if (hr < 0) return;
                    pitch = Math.Abs(_stride);
                }

                try
                {
                    ConvertToBgr(dataPtr, pitch);
                }
                finally
                {
                    if (is2D && buf2D != null)
                        buf2D.Unlock2D();
                    else
                        buffer.Unlock();
                }
            }
            finally
            {
                Marshal.ReleaseComObject(buffer);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Conversão de pixel format para BGR (Mat)
        // ─────────────────────────────────────────────────────────────────

        private void ConvertToBgr(IntPtr dataPtr, int pitch)
        {
            Mat? bgr = null;

            if (_subtype == MfInterop.MFVideoFormat_NV12)
            {
                // NV12: Y plane (w×h) + interleaved UV plane (w×h/2)
                // Total height for Mat: h * 3/2
                int nv12H = _height * 3 / 2;
                using var nv12 = Mat.FromPixelData(nv12H, _width, MatType.CV_8UC1, dataPtr, pitch);
                bgr = nv12.CvtColor(ColorConversionCodes.YUV2BGR_NV12);
            }
            else if (_subtype == MfInterop.MFVideoFormat_YUY2)
            {
                // YUY2: packed YUYV — 2 bytes per pixel in pairs
                using var yuy2 = Mat.FromPixelData(_height, _width, MatType.CV_8UC2, dataPtr, pitch);
                bgr = yuy2.CvtColor(ColorConversionCodes.YUV2BGR_YUY2);
            }
            else if (_subtype == MfInterop.MFVideoFormat_UYVY)
            {
                using var uyvy = Mat.FromPixelData(_height, _width, MatType.CV_8UC2, dataPtr, pitch);
                bgr = uyvy.CvtColor(ColorConversionCodes.YUV2BGR_UYVY);
            }
            else if (_subtype == MfInterop.MFVideoFormat_RGB24)
            {
                // RGB24 do MF é bottom-up BGR — stride pode ser negativo
                using var raw = Mat.FromPixelData(_height, _width, MatType.CV_8UC3, dataPtr, Math.Abs(pitch));
                if (pitch < 0 || _stride < 0)
                    bgr = raw.Flip(FlipMode.X); // bottom-up → top-down
                else
                {
                    bgr = new Mat();
                    raw.CopyTo(bgr);
                }
            }
            else if (_subtype == MfInterop.MFVideoFormat_RGB32)
            {
                using var bgra = Mat.FromPixelData(_height, _width, MatType.CV_8UC4, dataPtr, Math.Abs(pitch));
                Mat temp;
                if (pitch < 0 || _stride < 0)
                    temp = bgra.Flip(FlipMode.X);
                else
                    temp = bgra;
                bgr = temp.CvtColor(ColorConversionCodes.BGRA2BGR);
                if (temp != bgra) temp.Dispose();
            }
            else if (_subtype == MfInterop.MFVideoFormat_MJPG)
            {
                // MJPEG: decode JPEG completo
                int len;
                // Precisamos saber o tamanho do buffer para imdecode
                unsafe
                {
                    // Lê do buffer diretamente
                    len = _width * _height * 3; // upper bound estimate
                }
                // Para MJPG, usar buffer.GetCurrentLength seria melhor,
                // mas já temos o contiguous buffer — usar Mat.FromPixelData com tamanho real
                // Na prática, o buffer contiguo já contém o JPEG completo
                // Tentamos usar ImDecode com o raw bytes
                try
                {
                    // Cria array de bytes a partir do ponteiro
                    int bufLen = Math.Abs(pitch) * _height;
                    byte[] jpegData = new byte[bufLen];
                    Marshal.Copy(dataPtr, jpegData, 0, bufLen);
                    bgr = Cv2.ImDecode(jpegData, ImreadModes.Color);
                }
                catch
                {
                    return; // MJPEG decode failed
                }
            }

            if (bgr != null && !bgr.Empty())
            {
                _onFrame?.Invoke(bgr); // bgr é entregue ao pipeline; não Dispose aqui
            }
            else
            {
                bgr?.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Paragem
        // ─────────────────────────────────────────────────────────────────

        public void Stop()
        {
            _running = false;

            if (_thread != null && _thread.IsAlive)
            {
                // ReadSample pode bloquear — esperamos com timeout
                if (!_thread.Join(3000))
                    Console.WriteLine("[MF Native] WARN: ReadLoop não terminou em 3s");
            }
            _thread = null;

            Cleanup();
        }

        private void Cleanup()
        {
            if (_reader != null)
            {
                try { Marshal.ReleaseComObject(_reader); } catch { }
                _reader = null;
            }
            if (_source != null)
            {
                try { _source.Shutdown(); } catch { }
                try { Marshal.ReleaseComObject(_source); } catch { }
                _source = null;
            }
            if (_mfStarted)
            {
                try { MfInterop.MFShutdown(); } catch { }
                _mfStarted = false;
            }
        }

        public void Dispose() => Stop();

        // ─────────────────────────────────────────────────────────────────
        // Utilitários
        // ─────────────────────────────────────────────────────────────────

        private static string GetActivateName(IMFActivate activate)
        {
            try
            {
                activate.GetAllocatedString(
                    MfInterop.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME,
                    out string name, out _);
                return name ?? "(desconhecido)";
            }
            catch { return "(erro ao ler nome)"; }
        }

        private static void ReleaseActivates(IMFActivate[] activates)
        {
            foreach (var a in activates)
            {
                try { Marshal.ReleaseComObject(a); } catch { }
            }
        }

        private static int CalculateDefaultStride(Guid subtype, int width)
        {
            if (subtype == MfInterop.MFVideoFormat_NV12)   return width;
            if (subtype == MfInterop.MFVideoFormat_YUY2)   return width * 2;
            if (subtype == MfInterop.MFVideoFormat_UYVY)   return width * 2;
            if (subtype == MfInterop.MFVideoFormat_RGB24)  return width * 3;
            if (subtype == MfInterop.MFVideoFormat_RGB32)  return width * 4;
            if (subtype == MfInterop.MFVideoFormat_MJPG)   return width * 3;
            return width * 3; // fallback
        }

        private static string SubtypeToString(Guid subtype)
        {
            if (subtype == MfInterop.MFVideoFormat_NV12)   return "NV12";
            if (subtype == MfInterop.MFVideoFormat_YUY2)   return "YUY2";
            if (subtype == MfInterop.MFVideoFormat_UYVY)   return "UYVY";
            if (subtype == MfInterop.MFVideoFormat_RGB24)  return "RGB24";
            if (subtype == MfInterop.MFVideoFormat_RGB32)  return "RGB32";
            if (subtype == MfInterop.MFVideoFormat_MJPG)   return "MJPG";
            return subtype.ToString()[..8];
        }

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);
    }
}
