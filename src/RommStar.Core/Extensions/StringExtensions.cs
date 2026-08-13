using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Extensions
{
    public static class StringExtensions
    {
        public static void AppendIfNotNull(this StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            sb.Append(text);
        }

        public static string RedactSensitiveInfo(this string text, bool redact)
        {
            if (redact) { return "[REDACTED]"; }
            return text;
        }

    }
}
