/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Fault;
using System;
using System.Security.Principal;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A <see cref="IResourceCheckoutService"/> which uses the upstream
    /// </summary>
    public class UpstreamResourceCheckoutService : UpstreamServiceBase, IResourceCheckoutService
    {
        private readonly IDataCachingService m_dataCachingService;

        /// <summary>
        /// DI Constructor
        /// </summary>
        public UpstreamResourceCheckoutService(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IDataCachingService dataCachingService, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_dataCachingService = dataCachingService;
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Resource Checkout";

        /// <inheritdoc/>
        public bool Checkin<T>(Guid key)
        {
            try
            {
                using (var client = base.CreateRestClient(UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint<T>(), AuthenticationContext.Current.Principal))
                {
                    client.Invoke<Object, Object>("CHECKIN", $"{typeof(T).GetSerializationName()}/{key}", null);
                    this.m_dataCachingService.Remove(key);
                    return true;
                }
            }
            catch (RestClientException<RestServiceFault> ex) when (ex.Result.Type == nameof(ObjectLockedException))
            {
                throw new Core.Exceptions.ObjectLockedException(ex.Result.Data[0]);
            }
        }

        /// <inheritdoc/>
        public bool Checkout<T>(Guid key)
        {
            try
            {
                using (var client = base.CreateRestClient(UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint<T>(), AuthenticationContext.Current.Principal))
                {
                    client.Invoke<Object, Object>("CHECKOUT", $"{typeof(T).GetSerializationName()}/{key}", null);
                    this.m_dataCachingService.Remove(key);
                    return true;
                }
            }
            catch (RestClientException<RestServiceFault> ex) when (ex.Result.Type == nameof(ObjectLockedException))
            {
                throw new Core.Exceptions.ObjectLockedException(ex.Result.Data[0]);
            }
            catch (RestClientException<Object> ex) when (ex.Result is RestServiceFault rfe && ex.HttpStatus == (System.Net.HttpStatusCode)423)
            {
                throw new ObjectLockedException(rfe.Data[0]);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <inheritdoc/>
        public bool IsCheckedout<T>(Guid key, out IIdentity currentOwner)
        {
            try
            {
                using (var client = base.CreateRestClient(UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint<T>(), AuthenticationContext.Current.Principal))
                {
                    var headers = client.Head($"{typeof(T).GetSerializationName()}/{key}", null);
                    if (headers.TryGetValue(ExtendedHttpHeaderNames.CheckoutStatusHeader, out var owner))
                    {
                        currentOwner = new GenericIdentity(owner);
                        return true;
                    }
                }
                currentOwner = null;
                return false;
            }
            catch (RestClientException<RestServiceFault> ex) when (ex.Result.Type == nameof(ObjectLockedException))
            {
                throw new Core.Exceptions.ObjectLockedException(ex.Result.Data[0]);
            }
        }


    }
}
