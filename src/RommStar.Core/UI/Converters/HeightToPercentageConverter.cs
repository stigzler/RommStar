using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace RommStar.Core.UI.Converters
{
    internal class HeightToPercentageConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double newHeight = (double)value * (double.Parse(parameter.ToString()) / 100);

                return newHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double maxHeight && parameter is double percentage)
                return maxHeight / (percentage / 100);

            return 0;
        }
    }
}
