/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 */
using Newtonsoft.Json;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Upstream endpoint metadat utility
    /// </summary>
    public class UpstreamEndpointMetadataUtil : UpstreamServiceBase
    {

        private class UpstreamServiceCapability
        {
            private readonly ResourceCapabilityType[] CAN_WRITE = new[]
            {
                ResourceCapabilityType.Create,
                ResourceCapabilityType.CreateOrUpdate,
                ResourceCapabilityType.Update,
                ResourceCapabilityType.Delete
            };
            private readonly ResourceCapabilityType[] CAN_READ = new[]
            {
                ResourceCapabilityType.Get,
                ResourceCapabilityType.Search
            };

            /// <summary>
            /// This ctor supports serialization and should not be used to instantiate a new instance. Use <see cref="UpstreamServiceCapability.UpstreamServiceCapability(ServiceEndpointType, ServiceResourceOptions)"/> instead.
            /// </summary>
            public UpstreamServiceCapability()
            {

            }

            public UpstreamServiceCapability(ServiceEndpointType serviceEndpoint, ServiceResourceOptions serviceOption)
            {

                this.ServiceEndpoint = serviceEndpoint;
                this.Resource = serviceOption.ResourceType;
                if(this.Resource == null)
                {
                    this.Resource = new ModelSerializationBinder().BindToType(null, serviceOption.ResourceName);
                }
                this.CanWrite = CAN_WRITE.All(c => serviceOption.Capabilities.Any(s => s.Capability == c));
                this.CanRead = CAN_READ.All(c => serviceOption.Capabilities.Any(s => s.Capability == c));
            }

            /// <summary>
            /// Get the service endpoint
            /// </summary>
            public ServiceEndpointType ServiceEndpoint { get; set; }

            /// <summary>
            /// The resource name
            /// </summary>
            public Type Resource { get; set; }

            /// <summary>
            /// True if can read
            /// </summary>
            public bool CanRead { get; set; }

            /// <summary>
            /// True if can write
            /// </summary>
            public bool CanWrite { get; set; }

        }
        private static UpstreamEndpointMetadataUtil s_current = null;
        private static readonly object s_lock = new object();
        private static readonly Newtonsoft.Json.JsonSerializerSettings s_SerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = Newtonsoft.Json.TypeNameAssemblyFormatHandling.Full,
            Formatting = Newtonsoft.Json.Formatting.None,
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            MaxDepth = 12
        };

        private IDictionary<String, UpstreamServiceCapability> m_serviceEndpoints;
        private readonly object m_lock = new object();

        private readonly string m_OptionsCacheLocation;

        /// <summary>
        /// Creates a new instance of the metadata utility
        /// </summary>
        private UpstreamEndpointMetadataUtil(IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            var cachefolder = System.IO.Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory") as string, "cache");
            Directory.CreateDirectory(cachefolder);
            m_OptionsCacheLocation = System.IO.Path.Combine(cachefolder, "options.cache.json");
        }

        /// <summary>
        /// Get the singleton instance
        /// </summary>
        public static UpstreamEndpointMetadataUtil Current
        {
            get
            {
                if (s_current == null)
                {
                    lock (s_lock)
                    {
                        if (s_current == null)
                        {
                            var sp = ApplicationServiceContext.Current.GetService<IServiceProvider>();
                            s_current = new UpstreamEndpointMetadataUtil(sp.GetService<IRestClientFactory>(),
                                sp.GetService<IUpstreamManagementService>(),
                                sp.GetService<IUpstreamAvailabilityProvider>(),
                                sp.GetService<IUpstreamIntegrationService>());
                        }
                    }
                }
                return s_current;
            }
        }

        /// <summary>
        /// Get all supported resource types on the specified <paramref name="serviceEndpoint"/>
        /// </summary>
        public IEnumerable<String> GetSupportedResources(ServiceEndpointType serviceEndpoint) => this.GetServiceEndpoints().Where(o => o.Value.ServiceEndpoint == serviceEndpoint).Select(o => o.Key);

        /// <summary>
        /// Get resource which can be read
        /// </summary>
        public IEnumerable<Type> GetReadResources(ServiceEndpointType serviceEndpoint) => this.GetServiceEndpoints().Where(o => o.Value.ServiceEndpoint == serviceEndpoint).Select(o => o.Value.Resource);

        /// <summary>
        /// Get resources which can be written
        /// </summary>
        public IEnumerable<Type> GetWriteResources(ServiceEndpointType serviceEndpoint) => this.GetServiceEndpoints().Where(o => o.Value.ServiceEndpoint == serviceEndpoint).Select(o => o.Value.Resource);

        /// <summary>
        /// True if <paramref name="resourceType"/> can be read from a service
        /// </summary>
        public bool CanRead(Type resourceType) => this.GetServiceEndpoints().Any(o => o.Value.Resource == resourceType && o.Value.CanRead);

        /// <summary>
        /// True if <paramref name="resourceType"/> can be written to a service
        /// </summary>
        public bool CanWrite(Type resourceType) => this.GetServiceEndpoints().Any(o => o.Value.Resource == resourceType && o.Value.CanWrite);

        /// <summary>
        /// Get service endpoint
        /// </summary>
        public ServiceEndpointType GetServiceEndpoint<T>() => this.GetServiceEndpoint(typeof(T));

        /// <summary>
        /// Get the service endpoint which services <paramref name="t"/>
        /// </summary>
        public ServiceEndpointType GetServiceEndpoint(Type t)
        {
            return this.GetServiceEndpoints().TryGetValue(t.GetResourceName(), out var retVal) ? retVal.ServiceEndpoint : ServiceEndpointType.Other;
        }

        private IDictionary<String, UpstreamServiceCapability> GetServiceEndpoints()
        {
            if (this.m_serviceEndpoints == null)
            {
                lock (this.m_lock)
                {
                    if (this.m_serviceEndpoints == null)
                    {
                        GetServiceEndpointsInternal();
                    }
                }
            }
            return this.m_serviceEndpoints;
        }

        private void GetServiceEndpointsInternal()
        {
            try
            {
                // Attempt to read from cache if it exists and is not stale
                if (!File.Exists(this.m_OptionsCacheLocation) || DateTime.Now.Subtract(File.GetLastWriteTime(this.m_OptionsCacheLocation)).TotalHours > 24 || !this.TryGetServiceEndpointsFromCacheInternal())
                {
                    using (var amiClient = this.CreateRestClient(ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                    {
                        var options = amiClient.Options<ServiceOptions>("/");
                        this.m_serviceEndpoints = options.Endpoints.SelectMany(e =>
                        {
                            try
                            {
                                using (var client = this.CreateRestClient(e.ServiceType, AuthenticationContext.Current.Principal))
                                {
                                    return client
                                        .Options<ServiceOptions>("/")
                                        .Resources
                                        .Select(o => new KeyValuePair<String, UpstreamServiceCapability>(o.ResourceName, new UpstreamServiceCapability(e.ServiceType, o)));
                                }
                            }
                            catch
                            {
                                return new KeyValuePair<String, UpstreamServiceCapability>[0];
                            }
                        }).ToDictionaryIgnoringDuplicates(o => o.Key, o => o.Value);

                        if (null != this.m_serviceEndpoints && this.m_serviceEndpoints.Count > 0)
                            WriteServiceEndpointsToCacheInternal();
                    }
                }
            }
            catch
            {
                if (!TryGetServiceEndpointsFromCacheInternal()) //Try to read from the cache, otherwise throw up that we cannot get the endpoint data.
                    throw;
            }
        }

        /// <summary>
        /// Writes the current service endpoints to the cache file. Should only be called if <see cref="m_lock"/> is held.
        /// </summary>
        private void WriteServiceEndpointsToCacheInternal()
        {
            using (var fs = new FileStream(m_OptionsCacheLocation, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var sw = new StreamWriter(fs))
                {
                    using (var jw = new JsonTextWriter(sw))
                    {
                        var serializer = JsonSerializer.Create(s_SerializerSettings);

                        serializer.Serialize(jw, this.m_serviceEndpoints);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the service endpoints from the cache file. Should only be called if <see cref="m_lock"/> is held.
        /// </summary>
        /// <returns>True if the read succeeded, false otherwise.</returns>
        private bool TryGetServiceEndpointsFromCacheInternal()
        {
            if (!File.Exists(m_OptionsCacheLocation))
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(m_OptionsCacheLocation, FileMode.Open, FileAccess.Read))
                {
                    using (var sr = new StreamReader(fs))
                    {
                        using (var jr = new JsonTextReader(sr))
                        {
                            var serializer = JsonSerializer.Create(s_SerializerSettings);

                            var deserializedobject = serializer.Deserialize<Dictionary<string, UpstreamServiceCapability>>(jr);

                            if (null != deserializedobject)
                            {
                                m_serviceEndpoints = deserializedobject;
                                return true;
                            }
                            else
                                return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
