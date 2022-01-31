/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * User: fyfej
 * Date: 2021-9-23
 */
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
                    client.Invoke<Object, Object>("CHECKOUT", $"{typeof(T).GetSerializationName()}/{key}", null);
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
                    client.Invoke<Object, Object>("CHECKIN", $"{typeof(T).GetSerializationName()}/{key}", null);
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