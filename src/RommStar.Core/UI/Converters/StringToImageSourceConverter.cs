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
                return null; // Return null to hide the image
            }

            // Use the helper method to guarantee the file stream is closed immediately
            return ImageHelper.LoadImageNoLock(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}