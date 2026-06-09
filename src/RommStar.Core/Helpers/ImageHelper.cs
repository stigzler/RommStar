using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace RommStar.Core.UI.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Loads a BitmapImage from a file path without leaving a lock on the file.
        /// </summary>
        /// <param name="filePath">The full path to the image file.</param>
        /// <returns>A completely cached BitmapImage ready for WPF UI binding.</returns>
        public static BitmapImage LoadImageNoLock(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                BitmapImage bitmap = new BitmapImage();

                // Use a FileStream with Read access only
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    // This tells WPF to load the entire image into memory immediately
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }

                // Freeze the bitmap to make it cross-thread accessible and improve performance
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception)
            {
                // Handle or log exception as needed for your application
                return null;
            }
        }
    }
}