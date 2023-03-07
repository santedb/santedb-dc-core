using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Fault;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A <see cref="IResourceCheckoutService"/> which uses the upstream
    /// </summary>
    public class UpstreamResourceCheckoutService : UpstreamServiceBase, IResourceCheckoutService
    {
        /// <summary>
        /// DI Constructor
        /// </summary>
        public UpstreamResourceCheckoutService(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Resource Checkout";

        /// <inheritdoc/>
        public bool Checkin<T>(Guid key)
        {
            try
            {
                using(var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
                {
                    client.Invoke<Object, Object>("CHECKIN", $"{typeof(T).GetSerializationName()}/{key}", null);
                    return true;
                }
            }
            catch(RestClientException<RestServiceFault> ex) when (ex.Result.Type == nameof(ObjectLockedException))
            {
                throw new Core.Exceptions.ObjectLockedException(ex.Result.Data[0]);
            }
        }

        /// <inheritdoc/>
        public bool Checkout<T>(Guid key)
        {
            try
            {
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
                {
                    client.Invoke<Object, Object>("CHECKOUT", $"{typeof(T).GetSerializationName()}/{key}", null);
                    return true;
                }
            }
            catch (RestClientException<RestServiceFault> ex) when (ex.Result.Type == nameof(ObjectLockedException))
            {
                throw new Core.Exceptions.ObjectLockedException(ex.Result.Data[0]);
            }
            catch(RestClientException<Object> ex)  when (ex.Result is RestServiceFault rfe && ex.HttpStatus == (System.Net.HttpStatusCode)423)
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
                using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
                {
                    var headers = client.Head($"{typeof(T).GetSerializationName()}/{key}", null);
                    if(headers.TryGetValue(ExtendedHttpHeaderNames.CheckoutStatusHeader, out var owner))
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
