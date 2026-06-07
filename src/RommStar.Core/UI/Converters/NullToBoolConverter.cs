using System;
using System.Globalization;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return false;
            }

            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}