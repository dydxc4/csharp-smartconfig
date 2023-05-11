using System.Security.Cryptography;

namespace Sandwych.SmartConfig.EspTouchV2.Protocol
{
    public static class EspV2Aes
    {
        private static readonly byte[] _iv = new byte[16];

        public static byte[] Encrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = 128;
            aes.Key = key;
            aes.IV = _iv;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }
    }
}
