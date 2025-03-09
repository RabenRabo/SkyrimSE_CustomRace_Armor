using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SSE.CRA.UI
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    internal class BoolVisibilityConverter : IValueConverter
    {
        #region properties
        public Visibility TrueVisibility { get; set; } = Visibility.Visible;
        public Visibility FalseVisibility { get; set; } = Visibility.Collapsed;
        #endregion

        #region methods
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool valid = (bool)value;
            return valid ? TrueVisibility : FalseVisibility;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("not meant to be used");
        }
        #endregion
    }
}
