using Core_Aim.Data;
using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace Core_Aim.Services.AI
{
    public static class ColorFilterSystem
    {
        // Estrutura auxiliar para evitar re-parsing constante
        private struct HsvTarget { public int H; public int S; public int V; }

        public static void Apply(Mat imgBgr, List<YoloDetectionResult> detections, string hexString, int tolerance)
        {
            if (detections == null || detections.Count == 0 || imgBgr.Empty() || string.IsNullOrWhiteSpace(hexString))
                return;

            // Configurações fixas (iguais ao C++)
            const int colorBoxDistance = 40;
            const int minCount = 5;
            const int sFloor = 50;
            const int vFloor = 50;

            // Parse das cores (separa por vírgula se houver múltiplas)
            var targets = ParseHexColors(hexString);
            if (targets.Count == 0) return;

            int hTol = Math.Clamp(tolerance, 0, 90);
            int sTol = hTol * 2;
            int vTol = hTol * 2;

            // Como YoloDetectionResult é struct, usamos for loop para modificar in-place na lista
            for (int i = 0; i < detections.Count; i++)
            {
                var d = detections[i]; // Copia

                // Calcula a geometria
                int x1 = (int)d.BoundingBox.X;
                int y1 = (int)d.BoundingBox.Y;
                int w = (int)d.BoundingBox.Width;

                if (w <= 0) continue;

                int centerX = x1 + w / 2;
                int regionW = w; // Largura da box = largura da verificação
                int rTop = Math.Max(0, y1 - colorBoxDistance);

                int rx1 = Math.Max(0, centerX - regionW / 2);
                int rx2 = Math.Min(imgBgr.Cols, centerX + regionW / 2);

                // Se a região for inválida, ignora
                if (rTop >= y1 || rx1 >= rx2) continue;

                Rect roiRect = new Rect(rx1, rTop, rx2 - rx1, y1 - rTop);

                // Interseção segura com a imagem
                roiRect = roiRect.Intersect(new Rect(0, 0, imgBgr.Cols, imgBgr.Rows));
                if (roiRect.Width <= 0 || roiRect.Height <= 0) continue;

                // Processamento de Imagem
                using (Mat regionMat = imgBgr[roiRect])
                using (Mat hsvMat = new Mat())
                {
                    Cv2.CvtColor(regionMat, hsvMat, ColorConversionCodes.BGR2HSV);

                    // Cria máscara acumulativa
                    using (Mat totalMask = Mat.Zeros(hsvMat.Size(), MatType.CV_8UC1))
                    {
                        foreach (var t in targets)
                        {
                            Scalar lower = new Scalar(Math.Max(t.H - hTol, 0), Math.Max(t.S - sTol, sFloor), Math.Max(t.V - vTol, vFloor));
                            Scalar upper = new Scalar(Math.Min(t.H + hTol, 179), Math.Min(t.S + sTol, 255), Math.Min(t.V + vTol, 255));

                            using (Mat tempMask = new Mat())
                            {
                                Cv2.InRange(hsvMat, lower, upper, tempMask);
                                Cv2.BitwiseOr(totalMask, tempMask, totalMask);
                            }
                        }

                        if (Cv2.CountNonZero(totalMask) >= minCount)
                        {
                            d.ClassId = 99; // Classifica como alvo especial
                            d.Label = "COLOR_LOCK"; // Visual feedback
                            detections[i] = d; // Devolve a struct modificada para a lista
                        }
                    }
                }
            }
        }

        private static List<HsvTarget> ParseHexColors(string input)
        {
            var list = new List<HsvTarget>();
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                string hex = part.Replace("#", "").Trim();
                if (hex.Length != 6) continue;

                try
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);

                    // Usa OpenCV para converter RGB -> HSV exatamente como a imagem
                    using (Mat pix = new Mat(1, 1, MatType.CV_8UC3, new Scalar(b, g, r)))
                    using (Mat hsv = new Mat())
                    {
                        Cv2.CvtColor(pix, hsv, ColorConversionCodes.BGR2HSV);
                        var vec = hsv.At<Vec3b>(0, 0);
                        list.Add(new HsvTarget { H = vec.Item0, S = vec.Item1, V = vec.Item2 });
                    }
                }
                catch { }
            }
            return list;
        }
    }
}