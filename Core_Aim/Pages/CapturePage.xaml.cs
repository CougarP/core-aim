using System.Windows;
using System.Windows.Controls;
using Core_Aim.ViewModels;
using Button = System.Windows.Controls.Button;

namespace Core_Aim.Pages
{
    public partial class CapturePage : System.Windows.Controls.UserControl
    {
        public CapturePage()
        {
            InitializeComponent();
        }

        private void CapturePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split(',');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int w)
                    && int.TryParse(parts[1], out int h))
                {
                    if (DataContext is MainViewModel vm)
                    {
                        vm.SettingsViewModel.CaptureWidth  = w;
                        vm.SettingsViewModel.CaptureHeight = h;
                    }
                }
            }
        }
    }
}
