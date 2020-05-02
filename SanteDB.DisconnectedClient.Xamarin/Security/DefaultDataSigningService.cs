using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Security
{
    /// <summary>
    /// Represents a default data signature service
    /// </summary>
    /// <remarks>This service is a simple data signature service</remarks>
    public class DefaultDataSigningService : IDataSigningService
    {
        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Default Data Signature Service";

        /// <summary>
        /// Sign the specified data 
        /// </summary>
        public byte[] SignData(byte[] data, string keyId = null)
        {
            // TODO: Actually use the private key and RS256
            byte[] key = ApplicationContext.Current.GetCurrentContextSecurityKey();
            if (key == null) // NOCRYPT is turned on 
                key = System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(ApplicationContext.Current.Application.ApplicationSecret));

            if (!String.IsNullOrEmpty(keyId))
            {
                // TODO: Actually have a key store
                key = Encoding.UTF8.GetBytes(keyId);
            }

            // Ensure 128 bit
            while (key.Length < 16)
                key = key.Concat(key).ToArray();

            var hmac = new System.Security.Cryptography.HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        /// <summary>
        /// Verify the input data against the specified signature
        /// </summary>
        public bool Verify(byte[] data, byte[] signature, string keyId = null)
        {
            var newSig = this.SignData(data, keyId);
            return newSig.SequenceEqual(signature);
        }
    }
}
