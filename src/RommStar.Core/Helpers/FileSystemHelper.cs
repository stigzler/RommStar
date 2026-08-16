using RommStar.Core.Models;
using RommStar.Core.Primitives;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RommStar.Core.Helpers
{
    public class FileSystemHelper
    {
        private static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        /// <summary>
        /// Safely retrieves available free space for local directories, mapped drives, and network UNC shares.
        /// </summary>
        public static long GetAvailableFreeSpace(string directoryPath)
        {
            try
            {
                if (GetDiskFreeSpaceEx(directoryPath, out ulong freeBytesAvailable, out _, out _))
                {
                    return (long)freeBytesAvailable;
                }
            }
            catch
            {
                // Fallback: If network permissions temporarily block checking, 
                // return MaxValue so we don't permanently stall the queue loop.
                return long.MaxValue;
            }

            return long.MaxValue;
        }

        /// <summary>
        /// Checks if file present on disk. Can also verify via sha1 if check set
        /// </summary>
        /// <param name="useSha1">Whether to check the SHA1 or not</param>
        /// <param name="path">Fullpath to file</param>
        /// <param name="sha1">the SHA1 string</param>
        /// <returns>True if passes checks</returns>
        public static bool LocalFilePresent(bool useSha1, string path, string sha1)
        {
            if (!useSha1 && !File.Exists(path)) return false;

            if (useSha1 && !FileSystemHelper.LocalFilePresent(path, sha1)) return false;

            return true;
        }

        /// <summary>
        /// Checks if file present on disk. Can also verify via sha1
        /// </summary>
        /// <param name="path">Fullpath to file</param>
        /// <param name="sha1">the SHA1 string</param>
        /// <returns>True if passes checks</returns>
        public static bool LocalFilePresent(string path, string expectedSha1)
        {
            // 1. Basic existence check
            if (!File.Exists(path)) return false;

            // 2. Bypass hashing if no expected hash was provided
            if (string.IsNullOrWhiteSpace(expectedSha1)) return false;

            string localHash = string.Empty;

            try
            {
                // 3. Open the file in a read-only, shared state to prevent locking crashes
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var sha1Hasher = SHA1.Create())
                {
                    var hashBytes = sha1Hasher.ComputeHash(stream);
                    localHash = BitConverter.ToString(hashBytes).Replace("-", "");
                }
            }
            catch (Exception ex)
            {
                // If the file is locked by the downloader or system, log it if needed, but fail gracefully
                System.Diagnostics.Debug.WriteLine($"[Hash Check] Failed to hash file {path}: {ex.Message}");
                return false;
            }

            // 4. Compare the generated hash against the expected hash (ignoring case)
            return localHash.Equals(expectedSha1.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolvedRompath(string directoryPath, string platformName = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                return Path.Combine(Constants.LaunchboxRootDir, "Games", platformName);
            }
            else if (directoryPath == $"Games\\{platformName}")
            {
                return Path.Combine(Constants.LaunchboxRootDir, directoryPath);
            }
            return directoryPath;
        }

        public static bool IsValidFilenameWithExtension(string filename)
        {
            // 1. Check for null or empty strings
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            // 2. Reject strings containing illegal characters (like slashes, colons, etc.)
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            // 3. Ensure it has a valid base name (rejects hidden-style files like ".zip")
            string baseName = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrWhiteSpace(baseName))
                return false;

            // 4. Ensure it has a valid extension (rejects "wipeout" or "wipeout.")
            string extension = Path.GetExtension(filename);
            if (string.IsNullOrWhiteSpace(extension) || extension == ".")
                return false;

            return true;
        }

        public static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag)
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);
    }
}