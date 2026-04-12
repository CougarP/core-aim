using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Core_Aim.Services.Configuration;
using Core_Aim.Views;
using Core_Aim.Views.Splash;

namespace Core_Aim
{
    public partial class App : System.Windows.Application
    {
        // ── Single-instance lock ─────────────────────────────────────────────
        // Mutex global ao Local namespace (por sessão de utilizador). Se outro
        // processo Core_Aim já o possui, foca a janela existente e sai.
        // Mantemos referência estática para o mutex viver enquanto o processo viver.
        private static Mutex? _singleInstanceMutex;
        private const string SingleInstanceMutexName = @"Local\Core_Aim_Pro_SingleInstance_v1";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // ── Single-instance check ────────────────────────────────────────
            // Tenta criar/possuir o mutex. Se já existir, outro processo está
            // a correr — foca-o e sai sem mostrar UI nenhuma.
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                FocusExistingInstance();
                Shutdown();
                return;
            }

            string crashPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

            DispatcherUnhandledException += (_, ex) =>
            {
                var msg = $"[{DateTime.Now:HH:mm:ss}] ERRO FATAL (UI):\n{ex.Exception.GetType().Name}\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}";
                try { File.WriteAllText(crashPath, msg); } catch { }
                System.Windows.MessageBox.Show(msg, "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                ex.Handled = true;
                Shutdown(1);
            };

            // Captura exceções em threads de background que crasham o processo
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as System.Exception;
                var msg = $"[{DateTime.Now:HH:mm:ss}] ERRO FATAL (Background Thread):\n{ex?.GetType().Name}\n{ex?.Message}\n\n{ex?.StackTrace}";
                try { File.WriteAllText(crashPath, msg); } catch { }
                System.Console.Error.WriteLine(msg);
            };

            // Captura Task exceptions não observadas (evita crash silencioso)
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                var msg = $"[{DateTime.Now:HH:mm:ss}] Task Exception (ignorada):\n{args.Exception?.GetType().Name}\n{args.Exception?.Message}\n{args.Exception?.StackTrace}";
                try { File.AppendAllText(crashPath, "\n" + msg); } catch { }
                System.Console.Error.WriteLine(msg);
                args.SetObserved(); // previne crash do processo
            };

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            // Splash style — escolha agora vive na aba User da MainWindow.
            // Se nunca foi configurado, cai no default (NeuralBoot via SplashFactory).
            var settings = new AppSettingsService();

            // ─────────────────────────────────────────────────────────────
            // AUTH FLOW
            // ─────────────────────────────────────────────────────────────
            // Se auth.bin existir → splash primeiro, depois auto-login silencioso.
            // Se não existir → login MANUAL antes de tudo (sem splash), e só
            // depois de autenticado é que splash + main window aparecem.
            // Isto evita ver o splash inteiro antes de descobrir que ainda
            // tens que fazer login pela primeira vez.
            // ─────────────────────────────────────────────────────────────
            string authBinPath = System.IO.Path.Combine(AppContext.BaseDirectory, "auth.bin");
            bool hasAuthBin = System.IO.File.Exists(authBinPath);

            void LaunchMain()
            {
                var mainWindow = new MainWindow();
                this.MainWindow = mainWindow;
                mainWindow.Show();
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            void RunSplashThenMain()
            {
                ISplashWindow sp = SplashFactory.Create(settings.SplashStyle);
                sp.Completed += LaunchMain;
                sp.Show();
            }

            if (hasAuthBin)
            {
                // Auto-login silencioso após splash
                ISplashWindow splash = SplashFactory.Create(settings.SplashStyle);
                splash.Completed += () =>
                {
                    var loginWindow = new LoginWindow
                    {
                        Opacity = 0,
                        ShowInTaskbar = false
                    };

                    bool? result = loginWindow.ShowDialog();
                    if (result != true)
                    {
                        Shutdown();
                        return;
                    }
                    LaunchMain();
                };
                splash.Show();
            }
            else
            {
                // Sem auth.bin → login manual antes de qualquer splash
                var loginWindow = new LoginWindow();
                bool? result = loginWindow.ShowDialog();
                if (result != true)
                {
                    Shutdown();
                    return;
                }
                RunSplashThenMain();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }

        /// <summary>
        /// Procura o processo Core_Aim já em execução e traz a sua janela
        /// principal para a frente. Chamado quando o mutex é detectado.
        /// </summary>
        private static void FocusExistingInstance()
        {
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var others = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
                foreach (var p in others)
                {
                    if (p.Id == current.Id) continue;
                    var hwnd = p.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                        SetForegroundWindow(hwnd);
                        break;
                    }
                }
            }
            catch { }
        }
    }
}
