using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Services.Model;
using SanteDB.Rest.Common.Fault;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Remove resource checkout service
    /// </summary>
    public class RemoteResourceCheckoutService : IResourceCheckoutService
    {
        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteResourceCheckoutService));

        /// <summary>
        /// Service name
        /// </summary>
        public string ServiceName => "Upstream Checkout Manager";

        /// <summary>
        /// Gets the rest client
        /// </summary>
        /// <returns></returns>
        private IRestClient GetRestClient()
        {
            var retVal = ApplicationContext.Current.GetRestClient("hdsi");
            retVal.Accept = "application/xml";
            return retVal;
        }

        /// <summary>
        /// Checkout the specified resource
        /// </summary>
        public bool Checkout<T>(Guid key)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    client.Invoke<Object, Object>("CHECKOUT", $"{typeof(T).GetSerializationName()}/{key}", client.Accept, null);
                    return true;
                }
            }
            catch (RestClientException<RestServiceFault> e)
            {
                if (e.Response is HttpWebResponse hre && hre.StatusCode == (HttpStatusCode)423)
                    throw new ObjectLockedException(e.Result.Data[0].ToString());
                else
                    throw;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error checking out object {0} - {1}", key, e);
                throw;
            }
        }

        /// <summary>
        /// Checkin the specified object
        /// </summary>
        public bool Checkin<T>(Guid key)
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    client.Invoke<Object, Object>("CHECKIN", $"{typeof(T).GetSerializationName()}/{key}", client.Accept, null);
                    return true;
                }
            }
            catch (RestClientException<RestServiceFault> e)
            {
                if (e.Response is HttpWebResponse hre && hre.StatusCode == (HttpStatusCode)423)
                    throw new ObjectLockedException(e.Result.Data[0].ToString());
                else
                    throw;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error checking in object {0} - {1}", key, e);
                throw;
            }
        }
    }
}