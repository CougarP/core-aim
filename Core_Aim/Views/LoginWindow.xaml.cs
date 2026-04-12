using System.Windows;
using System.Windows.Input;
using Core_Aim.ViewModels;
using Core_Aim.Views.Splash;

namespace Core_Aim.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            if (DataContext is LoginViewModel vm)
            {
                vm.OnLoginResult += (success) =>
                {
                    if (success)
                    {
                        this.DialogResult = true;
                        this.Close();
                    }
                };

                // Auto-login falhou → torna janela visível para login manual
                vm.OnShowRequested += () =>
                {
                    this.Opacity        = 1;
                    this.ShowInTaskbar  = true;
                    this.Activate();
                };
            }
        }

        // Permite arrastar a janela clicando na barra superior
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // Abre o seletor de splash style — usuário pode trocar a qualquer hora.
        private void OpenSplashPicker_Click(object sender, RoutedEventArgs e)
        {
            var picker = new SplashPickerWindow { Owner = this };
            picker.ShowDialog();
        }
    }
}