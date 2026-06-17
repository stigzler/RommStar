using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class InverseBoolConverter : IValueConverter
    {
        public bool Negate { get; set; } = false;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue ^ Negate;
            else if (value is null)
                return Negate;  // Return the negation of Negate when input is null

            return value;
            //throw new ArgumentException("Input must be either a boolean or null");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool booleanValue = (bool)value;
            return !booleanValue ^ Negate;
        }
    }
}
