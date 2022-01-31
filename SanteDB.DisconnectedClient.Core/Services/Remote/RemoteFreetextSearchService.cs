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
 * Date: 2021-8-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// Represents a freetext service which passes queries to an upstream server
    /// </summary>
    public class RemoteFreetextSearchService : IFreetextSearchService
    {
        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Remote Freetext Search Service";

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteBiService));

        /// <summary>
        /// Gets the rest client
        /// </summary>
        /// <returns></returns>
        public IRestClient GetRestClient()
        {
            var retVal = ApplicationContext.Current.GetRestClient("hdsi");
            return retVal;
        }

        /// <summary>
        /// Search the upstream service for the specified object in a freetext manner
        /// </summary>
        public IEnumerable<TEntity> Search<TEntity>(string[] term, Guid queryId, int offset, int? count, out int totalResults, ModelSort<TEntity>[] orderBy) where TEntity : IdentifiedData, new()
        {
            try
            {
                using (var client = this.GetRestClient())
                {
                    var parms = term.Select(o => new KeyValuePair<String, Object>("_any", o)).ToList();
                    parms.Add(new KeyValuePair<string, object>("_offset", offset));
                    parms.Add(new KeyValuePair<string, object>("_count", count ?? 100));
                    var result = client.Get<Bundle>(typeof(TEntity).GetSerializationName(), parms.ToArray());
                    totalResults = result.TotalResults;
                    return result.Item.OfType<TEntity>();
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error performing freetext search service: {0}", e);
                throw new Exception($"Error performing freetext search {String.Join(",", term)}", e);
            }
        }
    }
}