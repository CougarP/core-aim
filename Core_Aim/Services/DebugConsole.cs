using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace Core_Aim.Services
{
    /// <summary>
    /// Redireciona Console.WriteLine para uma ObservableCollection exibida na UI.
    /// Chamar Attach() uma vez no startup.
    /// </summary>
    public static class DebugConsole
    {
        private const int MaxLines = 600;

        private static readonly ObservableCollection<string> _lines = new();
        public static ObservableCollection<string> Lines => _lines;

        private static bool _attached = false;

        /// <summary>Redireciona stdout e stderr para o console embutido.</summary>
        public static void Attach()
        {
            if (_attached) return;
            _attached = true;
            Console.SetOut(new ConsoleWriter(""));
            Console.SetError(new ConsoleWriter("[ERR] "));
        }

        /// <summary>Adiciona uma linha manualmente (pode ser chamado de qualquer thread).</summary>
        public static void Log(string message) => AppendLine("", message);

        internal static void AppendLine(string prefix, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var entry = $"{DateTime.Now:HH:mm:ss.fff}  {prefix}{text}";

            if (System.Windows.Application.Current is { } app)
                app.Dispatcher.InvokeAsync(() =>
                {
                    _lines.Add(entry);
                    while (_lines.Count > MaxLines)
                        _lines.RemoveAt(0);
                });
        }

        // ── Console redirect writer ────────────────────────────────────────
        private sealed class ConsoleWriter : TextWriter
        {
            private readonly string _prefix;
            private readonly StringBuilder _buf = new();

            public ConsoleWriter(string prefix) => _prefix = prefix;
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    var line = _buf.ToString().TrimEnd('\r');
                    _buf.Clear();
                    AppendLine(_prefix, line);
                }
                else
                {
                    _buf.Append(value);
                }
            }
        }
    }
}
