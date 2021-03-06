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
using System.Xml.Serialization;
using Newtonsoft.Json;
using SanteDB.Core.Configuration;
using SanteDB.Core.Http;
using SanteDB.Core.Http.Description;

namespace SanteDB.DisconnectedClient.Configuration
{

    /// <summary>
    /// Service client configuration
    /// </summary>
    [XmlType(nameof(ServiceClientConfigurationSection), Namespace = "http://santedb.org/mobile/configuration")][JsonObject(nameof(ServiceClientConfigurationSection))]
    public class ServiceClientConfigurationSection : IConfigurationSection
    {
	    /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Configuration.ServiceClientConfigurationSection"/> class.
        /// </summary>
        public ServiceClientConfigurationSection()
        {
            this.Client = new List<ServiceClientDescriptionConfiguration>();
        }

	    /// <summary>
        /// Gets or sets the proxy address.
        /// </summary>
        /// <value>The proxy address.</value>
        [XmlElement("proxyAddress")][JsonProperty("proxyAddress")]
        public string ProxyAddress
        {
            get;
            set;
        }

	    /// <summary>
        /// Represents a service client
        /// </summary>
        /// <value>The client.</value>
        [XmlElement("client")][JsonProperty("client")]
        public List<ServiceClientDescriptionConfiguration> Client
        {
            get;
            set;
        }

	    /// <summary>
        /// The optimization
        /// </summary>
        [XmlIgnore][JsonProperty("optimize")]
        public OptimizationMethod Optimize { get; set; }


	    /// <summary>
        /// Gets or sets the rest client implementation
        /// </summary>
        /// <value>The type of the rest client.</value>
        [XmlIgnore][JsonIgnore]
        public Type RestClientType
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the type which is to be used for rest clients
        /// </summary>
        /// <value>The rest client type xml.</value>
        [XmlAttribute("clientType")][JsonIgnore]
        public string RestClientTypeXml
        {
            get => this.RestClientType?.AssemblyQualifiedName;
            set => this.RestClientType = Type.GetType(value);
	    }
    }

    /// <summary>
    /// A service client reprsent a single client to a service 
    /// </summary>
    [XmlType(nameof(ServiceClientDescriptionConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    public class ServiceClientDescriptionConfiguration : IRestClientDescription
    {
	    /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Configuration.ServiceClient"/> class.
        /// </summary>
        public ServiceClientDescriptionConfiguration()
        {
            this.Endpoint = new List<ServiceClientEndpoint>();
        }

	    /// <summary>
        /// The endpoints of the client
        /// </summary>
        /// <value>The endpoint.</value>
        [XmlElement("endpoint")]
        public List<ServiceClientEndpoint> Endpoint
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the binding for the service client.
        /// </summary>
        /// <value>The binding.</value>
        [XmlElement("binding")]
        public ServiceClientBinding Binding
        {
            get;
            set;
        }


	    /// <summary>
        /// Gets or sets the name of the service client
        /// </summary>
        /// <value>The name.</value>
        [XmlAttribute("name")]
        public string Name
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets the binding
        /// </summary>
        IRestClientBindingDescription IRestClientDescription.Binding => this.Binding;

	    /// <summary>
        /// Gets the endpoints
        /// </summary>
        List<IRestClientEndpointDescription> IRestClientDescription.Endpoint => this.Endpoint.OfType<IRestClientEndpointDescription>().ToList();

	    /// <summary>
        /// Gets or sets the trace
        /// </summary>
        [XmlElement("trace")]
        public bool Trace { get; set; }

	    /// <summary>
        /// Clone the object
        /// </summary>
        public ServiceClientDescriptionConfiguration Clone()
        {
            var retVal = this.MemberwiseClone() as ServiceClientDescriptionConfiguration;
            retVal.Endpoint = new List<ServiceClientEndpoint>(this.Endpoint.Select(o => new ServiceClientEndpoint
            {
                Address = o.Address,
                Timeout = o.Timeout
            }));
            return retVal;
        }
    }

    /// <summary>
    /// Service client binding
    /// </summary>
    [XmlType(nameof(ServiceClientBinding), Namespace = "http://santedb.org/mobile/configuration")]
    public class ServiceClientBinding : IRestClientBindingDescription
    {
	    /// <summary>
        /// Initializes a new instance of the <see cref="ServiceClientBinding"/> class.
        /// </summary>
        public ServiceClientBinding()
        {
            this.ContentTypeMapper = new DefaultContentTypeMapper();
        }

	    /// <summary>
        /// Gets or sets the type which dictates how a body maps to a 
        /// </summary>
        /// <value>The serialization binder type xml.</value>
        [XmlAttribute("contentTypeMapper")]
        public string ContentTypeMapperXml
        {
            get => this.ContentTypeMapper?.GetType().AssemblyQualifiedName;
            set => this.ContentTypeMapper = Activator.CreateInstance(Type.GetType(value)) as IContentTypeMapper;
	    }

	    /// <summary>
        /// Gets or sets the security configuration
        /// </summary>
        /// <value>The security.</value>
        [XmlElement("security")]
        public ServiceClientSecurity Security
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets a value indicating whether this <see cref="SanteDB.DisconnectedClient.Configuration.ServiceClientBinding"/>
        /// is optimized
        /// </summary>
        /// <value><c>true</c> if optimize; otherwise, <c>false</c>.</value>
        [XmlElement("optimize")]
        public bool Optimize
        {
            get; set;
        }

	    /// <summary>
        /// Gets or sets the optimization method
        /// </summary>
        [XmlElement("method")]
        public OptimizationMethod OptimizationMethod { get; set; }


	    /// <summary>
        /// Content type mapper
        /// </summary>
        /// <value>The content type mapper.</value>
        [XmlIgnore]
        public IContentTypeMapper ContentTypeMapper
        {
            get;
            set;
        }


	    /// <summary>
        /// Gets the security description
        /// </summary>
        IRestClientSecurityDescription IRestClientBindingDescription.Security => this.Security;
    }

    /// <summary>
    /// Optimization method
    /// </summary>
    [XmlType(nameof(OptimizationMethod), Namespace = "http://santedb.org/mobile/configuration")]
    public enum OptimizationMethod
    {
        [XmlEnum("off")]
        None = 0,
        [XmlEnum("df")]
        Deflate = 1,
        [XmlEnum("gz")]
        Gzip = 2,
        [XmlEnum("bz2")]
        Bzip2 = 3,
        [XmlEnum("7z")]
        Lzma = 4
    }

    /// <summary>
    /// Service client security configuration
    /// </summary>
    [XmlType(nameof(ServiceClientSecurity), Namespace = "http://santedb.org/mobile/configuration")]
    public class ServiceClientSecurity : IRestClientSecurityDescription
    {
	    /// <summary>
        /// Gets or sets the ICertificateValidator interface which should be called to validate 
        /// certificates 
        /// </summary>
        /// <value>The serialization binder type xml.</value>
        [XmlAttribute("certificateValidator")]
        public string CertificateValidatorXml
        {
            get => this.CertificateValidator?.GetType().AssemblyQualifiedName;
            set => this.CertificateValidator = Activator.CreateInstance(Type.GetType(value)) as ICertificateValidator;
	    }


	    /// <summary>
        /// Gets the thumbprint the device should use for authentication
        /// </summary>
        [XmlElement("certificate")]
        public ServiceCertificateConfiguration ClientCertificate
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the ICredentialProvider
        /// </summary>
        /// <value>The credential provider xml.</value>
        [XmlAttribute("credentialProvider")]
        public string CredentialProviderXml
        {
            get => this.CredentialProvider?.GetType().AssemblyQualifiedName;
            set => this.CredentialProvider = Activator.CreateInstance(Type.GetType(value)) as ICredentialProvider;
	    }

	    /// <summary>
        /// Gets or sets the authentication realm this client should verify
        /// </summary>
        /// <value>The auth realm.</value>
        [XmlAttribute("realm")]
        public string AuthRealm
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the certificate validator.
        /// </summary>
        /// <value>The certificate validator.</value>
        [XmlIgnore]
        public ICertificateValidator CertificateValidator
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets certificate find
        /// </summary>
        IRestClientCertificateDescription IRestClientSecurityDescription.ClientCertificate => this.ClientCertificate;

	    /// <summary>
        /// Gets or sets the credential provider.
        /// </summary>
        /// <value>The credential provider.</value>
        [XmlIgnore]
        public ICredentialProvider CredentialProvider
        {
            get;
            set;
        }

	    /// <summary>
        /// Security mode
        /// </summary>
        /// <value>The mode.</value>
        [XmlAttribute("mode")]
        public SecurityScheme Mode
        {
            get;
            set;
        }

	    /// <summary>
        /// Preemptive authentication
        /// </summary>
        [XmlElement("preAuth")]
        public bool PreemptiveAuthentication { get; set; }
    }

    /// <summary>
    /// Service certificate configuration
    /// </summary>
    [XmlType(nameof(ServiceCertificateConfiguration), Namespace = "http://santedb.org/mobile/configuration")]
    public class ServiceCertificateConfiguration : IRestClientCertificateDescription
    {
	    /// <summary>
        /// Gets or sets the type of the find.
        /// </summary>
        /// <value>The type of the find.</value>
        [XmlAttribute("x509FindType")]
        public string FindType
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the find value.
        /// </summary>
        /// <value>The find value.</value>
        [XmlAttribute("findValue")]
        public string FindValue
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the store location.
        /// </summary>
        /// <value>The store location.</value>
        [XmlAttribute("storeLocation")]
        public string StoreLocation
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the name of the store.
        /// </summary>
        /// <value>The name of the store.</value>
        [XmlAttribute("storeName")]
        public string StoreName
        {
            get;
            set;
        }
    }


    /// <summary>
    /// Represnts a single endpoint for use in the service client
    /// </summary>
    [XmlType(nameof(ServiceClientEndpoint), Namespace = "http://santedb.org/mobile/configuration")]
    public class ServiceClientEndpoint : IRestClientEndpointDescription
    {
	    /// <summary>
        /// Timeout of 4 sec
        /// </summary>
        public ServiceClientEndpoint()
        {
            this.Timeout = 30000;
        }

	    /// <summary>
        /// Gets or sets the service client endpoint's address
        /// </summary>
        /// <value>The address.</value>
        [XmlAttribute("address")]
        public string Address
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the timeout
        /// </summary>
        [XmlAttribute("timeout")]
        public int Timeout { get; set; }
    }


}

