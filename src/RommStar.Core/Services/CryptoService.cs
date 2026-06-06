using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Services
{
    public class CryptoService
    {
        // Optional entropy can be used to further tie the data to the application
        private readonly byte[]? _entropy;

        public CryptoService(byte[]? optionalEntropy = null)
        {
            _entropy = optionalEntropy;
        }

        public byte[] Protect(byte[] plaintext)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            return ProtectedData.Protect(plaintext, _entropy, DataProtectionScope.CurrentUser);
        }

        public byte[] Unprotect(byte[] ciphertext)
        {
            if (ciphertext == null) throw new ArgumentNullException(nameof(ciphertext));
            return ProtectedData.Unprotect(ciphertext, _entropy, DataProtectionScope.CurrentUser);
        }

        public string ProtectToBase64(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            return Convert.ToBase64String(Protect(bytes));
        }

        public string? TryUnprotectFromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return string.Empty;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var plainBytes = Unprotect(bytes);
                return System.Text.Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null; // not a protected value or failed to decrypt
            }
        }
    }
}