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
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Impl
{
    /// <summary>
    /// Memory query persistence service
    /// </summary>
    public class MemoryQueryPersistenceService : IQueryPersistenceService
    {

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(MemoryQueryPersistenceService));

        // Memory cache of queries
        private Dictionary<Guid, MemoryQueryInfo> m_queryCache = new Dictionary<Guid, MemoryQueryInfo>(10);

        // Sync object
        private Object m_syncObject = new object();

        /// <summary>
        /// Memory based query information
        /// </summary>
        public class MemoryQueryInfo
        {
            /// <summary>
            /// Total results
            /// </summary>
            public int TotalResults { get; set; }

            /// <summary>
            /// Results in the result set
            /// </summary>
            public List<Guid> Results { get; set; }

            /// <summary>
            /// The query tag
            /// </summary>
            public object QueryTag { get; set; }

        }

        /// <summary>
        /// Gets the specified query results
        /// </summary>
        public IEnumerable<Guid> GetQueryResults(Guid queryId, int offset, int count)
        {
            MemoryQueryInfo retVal = null;
            if (this.m_queryCache.TryGetValue(queryId, out retVal))
                return retVal.Results.Skip(offset).Take(count);
            return null;
        }

        /// <summary>
        /// Get the query tag
        /// </summary>
        public object GetQueryTag(Guid queryId)
        {
            MemoryQueryInfo retVal = null;
            if (this.m_queryCache.TryGetValue(queryId, out retVal))
                return retVal.QueryTag;
            return null;
        }

        /// <summary>
        /// Return whether the query is registered
        /// </summary>
        public bool IsRegistered(Guid queryId)
        {
            return this.m_queryCache.ContainsKey(queryId);
        }

        /// <summary>
        /// Get the total results
        /// </summary>
        public long QueryResultTotalQuantity(Guid queryId)
        {
            MemoryQueryInfo retVal = null;
            if (this.m_queryCache.TryGetValue(queryId, out retVal))
                return retVal.TotalResults;
            return 0;
        }

        /// <summary>
        /// Register a query
        /// </summary>
        public bool RegisterQuerySet(Guid queryId, IEnumerable<Guid> results, object tag, int totalResults)
        {
            MemoryQueryInfo retVal = null;
            if (this.m_queryCache.TryGetValue(queryId, out retVal))
            {
                this.m_tracer.TraceVerbose("Updating query {0} ({1} results)", queryId, results.Count());
                retVal.Results = results.ToList();
                retVal.QueryTag = tag;
                retVal.TotalResults = totalResults;
            }
            else
                lock (this.m_syncObject)
                {
                    this.m_tracer.TraceVerbose("Registering query {0} ({1} results)", queryId, results.Count());

                    this.m_queryCache.Add(queryId, new MemoryQueryInfo()
                    {
                        QueryTag = tag,
                        Results = results.ToList(),
                        TotalResults = totalResults
                    });
                }
            return true;
        }

        /// <summary>
        /// Add results to an existing query 
        /// </summary>
        /// <param name="queryId">The identifier of the query to add to</param>
        /// <param name="results">The results to add</param>
        public void AddResults(Guid queryId, IEnumerable<Guid> results)
        {
            MemoryQueryInfo query = null;
            if (this.m_queryCache.TryGetValue(queryId, out query))
                query.Results.AddRange(results);
        }
        
        /// <summary>
        /// Find the query id by the query tag
        /// </summary>
        public Guid FindQueryId(object queryTag)
        {
            return this.m_queryCache.FirstOrDefault(o => o.Value.QueryTag == queryTag).Key;
        }

        /// <summary>
        /// Set the query tag
        /// </summary>
        public void SetQueryTag(Guid queryId, object value)
        {
            MemoryQueryInfo query = null;
            if (this.m_queryCache.TryGetValue(queryId, out query))
                query.QueryTag = value;
        }
    }
}
