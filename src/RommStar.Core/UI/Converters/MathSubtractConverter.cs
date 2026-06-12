using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.Converters
{
    public class MathSubtractConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth && parameter != null)
            {
                if (double.TryParse(parameter.ToString(), out double subtractValue))
                {
                    // Subtract the buffer space from the ComboBox width
                    return Math.Max(0, actualWidth - subtractValue);
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
