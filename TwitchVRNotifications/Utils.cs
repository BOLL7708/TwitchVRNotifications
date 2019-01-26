using System;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;

namespace TwitchVRNotifications
{
    class Utils
    {
        private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        public static string EncryptStringToBase64(string secretText, string additionalEntropyText)
        {
            return BytesToBase64String(EncryptBytes(StringToBytes(secretText), Base64StringToBytes(additionalEntropyText)));
        }

        public static string DecryptStringFromBase64(string protectedText, string additionalEntropyText)
        {
            return BytesToString(DecryptBytes(Base64StringToBytes(protectedText), Base64StringToBytes(additionalEntropyText)));
        }

        public static byte[] EncryptBytes(byte[] secretBytes, byte[] additionalEntropy)
        {
            byte[] protectedBytes = { };
            try
            {
                protectedBytes = ProtectedData.Protect(secretBytes, additionalEntropy, DataProtectionScope.CurrentUser);

            } catch (CryptographicException e)
            {
                Debug.WriteLine(e.Message);
            }
            return protectedBytes;
        }

        public static byte[] DecryptBytes(byte[] protectedBytes, byte[] additionalEntropy)
        {
            byte[] secretBytes = { };
            try
            {
                secretBytes = ProtectedData.Unprotect(protectedBytes, additionalEntropy, DataProtectionScope.CurrentUser);
            } catch(CryptographicException e)
            {
                Debug.WriteLine(e.Message);
            }
            return secretBytes;
        }
        
        public static byte[] StringToBytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
        public static string BytesToString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public static string BytesToBase64String(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        public static byte[] Base64StringToBytes(string base64text)
        {
            byte[] result = { };
            try
            {
                result = Convert.FromBase64String(base64text);
            } catch(FormatException e)
            {
                Debug.WriteLine(e.Message);
            }
            return result;
        }

        public static byte[] GetRandomBytes(int count)
        {
            var bytes = new byte[count];
            rngCsp.GetBytes(bytes);
            return bytes;
        }
    }
}
