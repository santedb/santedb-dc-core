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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SanteDB.DisconnectedClient.SQLite.Connection
{
    /// <summary>
    /// SQLite connection pool
    /// </summary>
    public class SQLiteConnectionPool : IList<LockableSQLiteConnection>, IDisposable
    {

        // The pool of connections
        private List<LockableSQLiteConnection> m_pool = new List<LockableSQLiteConnection>();

        // Lock
        private object m_lockObject = new object();

        /// <summary>
        /// Gets the specified pool object
        /// </summary>
        public LockableSQLiteConnection this[int index]
        {
            get => this.m_pool[index];
            set => this.m_pool[index] = value;
        }

        /// <summary>
        /// Get the count
        /// </summary>
        public int Count => this.m_pool.Count;

        /// <summary>
        /// Readonly 
        /// </summary>
        public bool IsReadOnly
        {
            get; private set;
        }


        /// <summary>
        /// Add an item
        /// </summary>
        public void Add(LockableSQLiteConnection item)
        {
            lock (this.m_lockObject)
                this.m_pool.Add(item);
        }

        /// <summary>
        /// Clear the specified object
        /// </summary>
        public void Clear()
        {
            lock (this.m_lockObject)
                this.m_pool.Clear();
        }

        /// <summary>
        /// True if the object is contained
        /// </summary>
        public bool Contains(LockableSQLiteConnection item)
        {
            return this.m_pool.Contains(item);
        }

        /// <summary>
        /// Copy to another
        /// </summary>
        public void CopyTo(LockableSQLiteConnection[] array, int arrayIndex)
        {
            this.m_pool.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Close all connection
        /// </summary>
        public void CloseAll()
        {
            lock (this.m_lockObject)
                foreach (var itm in this.m_pool.ToArray())
                {
                    itm.Dispose();
                    this.m_pool.Remove(itm);
                }
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        public void Dispose()
        {
            this.CloseAll();
        }

        /// <summary>
        /// Get enumerator
        /// </summary>
        public IEnumerator<LockableSQLiteConnection> GetEnumerator()
        {
            return this.m_pool.GetEnumerator();
        }

        /// <summary>
        /// Get the index of item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(LockableSQLiteConnection item)
        {
            return this.m_pool.IndexOf(item);
        }

        /// <summary>
        /// Insert the object
        /// </summary>
        public void Insert(int index, LockableSQLiteConnection item)
        {
            lock (this.m_lockObject)
                this.m_pool.Insert(index, item);
        }

        /// <summary>
        /// Remove item
        /// </summary>
        public bool Remove(LockableSQLiteConnection item)
        {
            lock (this.m_lockObject)
                return this.m_pool.Remove(item);
        }

        /// <summary>
        /// Remove att
        /// </summary>
        public void RemoveAt(int index)
        {
            lock (this.m_lockObject)
                this.m_pool.RemoveAt(index);
        }

        /// <summary>
        /// Get the enumerator
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.m_pool.GetEnumerator();
        }

        /// <summary>
        /// Locks the object
        /// </summary>
        public IDisposable Lock()
        {
            return new ConnectionPoolLockBox(this);
        }

        /// <summary>
        /// Connection pool lock box
        /// </summary>
        private class ConnectionPoolLockBox : IDisposable
        {

            /// <summary>
            /// Connection pool
            /// </summary>
            private SQLiteConnectionPool m_pool;

            /// <summary>
            /// Create a new connection pool lock
            /// </summary>
            public ConnectionPoolLockBox(SQLiteConnectionPool pool)
            {
                this.m_pool = pool;
                Monitor.Enter(this.m_pool.m_lockObject);
            }

            /// <summary>
            /// Dispose
            /// </summary>
            public void Dispose()
            {
                Monitor.Exit(this.m_pool.m_lockObject);
            }
        }

        /// <summary>
        /// Get a connection this already owns
        /// </summary>
        /// <returns></returns>
        public LockableSQLiteConnection GetEntered()
        {
            return this.m_pool.Find(o => o.IsEntered && !o.IsDisposed);
        }

        /// <summary>
        /// Get the free object
        /// </summary>
        public LockableSQLiteConnection GetFree()
        {
            var conn = this.m_pool.Find(o => o.LockCount == 0 && !o.IsDisposed);
            return conn;
        }

        /// <summary>
        /// Get a writable connection
        /// </summary>
        public LockableSQLiteConnection GetWritable()
        {
            return this.m_pool.Find(o => !o.IsReadonly && !o.IsDisposed);
        }

        /// <summary>
        /// Add unique value
        /// </summary>
        /// <param name="retVal"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public LockableSQLiteConnection AddUnique(LockableSQLiteConnection item, Predicate<LockableSQLiteConnection> predicate)
        {
            lock (this.m_lockObject)
            {
                var existing = this.m_pool.Find(predicate);
                if (existing == null || existing.IsDisposed)
                {
                    this.m_pool.Add(item);
                    return item;
                }
                else
                {
                    item.Dispose();
                    return existing;
                }

            }
        }

    }
}
