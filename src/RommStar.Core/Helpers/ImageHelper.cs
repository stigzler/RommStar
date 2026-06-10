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
        /// Also optimizes for sharpness by setting the DecodePixelWidth property (approximate width of final rendered image)
        /// </summary>
        /// <param name="filePath">The full path to the image file.</param>
        /// <param name="decodeWidth">Optional target width constraint to optimize memory and decode sharpness.</param>
        public static BitmapImage LoadImageNoLock(string filePath, int decodeWidth = 0)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                BitmapImage bitmap = new BitmapImage();

                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;

                    // If a target size is passed, tell the hardware decoder to scale it safely on load
                    if (decodeWidth > 0)
                    {
                        bitmap.DecodePixelWidth = decodeWidth;
                    }

                    bitmap.EndInit();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}