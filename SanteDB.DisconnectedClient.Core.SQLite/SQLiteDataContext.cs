/*
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.DisconnectedClient.SQLite.Query;
using SanteDB.Core.Model;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SQLite.Net.Interop;
using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace SanteDB.DisconnectedClient.SQLite
{
    /// <summary>
    /// Local data context
    /// </summary>
    public class SQLiteDataContext : IDisposable
    {

        /// <summary>
        /// Partial load mode
        /// </summary>
        public SQLiteDataContext(IPrincipal principal)
        {
            this.DelayLoadMode = LoadState.PartialLoad;
            this.Principal = principal;
        }

        // Prepared
        private Dictionary<String, IDbStatement> m_prepared = new Dictionary<string, IDbStatement>();

        // Transaction items
        private Dictionary<Guid, IdentifiedData> m_transactionItems = new Dictionary<Guid, IdentifiedData>();

        /// <summary>
        /// Cache commit
        /// </summary>
        private Dictionary<Guid, IdentifiedData> m_cacheCommit = new Dictionary<Guid, IdentifiedData>();

        // Data dictionary
        private Dictionary<String, Object> m_dataDictionary = new Dictionary<string, object>();

        /// <summary>
        /// Associations to be be forcably loaded
        /// </summary>
        public String[] LoadAssociations { get; set; }

        /// <summary>
        /// Local data context
        /// </summary>
        public SQLiteDataContext(LockableSQLiteConnection connection, IPrincipal principal)
        {
            this.Principal = principal;
            this.Connection = connection;
            this.m_cacheCommit = new Dictionary<Guid, IdentifiedData>();
        }

        /// <summary>
        /// Lock connection
        /// </summary>
        public IDisposable LockConnection()
        {
            return this.Connection.Lock();
        }

        /// <summary>
        /// Local data connection
        /// </summary>
        public LockableSQLiteConnection Connection { get; set; }

        /// <summary>
        /// Cache on commit
        /// </summary>
        public IEnumerable<IdentifiedData> CacheOnCommit
        {
            get
            {
                return this.m_cacheCommit.Values;
            }
        }

        /// <summary>
        /// Data dictionary
        /// </summary>
        public IDictionary<String, Object> Data { get { return this.m_dataDictionary; } }

        /// <summary>
        /// Add an item to the list of items which are being about to be committed
        /// </summary>
        internal void AddTransactedItem<TModel>(TModel data) where TModel : IdentifiedData
        {
            lock (this.m_transactionItems)
                if (!this.m_transactionItems.ContainsKey(data.Key.Value))
                    this.m_transactionItems.Add(data.Key.Value, data);
        }

        /// <summary>
        /// Add an item to the list of items which are being about to be committed
        /// </summary>
        internal IdentifiedData FindTransactedItem(Guid key)
        {
            IdentifiedData retVal = null;
            this.m_transactionItems.TryGetValue(key, out retVal);
            return retVal;
        }

        /// <summary>
        /// The data loading mode
        /// </summary>
        public SanteDB.Core.Model.LoadState DelayLoadMode { get; set; }

        /// <summary>
        /// Gets the principal associated with the context
        /// </summary>
        public IPrincipal Principal { get; }

        /// <summary>
        /// Add cache commit
        /// </summary>
        public void AddCacheCommit(IdentifiedData data)
        {
            if (data.Key.HasValue && !this.m_cacheCommit.ContainsKey(data.Key.Value) && data.Key.HasValue)
                this.m_cacheCommit.Add(data.Key.Value, data);
            else if (data.Key.HasValue)
                this.m_cacheCommit[data.Key.Value] = data;
        }

        /// <summary>
        /// Adds data in a safe way
        /// </summary>
        public void AddData(string key, object value)
        {
            lock (this.m_dataDictionary)
                if (!this.m_dataDictionary.ContainsKey(key))
                    this.m_dataDictionary.Add(key, value);
        }

        /// <summary>
        /// Try get cache item
        /// </summary>
        public IdentifiedData TryGetCacheItem(Guid key)
        {
            IdentifiedData retVal = null;
            this.m_cacheCommit.TryGetValue(key, out retVal);
            return retVal;
        }

        /// <summary>
        /// Get or create prepared statement
        /// </summary>
        internal IDbStatement GetOrCreatePrepared(string cmdText)
        {
            IDbStatement prepared = null;
            if (!this.m_prepared.TryGetValue(cmdText, out prepared))
            {
                prepared = this.Connection.Prepare(cmdText);
                lock (this.m_prepared)
                    if (!this.m_prepared.ContainsKey(cmdText))
                        this.m_prepared.Add(cmdText, prepared);
            }
            return prepared;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            foreach (var stmt in this.m_prepared.Values)
                stmt.Finalize();
        }

        /// <summary>
        /// Query
        /// </summary>
        public String GetQueryLiteral(SqlStatement query)
        {
            return query.ToString();
        }

        /// <summary>
        /// Try to get data from data array
        /// </summary>
        public Object TryGetData(String key)
        {
            Object data = null;
            this.Data.TryGetValue(key, out data);
            return data;
        }
    }
}
