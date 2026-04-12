using OpenCvSharp;
using System.Collections.Concurrent;

namespace Core_Aim.Services.Camera
{
    /// <summary>
    /// Pool de Mat pré-alocados para eliminar alocações no LOH durante captura.
    /// Thread-safe. Rent/Return: O(1) sem alocação de memória nativa.
    /// </summary>
    internal sealed class MatPool : IDisposable
    {
        private readonly ConcurrentQueue<Mat> _q = new();
        private readonly int _rows, _cols;
        private readonly MatType _type;

        public MatPool(int rows, int cols, MatType type, int initialCount)
        {
            _rows = rows; _cols = cols; _type = type;
            for (int i = 0; i < initialCount; i++)
                _q.Enqueue(new Mat(rows, cols, type));
        }

        /// <summary>Retira um frame do pool. Se vazio, aloca um novo (recuperação automática).</summary>
        public Mat Rent()
            => _q.TryDequeue(out var m) ? m : new Mat(_rows, _cols, _type);

        /// <summary>Devolve um frame ao pool para reutilização futura. Null e disposed são ignorados.</summary>
        public void Return(Mat? m)
        {
            if (m == null || m.IsDisposed) return;
            _q.Enqueue(m);
        }

        /// <summary>Quantidade de frames disponíveis no pool (para diagnóstico).</summary>
        public int Count => _q.Count;

        public void Dispose()
        {
            while (_q.TryDequeue(out var m)) m.Dispose();
        }
    }
}
