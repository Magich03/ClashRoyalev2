namespace ClashRoyale.Crypto
{
    using System.Collections.Generic;

    using ClashRoyale.Crypto.Nacl;
    using ClashRoyale.Extensions;

    public static class PepperFactory
    {
        /// <summary>
        /// Gets the latest supercell server public key for the <see cref="PepperCrypto"/>.
        /// </summary>
        public static byte[] PublicKey
        {
            get
            {
                return PepperFactory.PublicKeys[15];
            }
        }

        /// <summary>
        /// Gets the latest private server secret key for the <see cref="PepperCrypto"/>,
        /// and generates a public key with it.
        /// </summary>
        public static byte[] ModdedPublicKey
        {
            get
            {
                byte[] k = new byte[32];
                Curve25519Xsalsa20Poly1305.CryptoBoxGetpublickey(k, PepperFactory.GetSecretKey(14));
                return k;
            }
        }

        /// <summary>
        /// The patched client always encrypts with <see cref="ModdedPublicKey"/>, so any key version maps to secret key 14.
        /// </summary>
        private static readonly System.Lazy<byte[]> DeploymentKey = new(() =>
        {
            string path = System.Environment.GetEnvironmentVariable("CLASH_PEPPER_KEY_FILE")
                ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "Secrets", "pepper.key");
            if (!System.IO.File.Exists(path)) return null;
            byte[] key = System.IO.File.ReadAllBytes(path);
            if (key.Length != 32) throw new System.IO.InvalidDataException("Pepper key must be exactly 32 bytes.");
            return key;
        });
        public static byte[] GetSecretKey(int KeyVersion)
        {
            if (DeploymentKey.Value != null) return DeploymentKey.Value;
            return PepperFactory.SecretKeys.TryGetValue(KeyVersion, out byte[] Key) ? Key : PepperFactory.SecretKeys[14];
        }

        public static readonly Dictionary<int, byte[]> SecretKeys = new Dictionary<int, byte[]>
        {
            {
                14, "98 0C F7 BB 72 62 B3 86 FE A6 10 34 AB A7 37 06 13 62 79 19 66 6B 34 E6 EC F6 63 07 A3 81 DD 61".HexaToBytes()
            }
        };

        public static readonly Dictionary<int, byte[]> PublicKeys = new Dictionary<int, byte[]>
        {
            {
                15, "99 b6 18 76 f3 ff 18 ca ec a0 ae c1 f3 26 d9 98 1b bc af 64 e7 da a3 17 a7 f1 09 66 86 7a f9 68".HexaToBytes()
            }
        };
    }
}