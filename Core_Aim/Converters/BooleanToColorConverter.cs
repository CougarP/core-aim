using System;
using System.Globalization;
using System.Windows.Data;
// Estas linhas forçam o uso do WPF, resolvendo o conflito com System.Drawing
using Media = System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Core_Aim.Converters
{
    // Converte booleano para Cor/Brush
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTrue && isTrue)
            {
                // Verde (Sucesso)
                return new SolidColorBrush(Media.Color.FromRgb(16, 185, 129));
            }

            // Vermelho (Erro/Falha) ou cor padrão
            return new SolidColorBrush(Media.Color.FromRgb(239, 68, 68));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}