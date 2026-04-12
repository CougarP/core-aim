using System;
using System.Globalization;
using System.Windows.Data;
// Alias explícitos para garantir que usamos WPF e não Windows Forms
using Media = System.Windows.Media;

namespace Core_Aim.Converters
{
    // Converte o status (bool) para Texto no botão
    public class AppStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool connected && connected)
                return "CONNECTED";

            return "DISCONNECTED";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }

    // Converte o status (bool) para Cor de Fundo no botão
    public class AppColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool connected && connected)
            {
                // Retorna Verde (#10B981) usando classes explícitas do WPF
                return new Media.SolidColorBrush(Media.Color.FromRgb(16, 185, 129));
            }

            // Retorna Vermelho (#EF4444)
            return new Media.SolidColorBrush(Media.Color.FromRgb(239, 68, 68));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    // Converte bool (botao pressionado) para cor de fundo
    public class BoolToButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool pressed && pressed)
                return new Media.SolidColorBrush(Media.Color.FromRgb(16, 185, 129)); // verde
            return new Media.SolidColorBrush(Media.Color.FromRgb(39, 39, 42)); // cinza escuro
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }

    // Converte null para Visibilidade (caso esteja a usar noutros sítios)
    public class AppNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}