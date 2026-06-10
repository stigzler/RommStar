using RommStar.Core.UI.Helpers;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RommStar.Core.UI.Converters
{
    internal class StringToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value?.ToString();

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            // 1. Check if a parameter was passed from XAML
            int decodeWidth = 0;
            if (parameter != null && int.TryParse(parameter.ToString(), out int parsedWidth))
            {
                decodeWidth = parsedWidth;
            }

            // 2. Pass the parsed integer value to your LoadImage method
            return ImageHelper.LoadImageNoLock(path, decodeWidth);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}