/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 * 
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
 * Date: 2017-9-1
 */
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Caching;
using SanteDB.DisconnectedClient.Core.Security;
using SanteDB.DisconnectedClient.Xamarin.Services.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Services.ServiceHandlers
{
    /// <summary>
    /// Concept service handlers for the IMSI
    /// </summary>
    public partial class HdsiService
    {
        /// <summary>
        /// Gets a list of acts.
        /// </summary>
        /// <returns>Returns a list of acts.</returns>
        [RestOperation(Method = "GET", UriPath = "/Concept", FaultProvider = nameof(HdsiFault))]
        [Demand(PolicyIdentifiers.ReadMetadata)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public IdentifiedData GetConcept()
        {
            var conceptRepositoryService = ApplicationContext.Current.GetService<IConceptRepositoryService>();
            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

            if (search.ContainsKey("_id"))
            {
                // Force load from DB
                ApplicationContext.Current.GetService<IDataCachingService>().Remove(Guid.Parse(search["_id"].FirstOrDefault()));
                var concept = conceptRepositoryService.GetConcept(Guid.Parse(search["_id"].FirstOrDefault()), Guid.Empty);
                return concept;
            }
            else
            {
                int totalResults = 0,
                       offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
                       count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

                var results = conceptRepositoryService.FindConcepts(QueryExpressionParser.BuildLinqExpression<Concept>(search, null, false), offset, count, out totalResults);

                // 
                return new Bundle
                {
                    Count = results.Count(),
                    Item = results.OfType<IdentifiedData>().ToList(),
                    Offset = 0,
                    TotalResults = totalResults
                };
            }
        }

        /// <summary>
        /// Gets a list of acts.
        /// </summary>
        /// <returns>Returns a list of acts.</returns>
        [RestOperation(Method = "GET", UriPath = "/ConceptSet", FaultProvider = nameof(HdsiFault))]
        [Demand(PolicyIdentifiers.ReadMetadata)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public IdentifiedData GetConceptSet()
        {
            var conceptRepositoryService = ApplicationContext.Current.GetService<IConceptRepositoryService>();
            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

            if (search.ContainsKey("_id"))
            {
                // Force load from DB
                ApplicationContext.Current.GetService<IDataCachingService>().Remove(Guid.Parse(search["_id"].FirstOrDefault()));
                var concept = conceptRepositoryService.GetConceptSet(Guid.Parse(search["_id"].FirstOrDefault()));
                return concept;
            }
            else
            {
                int totalResults = 0,
                       offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
                       count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

                var results = conceptRepositoryService.FindConceptSets(QueryExpressionParser.BuildLinqExpression<ConceptSet>(search, null, false), offset, count, out totalResults);

                // 
                return new Bundle
                {
                    Count = results.Count(),
                    Item = results.OfType<IdentifiedData>().ToList(),
                    Offset = 0,
                    TotalResults = totalResults
                };
            }
        }
    }
}
