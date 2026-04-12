using System;
using System.Windows;
using System.Windows.Controls;
using Core_Aim.ViewModels;

namespace Core_Aim.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private SettingsViewModel? VM =>
            this.DataContext as SettingsViewModel;

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
                return;

            var window = Window.GetWindow(this);
            if (window?.DataContext is MainViewModel mainVm)
            {
                if (mainVm.SettingsViewModel != null)
                {
                    this.DataContext = mainVm.SettingsViewModel;
                }
            }
        }

    }
}
