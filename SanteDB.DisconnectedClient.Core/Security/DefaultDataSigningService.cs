/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2020-5-2
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;
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
        private ConcurrentDictionary<String, SecuritySignatureConfiguration> m_keys = new ConcurrentDictionary<string, SecuritySignatureConfiguration>();

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
            // Load keys from configuration
            var configuredKeys = ApplicationContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>()?.SigningKeys;
            if (configuredKeys != null)
                foreach (var k in configuredKeys)
                    this.m_keys.TryAdd(k.KeyName, k);
            else
                configuredKeys = new List<SecuritySignatureConfiguration>(); // Temporary list

            ApplicationContext.Current.Started += (o, e) =>
            {
                try
                {
                   
                    var appName = ApplicationContext.Current.Application.Name;
                    var app = ApplicationContext.Current.GetService<IRepositoryService<SecurityApplication>>()?.Find(a => a.Name == appName, 0, 1, out int tr).FirstOrDefault() ?? ApplicationContext.Current.Application;
                    if (!this.m_keys.TryGetValue($"SA.{app.Key.ToString()}", out SecuritySignatureConfiguration meKey))
                    {
                        if (app == null)
                            app = ApplicationContext.Current.Application;
                        var secret = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().ApplicationSecret ??
                            ApplicationContext.Current.Application.ApplicationSecret;

                        meKey = new SecuritySignatureConfiguration()
                        {
                            KeyName = $"SA.{app.Key.ToString()}",
                            Algorithm = SignatureAlgorithm.HS256,
                            HmacSecret = secret
                        };
                        m_keys.TryAdd($"SA.{app.Key.ToString()}", meKey);
                        
                        configuredKeys.Add(meKey);
                    }
                    if (!m_keys.ContainsKey("default"))
                        m_keys.TryAdd("default", meKey);
                }
                catch(Exception ex) {
                    throw new Exception("Error starting up data signing service", ex);
                }
            };
        }

        /// <summary>
        /// Add a signing key
        /// </summary>
        public void AddSigningKey(string keyId, byte[] keyData, string signatureAlgorithm)
        {
            if (!this.m_keys.ContainsKey(keyId))
            {
                var keyConfig = new SecuritySignatureConfiguration()
                {
                    KeyName = keyId,
                    Algorithm = (SignatureAlgorithm)Enum.Parse(typeof(SignatureAlgorithm), signatureAlgorithm),
                    FindType = System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
                    FindValue = signatureAlgorithm != "HS256" ? BitConverter.ToString(keyData).Replace("-", "") : null,
                    StoreLocation = System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine,
                    StoreName = System.Security.Cryptography.X509Certificates.StoreName.My,
                    FindTypeSpecified = signatureAlgorithm != "HS256",
                    StoreLocationSpecified = signatureAlgorithm != "HS256",
                    StoreNameSpecified = signatureAlgorithm != "HS256"
                };

                if (signatureAlgorithm == "HS256")
                    keyConfig.SetSecret(keyData);

                if (!this.m_keys.TryAdd(keyId, keyConfig))
                    throw new InvalidOperationException("Cannot add signing key");
            }
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
            if (this.m_keys.TryGetValue(keyId ?? "default", out SecuritySignatureConfiguration config))
                return config.Algorithm.ToString();
            return null;
        }

        /// <summary>
        /// Sign the specified data 
        /// </summary>
        public byte[] SignData(byte[] data, string keyId = null)
        {
            // Fetch the key from the repository
            if (!this.m_keys.TryGetValue(keyId ?? "default", out SecuritySignatureConfiguration configuration))
                throw new InvalidOperationException($"Key {keyId ?? "default"} not found");

            switch (configuration.Algorithm)
            {
                case SignatureAlgorithm.HS256:
                    {
                        var key = configuration.GetSecret();
                        // Ensure 128 bit
                        while (key.Length < 16)
                            key = key.Concat(key).ToArray();

                        var hmac = new System.Security.Cryptography.HMACSHA256(key);
                        return hmac.ComputeHash(data);
                    }
                case SignatureAlgorithm.RS256:
                case SignatureAlgorithm.RS512:
                    {

                        if (!configuration.Certificate.HasPrivateKey)
                            throw new InvalidOperationException("You must have the private key to sign data with this certificate");
                        var csp = (RSACryptoServiceProvider)configuration.Certificate.PrivateKey;
                        RSAPKCS1SignatureFormatter formatter = new RSAPKCS1SignatureFormatter(csp);
                        formatter.SetHashAlgorithm(configuration.Algorithm == SignatureAlgorithm.RS256 ? "SHA256" : "SHA512");
                        return formatter.CreateSignature(data);
                    }
                default:
                    throw new InvalidOperationException("Cannot generate digital signature");
            }
        }

        /// <summary>
        /// Verify the input data against the specified signature
        /// </summary>
        public bool Verify(byte[] data, byte[] signature, string keyId = null)
        {
            // Fetch the key from the repository
            if (!this.m_keys.TryGetValue(keyId ?? "default", out SecuritySignatureConfiguration configuration))
                throw new InvalidOperationException($"Key {keyId ?? "default"} not found");

            switch (configuration.Algorithm)
            {
                case SignatureAlgorithm.HS256:
                    {
                        var key = configuration.GetSecret();
                        // Ensure 128 bit
                        while (key.Length < 16)
                            key = key.Concat(key).ToArray();

                        var hmac = new System.Security.Cryptography.HMACSHA256(key);
                        return hmac.ComputeHash(data).SequenceEqual(signature);
                    }
                case SignatureAlgorithm.RS256:
                case SignatureAlgorithm.RS512:
                    {
                        var csp = (RSACryptoServiceProvider)configuration.Certificate.PublicKey.Key;
                        RSAPKCS1SignatureDeformatter formatter = new RSAPKCS1SignatureDeformatter(csp);
                        formatter.SetHashAlgorithm(configuration.Algorithm == SignatureAlgorithm.RS256 ? "SHA256" : "SHA512");
                        // Compute SHA hash
                        HashAlgorithm sha = configuration.Algorithm == SignatureAlgorithm.RS256 ? (HashAlgorithm)SHA256.Create() : (HashAlgorithm)SHA512.Create();
                        var hashValue = sha.ComputeHash(data);
                        return formatter.VerifySignature(hashValue, signature);
                    }
                default:
                    throw new InvalidOperationException("Cannot generate digital signature");
            }
        }
    }
}
