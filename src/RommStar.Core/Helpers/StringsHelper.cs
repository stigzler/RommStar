using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Helpers
{
    internal class StringsHelper
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Strips or replaces illegal characters from a file or folder name.
        /// </summary>
        /// <param name="name">The raw file/folder name string to clean.</param>
        /// <param name="replacement">The string to substitute for invalid characters (default is empty/strip).</param>
        /// <returns>A sanitized string safe for file systems.</returns>
        public static string SanitizeFileName(string name, string replacement = "")
        {
            if (string.IsNullOrEmpty(name))
                return name;

            StringBuilder sb = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                // Check if the character is illegal
                if (Array.IndexOf(InvalidFileNameChars, c) >= 0)
                {
                    sb.Append(replacement);
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}