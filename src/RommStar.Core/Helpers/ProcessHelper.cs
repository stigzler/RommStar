using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Helpers
{
    internal class ProcessHelper
    {
        internal static bool OpenLinkInBrowser(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., if the user has no default browser set, 
                // or the URL string is malformed)
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
                return false;
            }
        }
    }
}
