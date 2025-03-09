using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace SSE.CRA.UI
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    internal class ValidityBrushConverter : IValueConverter
    {
        #region properties
        public Brush ValidBrush { get; set; } = Brushes.Transparent;
        public Brush InvalidBrush { get; set; } = Brushes.Red;
        #endregion

        #region methods
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool valid = (bool)value;
            return valid ? ValidBrush : InvalidBrush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("not meant to be used");
        }
        #endregion
    }
}
