using SanteDB.Core.Model;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a default data signature service
    /// </summary>
    /// <remarks>This service is a simple data signature service</remarks>
    public class DefaultDataSigningService : IDataSigningService
    {

        /// <summary>
        /// Keys for the signing of data
        /// </summary>
        private ConcurrentDictionary<String, byte[]> m_keys = new ConcurrentDictionary<string, byte[]>();

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Default Data Signature Service";

        /// <summary>
        /// Get whether the signature is symmetric
        /// </summary>
        public bool IsSymmetric => true;

        /// <summary>
        /// Get the default data signing service (ctor)
        /// </summary>
        public DefaultDataSigningService()
        {
            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                    var appName = ApplicationContext.Current.Application.Name;
                    var app = ApplicationContext.Current.GetService<IRepositoryService<SecurityApplication>>().Find(a => a.Name == appName, 0, 1, out int tr).FirstOrDefault();
                    if (app == null)
                        app = ApplicationContext.Current.Application;
                    var secret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret ??
                        ApplicationContext.Current.Application.ApplicationSecret;

                    var keyData = ApplicationContext.Current.GetService<IPasswordHashingService>().ComputeHash(secret).ParseHexString();
                    m_keys.TryAdd($"SA.{app.Key.ToString()}", keyData);
                }
                catch { }
            };
        }

        /// <summary>
        /// Add a signing key
        /// </summary>
        /// <param name="keyId"></param>
        /// <param name="keyData"></param>
        /// <param name="signatureAlgorithm"></param>
        public void AddSigningKey(string keyId, byte[] keyData, string signatureAlgorithm)
        {
            if (!this.m_keys.TryAdd(keyId, keyData))
                throw new InvalidOperationException("Cannot add signing key");
        }

        /// <summary>
        /// Get all keys
        /// </summary>
        public IEnumerable<string> GetKeys()
        {
            return m_keys.Keys;
        }

        /// <summary>
        /// Gets the signature algorithm
        /// </summary>
        public string GetSignatureAlgorithm(string keyId = null)
        {
            return "HS256";
        }

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
