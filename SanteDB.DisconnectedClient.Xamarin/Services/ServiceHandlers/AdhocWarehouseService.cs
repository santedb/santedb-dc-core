/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Data.Warehouse;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using SanteDB.DisconnectedClient.Xamarin.Services.Model;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
{
    /// <summary>
    /// Interacts with the ad-hoc data warehouse
    /// </summary>
    [RestService("/__zombo")]
    public class AdhocWarehouseService
    {

        /// <summary>
        /// Performs an ad-hoc query against the datawarehouse
        /// </summary>
        [RestOperation(Method = "GET", UriPath = "/adhocQuery", FaultProvider = nameof(AdhocWarehouseFaultProvider))]
        public object AdHocQuery()
        {
            var warehouseSvc = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();

            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            int totalResults = 0,
                   offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
                   count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

            if (!search.ContainsKey("martId")) throw new ArgumentNullException("Missing datamart identifier");
            else
            {
                var dataMart = warehouseSvc.GetDatamart(search["martId"][0]);
                search.Remove("martId");
                search.Remove("_");
                return warehouseSvc.AdhocQuery(dataMart.Id, search);
            }
        }

        /// <summary>
        /// Performs an ad-hoc query against the datawarehouse
        /// </summary>
        [RestOperation(Method = "GET", UriPath = "/storedQuery", FaultProvider = nameof(AdhocWarehouseFaultProvider))]
        public object StoredQuery()
        {
            var warehouseSvc = ApplicationContext.Current.GetService<IAdHocDatawarehouseService>();

            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            int totalResults = 0,
                   offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
                   count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

            if (!search.ContainsKey("martId")) throw new ArgumentNullException("Missing datamart identifier");
            else if (!search.ContainsKey("queryId")) throw new ArgumentNullException("Query identifier");
            else
            {
                var dataMart = warehouseSvc.GetDatamart(search["martId"][0]);
                var queryName = search["queryId"][0];
                search.Remove("martId");
                search.Remove("queryId");
                search.Remove("_");
                var results = warehouseSvc.StoredQuery(dataMart.Id, queryName, search, 0, -1, out totalResults);


                return results;
            }
        }

        /// <summary>
        /// Adhoc warehouse fault provider
        /// </summary>
        public ErrorResult AdhocWarehouseFaultProvider(Exception e)
        {
            return new ErrorResult()
            {
                Error = e.Message,
                ErrorDescription = e.InnerException?.Message,
                ErrorType = e.GetType().Name
            };
        }

    }
}
