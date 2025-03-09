using System.Globalization;
using System.Windows.Data;

namespace SSE.CRA.UI
{
    [ValueConversion(typeof(bool), typeof(bool))]
    internal class BoolInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
