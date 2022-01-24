using System.Security.Cryptography;
using System.Text;

namespace GuacamoleSharp.Server
{
    public static class TokenEncrypter
    {
        #region Private Fields

        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        #endregion Private Fields

        #region Public Methods

        public static string DecryptString(string password, string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            string base64Text = cipherText
                .Replace('_', '/')
                .Replace('-', '+');

            switch (cipherText.Length % 4)
            {
                case 2: base64Text += "=="; break;
                case 3: base64Text += "="; break;
            }

            byte[] cipherBytes = Convert.FromBase64String(base64Text);
            byte[] key = GenerateKey(password);
            byte[] iv = cipherBytes.Take(16).ToArray();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var memStream = new MemoryStream(cipherBytes.Skip(16).ToArray()))
                {
                    using (var cryStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (var reader = new StreamReader(cryStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public static string EncryptString(string password, string plainText)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrWhiteSpace(plainText))
                throw new ArgumentNullException(nameof(plainText));

            byte[] key = GenerateKey(password);
            byte[] iv = new byte[16];
            _rng.GetBytes(iv);

            byte[] result;

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encrypter = aes.CreateEncryptor(aes.Key, aes.IV);

                byte[] token;

                using (var memStream = new MemoryStream())
                {
                    using (var cryStream = new CryptoStream(memStream, encrypter, CryptoStreamMode.Write))
                    {
                        using (var writer = new StreamWriter(cryStream))
                        {
                            writer.Write(plainText);
                        }

                        token = memStream.ToArray();
                    }
                }

                result = new byte[iv.Length + token.Length];

                iv.CopyTo(result, 0);
                token.CopyTo(result, iv.Length);
            }

            string base64result = Convert.ToBase64String(result);
            return base64result.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        #endregion Public Methods

        #region Private Methods

        private static byte[] GenerateKey(string password)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(password);
            using var md5 = MD5.Create();
            return md5.ComputeHash(keyBytes);
        }

        #endregion Private Methods
    }
}
