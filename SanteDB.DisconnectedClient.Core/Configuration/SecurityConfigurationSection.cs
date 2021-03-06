/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Configuration;

namespace SanteDB.DisconnectedClient.Configuration
{

    /// <summary>
    /// Dictates how the domain client performs authentication of the application
    /// </summary>
    public enum DomainClientAuthentication
    {
        /// <summary>
        /// Authentication is performed using a certificate
        /// </summary>
        [XmlEnum("cert")]
        Certificate,
        /// <summary>
        /// Authentication is performed using BASIC AUTH
        /// </summary>
        [XmlEnum("basic")]
        Basic,
        /// <summary>
        /// Authentication is performed using inline client credentials
        /// </summary>
        Inline
    }

    /// <summary>
    /// Security configuration section
    /// </summary>
    [XmlType(nameof(SecurityConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")][JsonObject(nameof(SecurityConfigurationSection))]
    public class SecurityConfigurationSection : IConfigurationSection
    {
	    // Application secret
	    private string m_applicationSecret;

	    // Device secret
	    private string m_deviceSecret;

	    /// <summary>
        /// Max local session
        /// </summary>
        public SecurityConfigurationSection()
        {
            this.MaxLocalSession = new TimeSpan(0, 30, 0);
            this.DomainAuthentication = DomainClientAuthentication.Basic;
        }

	    /// <summary>
        /// Sets the application secret.
        /// </summary>
        /// <value>The application secret.</value>
        [XmlIgnore][JsonProperty("client_secret")]
        public string ApplicationSecret
        {
            get
            {
                try
                {
                    if (this.PlainTextSecret && string.IsNullOrEmpty(this.m_applicationSecret) && this.ApplicationSecretXml != null)
                    {
	                    this.m_applicationSecret = Encoding.UTF8.GetString(this.ApplicationSecretXml, 0, this.ApplicationSecretXml.Length);
                    }
                    else if (string.IsNullOrEmpty(this.m_applicationSecret) && this.ApplicationSecretXml != null)
                    {
                        var cryptoService = ApplicationContext.Current.GetService<ISymmetricCryptographicProvider>();
                        if (cryptoService != null)
                        {
                            if (ApplicationContext.Current.GetCurrentContextSecurityKey() != null)
                            {
                                var res = cryptoService.Decrypt(this.ApplicationSecretXml.Skip(16).ToArray(), ApplicationContext.Current.GetCurrentContextSecurityKey(), this.ApplicationSecretXml.Take(16).ToArray());
                                this.m_applicationSecret = Encoding.UTF8.GetString(res, 0, res.Length);
                            }
                            else
                            {
	                            this.m_applicationSecret = Encoding.UTF8.GetString(this.ApplicationSecretXml, 0, this.ApplicationSecretXml.Length);
                            }
                        }
                        else
                        {
	                        this.m_applicationSecret = Encoding.UTF8.GetString(this.ApplicationSecretXml, 0, this.ApplicationSecretXml.Length);
                        }
                    }
                    return this.m_applicationSecret;
                }
                catch { return null; }
            }
            set
            {
                try
                {
                    this.m_applicationSecret = value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        if (this.PlainTextSecret)
                        {
	                        this.ApplicationSecretXml = Encoding.UTF8.GetBytes(value);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                var cryptoService = ApplicationContext.Current.GetService<ISymmetricCryptographicProvider>();
                                if (cryptoService != null)
                                {
                                    if (ApplicationContext.Current.GetCurrentContextSecurityKey() != null)
                                    {
                                        var iv = cryptoService.GenerateIV();
                                        var res = cryptoService.Encrypt(Encoding.UTF8.GetBytes(value), ApplicationContext.Current.GetCurrentContextSecurityKey(), iv);
                                        var b = new byte[iv.Length + res.Length];
                                        Array.Copy(iv, b, iv.Length);
                                        Array.Copy(res, 0, b, iv.Length, res.Length);
                                        this.DeviceSecretXml = b;
                                    }
                                    else
                                    {
	                                    this.ApplicationSecretXml = Encoding.UTF8.GetBytes(value);
                                    }
                                }
                                else
                                {
	                                this.ApplicationSecretXml = Encoding.UTF8.GetBytes(value);
                                }
                            }
                        }
                    }
                    else
                    {
	                    this.ApplicationSecretXml = null;
                    }
                }
                catch {
                    this.PlainTextSecret = true;
                    this.ApplicationSecretXml = Encoding.UTF8.GetBytes(value);

                }

            }
        }

	    /// <summary>
        /// Gets or sets the configured device name
        /// </summary>
        /// <value>The name of the device.</value>
        [XmlElement("deviceName")][JsonProperty("deviceName")]
        public string DeviceName
        {
            get;
            set;
        }

	    /// <summary>
        /// Plain text secret
        /// </summary>
        [XmlElement("plainTextSecret")][JsonProperty("plainTextSecret")]
        public bool PlainTextSecret { get; set; }

	    /// <summary>
        /// Gets or sets the device secret
        /// </summary>
        [XmlElement("deviceSecret")][JsonIgnore]
        public byte[] DeviceSecretXml
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the application secret
        /// </summary>
        [XmlElement("applicationSecret")][JsonIgnore]
        public byte[] ApplicationSecretXml
        {
            get;
            set;
        }

	    /// <summary>
        /// Audit retention
        /// </summary>
        [XmlIgnore][JsonIgnore]
        public TimeSpan AuditRetention { get; set; }

	    /// <summary>
        /// Gets or sets teh device certificate
        /// </summary>
        [XmlElement("deviceCertificate")][JsonIgnore]
        public ServiceCertificateConfiguration DeviceCertificate { get; set; }

	    /// <summary>
        /// Audit retention
        /// </summary>
        [XmlElement("auditRetention")][JsonProperty("auditRetention")]
        public string AuditRetentionXml
        {
            get => this.AuditRetention.ToString();
            set => this.AuditRetention = TimeSpan.Parse(value);
	    }


	    /// <summary>
        /// Domain authentication
        /// </summary>
        [XmlElement("domainSecurity")][JsonProperty("domainSecurity")]
        public DomainClientAuthentication DomainAuthentication
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the allowed token type
        /// </summary>
        /// <value>The type of the token.</value>
        [XmlElement("tokenType")][JsonIgnore]
        public string TokenType
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the token algorithms.
        /// </summary>
        /// <value>The token algorithms.</value>
        [XmlElement("tokenAlg")][JsonIgnore]
        public List<string> TokenAlgorithms
        {
            get;
            set;
        }

	    /// <summary>
        /// Sets the device secret.
        /// </summary>
        /// <value>The device secret.</value>
        [XmlIgnore][JsonIgnore]
        public string DeviceSecret
        {
            get
            {
                if (this.PlainTextSecret && string.IsNullOrEmpty(this.m_deviceSecret) && this.DeviceSecretXml != null)
                {
	                this.m_deviceSecret = Encoding.UTF8.GetString(this.DeviceSecretXml, 0, this.DeviceSecretXml.Length);
                }
                else if (string.IsNullOrEmpty(this.m_deviceSecret) && this.DeviceSecretXml != null)
                {
                    var cryptoService = ApplicationContext.Current.GetService<ISymmetricCryptographicProvider>();
                    if (cryptoService != null)
                    {
                        if (ApplicationContext.Current.GetCurrentContextSecurityKey() != null)
                        {
                            var res = cryptoService.Decrypt(this.DeviceSecretXml.Skip(16).ToArray(), ApplicationContext.Current.GetCurrentContextSecurityKey(), this.DeviceSecretXml.Take(16).ToArray());
                            this.m_deviceSecret = Encoding.UTF8.GetString(res, 0, res.Length);
                        }
                        else
                        {
	                        this.m_deviceSecret = Encoding.UTF8.GetString(this.DeviceSecretXml, 0, this.DeviceSecretXml.Length);
                        }
                    }
                    else
                    {
	                    this.m_deviceSecret = Encoding.UTF8.GetString(this.DeviceSecretXml, 0, this.DeviceSecretXml.Length);
                    }
                }
                return this.m_deviceSecret;
            }
            set
            {
                this.m_deviceSecret = value;
                try
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (this.PlainTextSecret)
                        {
	                        this.DeviceSecretXml = Encoding.UTF8.GetBytes(value);
                        }
                        else
                        {
                            var cryptoService = ApplicationContext.Current.GetService<ISymmetricCryptographicProvider>();
                            if (cryptoService != null)
                            {
                                if (ApplicationContext.Current.GetCurrentContextSecurityKey() != null)
                                {
                                    var iv = cryptoService.GenerateIV();
                                    var res = cryptoService.Encrypt(Encoding.UTF8.GetBytes(value), ApplicationContext.Current.GetCurrentContextSecurityKey(), iv);
                                    var b = new byte[iv.Length + res.Length];
                                    Array.Copy(iv, b, iv.Length);
                                    Array.Copy(res, 0, b, iv.Length, res.Length);
                                    this.DeviceSecretXml = b;
                                }
                                else
                                {
	                                this.DeviceSecretXml = Encoding.UTF8.GetBytes(value);
                                }
                            }
                            else
                            {
	                            this.DeviceSecretXml = Encoding.UTF8.GetBytes(value);
                            }
                        }
                    }
                    else
                    {
	                    this.DeviceSecretXml = null;
                    }
                }
                catch {
                    this.PlainTextSecret = true;
                    this.DeviceSecretXml = Encoding.UTF8.GetBytes(value);

                }
            }
        }


	    /// <summary>
        /// Gets the real/domain to which the application is currently joined
        /// </summary>
        [XmlElement("domain")][JsonProperty("domain")]
        public string Domain
        {
            get;
            set;
        }


	    /// <summary>
        /// Restrict login to only listed facilities
        /// </summary>
        [XmlElement("restrictLogin")][JsonProperty("restrictLogin")]
        public bool RestrictLoginToFacilityUsers { get; set; }

	    /// <summary>
        /// When true, only allow login from this facility
        /// </summary>
        [XmlElement("facility")][JsonProperty("facility")]
        public List<Guid> Facilities { get; set; }

	    /// <summary>
        /// Gets or sets the hasher (for JSON view model only)
        /// </summary>
        [XmlIgnore][JsonProperty("hasher")]
        public string Hasher { get; set; }

	    /// <summary>
        /// When true, only allow login from this facility
        /// </summary>
        [XmlElement("owner")][JsonProperty("owner")]
        public List<Guid> Owners { get; set; }

	    /// <summary>
        /// Local session length
        /// </summary>
        [XmlElement("localSessionLength")][JsonProperty("localSessionLength")]
        public string MaxLocalSessionXml
        {
            get => this.MaxLocalSession.ToString();
            set => this.MaxLocalSession = TimeSpan.Parse(!string.IsNullOrEmpty(value) ? value : "00:30:00");
	    }

	    /// <summary>
        /// Maximum invalid logins
        /// </summary>
        [XmlElement("maxInvalidLogins")][JsonProperty("maxInvalidLogins")]
        public int? MaxInvalidLogins { get; set; }

	    /// <summary>
        /// Local session
        /// </summary>
        [XmlIgnore][JsonIgnore]
        public TimeSpan MaxLocalSession { get; set; }


	    // Don't dislose it
	    public bool ShouldSerializeApplicationSecret => false;

	    /// <summary>
        /// Signature configuration
        /// </summary>
        [XmlArray("signingKeys")][XmlArrayItem("add")]
        [JsonProperty("signingKeys")]
        public List<SecuritySignatureConfiguration> SigningKeys { get; set; }

        /// <summary>
        /// Gets or sets the password validation regex
        /// </summary>
        [XmlElement("passwordRegex"), JsonProperty("passwordRegex")]
        public string PasswordRegex { get; set; }


        /// <summary>
        /// Returns a copy of this object with sensitive information removed
        /// </summary>
        public SecurityConfigurationSection RemoveSensitiveInformation()
        {
            return new SecurityConfigurationSection
            {
                ApplicationSecret = null,
                ApplicationSecretXml = null,
                AuditRetention = this.AuditRetention,
                DeviceName = this.DeviceName,
                DeviceCertificate = null,
                DeviceSecret = null,
                Domain = this.Domain,
                DomainAuthentication = this.DomainAuthentication,
                Facilities = this.Facilities?.ToList(),
                Hasher = this.Hasher,
                MaxInvalidLogins = this.MaxInvalidLogins,
                MaxLocalSession = this.MaxLocalSession,
                Owners = this.Owners?.ToList(),
                PlainTextSecret = false,
                RestrictLoginToFacilityUsers = this.RestrictLoginToFacilityUsers,
                TokenAlgorithms = this.TokenAlgorithms?.ToList(),
                TokenType = this.TokenType,
                SigningKeys = this.SigningKeys?.Select(o => new SecuritySignatureConfiguration
                {
                    Algorithm = o.Algorithm,
                    FindType = o.FindType,
                    FindTypeSpecified = o.FindTypeSpecified,
                    FindValue = o.FindValue,
                    HmacSecret = null,
                    IssuerName = o.IssuerName,
                    KeyName = o.KeyName,
                    Secret = null,
                    StoreLocation = o.StoreLocation,
                    StoreLocationSpecified = o.StoreLocationSpecified,
                    StoreName = o.StoreName,
                    StoreNameSpecified = o.StoreNameSpecified
                }).ToList()
            };
        }
    }

}

