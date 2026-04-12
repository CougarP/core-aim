using System;
using System.Globalization;
using System.Windows.Data;

namespace Core_Aim.Converters
{
    public class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "CONNECTED" : "DISCONNECTED";
            }
            return "DISCONNECTED";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}