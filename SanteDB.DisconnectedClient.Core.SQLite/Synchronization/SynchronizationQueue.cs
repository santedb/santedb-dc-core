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
using System;
using SanteDB.DisconnectedClient.SQLite.Synchronization.Model;
using System.Collections.Generic;
using System.Reflection;
using SQLite.Net;
using System.Threading.Tasks;
using SanteDB.DisconnectedClient.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using System.IO;
using System.Xml.Serialization;
using SanteDB.DisconnectedClient.Core.Services;
using System.Linq.Expressions;
using System.Linq;
using SanteDB.DisconnectedClient.SQLite.Connection;
using SanteDB.DisconnectedClient.Core.Synchronization;
using SanteDB.DisconnectedClient.Core;
using SanteDB.Core.Model.Query;

namespace SanteDB.DisconnectedClient.SQLite.Synchronization
{
	/// <summary>
	/// Synchronization queue helper.
	/// </summary>
	public static class SynchronizationQueue
	{
        
		/// <summary>
		/// Gets the current inbound queue
		/// </summary>
		/// <value>The inbound.</value>
		public static SynchronizationQueue<InboundQueueEntry> Inbound {
			get {
				return SynchronizationQueue<InboundQueueEntry>.Current;
			}
		}

		/// <summary>
		/// Gets the current outbound queue
		/// </summary>
		/// <value>The inbound.</value>
		public static SynchronizationQueue<OutboundQueueEntry> Outbound {
			get {
				return SynchronizationQueue<OutboundQueueEntry>.Current;
			}
		}

        /// <summary>
        /// Gets the current admin outbound queue
        /// </summary>
        /// <value>The inbound.</value>
        public static SynchronizationQueue<OutboundAdminQueueEntry> Admin
        {
            get
            {
                return SynchronizationQueue<OutboundAdminQueueEntry>.Current;
            }
        }

        /// <summary>
        /// Gets the current deadletter queue
        /// </summary>
        /// <value>The inbound.</value>
        public static SynchronizationQueue<DeadLetterQueueEntry> DeadLetter {
			get {
				return SynchronizationQueue<DeadLetterQueueEntry>.Current;
			}
		}

	}

	/// <summary>
	/// Represents a generic synchronization queue
	/// </summary>
	public class SynchronizationQueue<TQueueEntry> : ISynchronizationQueue
        where TQueueEntry : SynchronizationQueueEntry, new()
	{

		// Get the tracer
		private Tracer m_tracer = Tracer.GetTracer(typeof(SynchronizationQueue<TQueueEntry>));

		// Object sync
		private Object m_syncObject = new object ();

		// The queue instance
		private static SynchronizationQueue<TQueueEntry> s_instance;

        /// <summary>
        /// Fired when the data is about to be enqueued
        /// </summary>
        public event EventHandler<DataPersistencePreEventArgs<TQueueEntry>> Enqueuing;
        /// <summary>
        /// Fired after the data has been enqueued
        /// </summary>
        public event EventHandler<DataPersistenceEventArgs<TQueueEntry>> Enqueued;

		/// <summary>
		/// Singleton
		/// </summary>
		/// <value>The current.</value>
		public static SynchronizationQueue<TQueueEntry> Current
		{
			get {
				if (s_instance == null)
					s_instance = new SynchronizationQueue<TQueueEntry> ();
				return s_instance;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.SQLite.Synchronization.SynchronizationQueue`1"/> class.
		/// </summary>
		private SynchronizationQueue ()
		{
		}

		/// <summary>
		/// Create a connection
		/// </summary>
		/// <returns>The connection.</returns>
		private LockableSQLiteConnection CreateConnection()
		{
            return SQLiteConnectionManager.Current.GetConnection(ApplicationContext.Current.Configuration.GetConnectionString (
				ApplicationContext.Current.Configuration.GetSection<DataConfigurationSection> ().MessageQueueConnectionStringName
			).Value);
		}



		/// <summary>
		/// Enqueue the specified entry data
		/// </summary>
		public TQueueEntry Enqueue(IdentifiedData data, SynchronizationOperationType operation)
		{

            // Serialize object
            TQueueEntry queueEntry = new TQueueEntry() {
                Data = ApplicationContext.Current.GetService<IQueueFileProvider>().SaveQueueData(data),
				CreationTime = DateTime.Now,
				Operation = operation,
				Type = data.GetType ().AssemblyQualifiedName
			};

			// Enqueue the object
			return this.EnqueueRaw (queueEntry);
		}

		/// <summary>
		/// Enqueue the specified entry
		/// </summary>
		/// <param name="entry">Entry.</param>
		public TQueueEntry EnqueueRaw(TQueueEntry entry)
		{
            // Fire pre-event args
            var preEventArgs = new DataPersistencePreEventArgs<TQueueEntry>(entry);
            this.Enqueuing?.Invoke(this, preEventArgs);
            if (preEventArgs.Cancel)
            {
                this.m_tracer.TraceInfo("Pre-event handler has cancelled the action");
                return preEventArgs.Data;
            }

            var conn = this.CreateConnection();
            using(conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                    {
                        conn.BeginTransaction();
                        // Persist the queue entry
                        this.m_tracer.TraceInfo("Enqueue {0} successful. Queue item {1}", entry, conn.Insert(entry));
                        conn.Commit();
                    }

                    var postEventArgs = new DataPersistenceEventArgs<TQueueEntry>(entry);
                    this.Enqueued?.Invoke(this, postEventArgs);
                    return postEventArgs.Data;

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error enqueueing object {0} : {1}", entry, e);
                    conn.Rollback();
                    throw;
                }

            }
		}

		/// <summary>
		/// Deserialize the object
		/// </summary>
		public IdentifiedData DeserializeObject(TQueueEntry entry)
		{
            return ApplicationContext.Current.GetService<IQueueFileProvider>().GetQueueData(entry.Data, Type.GetType(entry.Type));
		}

		/// <summary>
		/// Peeks at the next item in the stack
		/// </summary>
		public IdentifiedData Peek()
		{
			return this.DeserializeObject (this.PeekRaw ());
		}

		/// <summary>
		/// Pop an item from the queue.
		/// </summary>
		public IdentifiedData Dequeue() {
			return this.DeserializeObject (this.DequeueRaw());
		}

		/// <summary>
		/// Pops the item off the stack
		/// </summary>
		public TQueueEntry DequeueRaw()
		{
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    // Delete the object
                    using (conn.Lock())
                    {
                        // Fetch the object
                        var queueItem = conn.Table<TQueueEntry>().Where(o => o.Id >= 0).OrderBy(i => i.Id).FirstOrDefault();
                        if (queueItem != null)
                        {
                            ApplicationContext.Current.GetService<IQueueFileProvider>().RemoveQueueData(queueItem.Data);
                            conn.Delete(queueItem);
                        }
                        return queueItem;
                    }

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error popping object off queue : {0}", e);
                    throw;
                }
            }
		}

        /// <summary>
        /// Provides a mechanism for a queue entry to be udpated
        /// </summary>
        internal void UpdateRaw(TQueueEntry entry)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                        conn.Update(entry);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error updating object: {0}", e);
                }
            }
        }

        /// <summary>
        /// Peeks a raw row entry from the database.
        /// </summary>
        /// <returns>The raw.</returns>
        public TQueueEntry PeekRaw(int skip = 0){
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                        return conn.Table<TQueueEntry>().OrderBy(i => i.Id).Skip(skip).FirstOrDefault();
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error peeking object: {0}", e);
                    throw;
                }
            }
		}

        /// <summary>
        /// Query the specified object
        /// </summary>
        public IEnumerable<TQueueEntry> Query(Expression<Func<TQueueEntry, bool>> query, int offset, int? count, out int totalResults)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                    {
                        var retVal = conn.Table<TQueueEntry>().Where(query);
                        totalResults = retVal.Count();

                        retVal = retVal.Skip(offset);
                        if (count.HasValue)
                            retVal = retVal.Take(count.Value);
                        return retVal.OrderBy(i => i.Id).ToList();
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error querying queue: {0}", e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Counts the number of objects in the current queue
        /// </summary>
        public int Count()
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    
                    using(conn.Lock())
                        return conn.ExecuteScalar<Int32>($"SELECT COUNT(*) FROM {conn.GetMapping<TQueueEntry>().TableName}");
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error counting queue: {0}", e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Deletes the specified object from the queue
        /// </summary>
        public void Delete(int id)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                    {
                        var tdata = conn.Table<TQueueEntry>().Where(o => o.Id == id).FirstOrDefault();
                        if (tdata != null)
                        {
                            conn.Delete(tdata);
                            ApplicationContext.Current.GetService<IQueueFileProvider>().RemoveQueueData(tdata?.Data);
                        }
                        else
                            this.m_tracer.TraceWarning("Could not find queue item {0} to be deleted", id);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error deleting object: {0}", e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the specified queue item
        /// </summary>
        public TQueueEntry Get(int id)
        {
            var conn = this.CreateConnection();
            using (conn.Lock())
            {
                try
                {
                    using (conn.Lock())
                        return conn.Get<TQueueEntry>(id);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error getting queue entry {0}: {1}", id, e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Get the specified object from the queue
        /// </summary>
        /// <param name="id">The identifier of the queue entry to get</param>
        /// <returns>The queue entry</returns>
        ISynchronizationQueueEntry ISynchronizationQueue.Get(int id)
        {
            return this.Get(id);
        }

        /// <summary>
        /// Get all objects from the queue
        /// </summary>
        /// <param name="offset">The offset of the object</param>
        /// <param name="count">The number of items to get</param>
        /// <returns>The queue items</returns>
        public IEnumerable<ISynchronizationQueueEntry> GetAll(int offset, int count, out int totalResults)
        {
            return this.Query(o => o.Id > 0, offset, count, out totalResults);
        }

        /// <summary>
        /// Enqueues the specified data
        /// </summary>
        /// <param name="data">The data to be sent to the server</param>
        /// <param name="operation">The operation on the data</param>
        /// <returns>The queue item</returns>
        ISynchronizationQueueEntry ISynchronizationQueue.Enqueue(IdentifiedData data, SynchronizationOperationType operation)
        {
            return this.Enqueue(data, operation);
        }

        /// <summary>
        /// Retry to queue the specified item
        /// </summary>
        /// <param name="queueItem">The item to retry queuing </param>
        public void Retry(ISynchronizationQueueRetryEntry queueItem)
        {
            // HACK: Clean this up
            switch (queueItem.OriginalQueue)
            {
                case "inbound":
                case "inbound_queue":
                    SynchronizationQueue.Inbound.EnqueueRaw(new InboundQueueEntry(queueItem as DeadLetterQueueEntry));
                    break;
                case "outbound":
                case "outbound_queue":
                    SynchronizationQueue.Outbound.EnqueueRaw(new OutboundQueueEntry(queueItem as DeadLetterQueueEntry));
                    break;
                case "admin":
                case "admin_queue":
                    SynchronizationQueue.Admin.EnqueueRaw(new OutboundAdminQueueEntry(queueItem as DeadLetterQueueEntry));
                    break;
                default:
                    throw new KeyNotFoundException(queueItem.OriginalQueue);
            }
        }

        /// <summary>
        /// Query for matching records
        /// </summary>
        public IEnumerable<ISynchronizationQueueEntry> Query(NameValueCollection search, int offset, int count, out int totalResults)
        {
            return this.Query(QueryExpressionParser.BuildLinqExpression<TQueueEntry>(search), offset, count, out totalResults);
        }
    }
}

