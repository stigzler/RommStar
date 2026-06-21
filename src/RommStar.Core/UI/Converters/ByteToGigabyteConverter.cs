using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    [ValueConversion(typeof(long), typeof(double))]
    public class ByteToGigabyteConverter : IValueConverter
    {
        private const double BytesInGigabyte = 1073741824.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long longValue)
            {
                return Math.Round(longValue / BytesInGigabyte, 2);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && double.TryParse(stringValue, out double doubleValue))
            {
                return (long)(doubleValue * BytesInGigabyte);
            }
            if (value is double dblValue)
            {
                return (long)(dblValue * BytesInGigabyte);
            }
            return 0L;
        }
    }
}
