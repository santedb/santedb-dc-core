﻿/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SanteDB.Core;
using SanteDB.Core.Event;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    internal class SynchronizationQueue<TEntry> : ISynchronizationQueue, IDisposable where TEntry : ISynchronizationQueueEntry, new()
    {
        private static JsonSerializerSettings s_SerializerSettings = new JsonSerializerSettings() { TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple, TypeNameHandling = TypeNameHandling.Auto };
        private static ThreadSafeRandomNumberGenerator s_Rand = new ThreadSafeRandomNumberGenerator();

        private FileStream _BackingFile;
        private ReaderWriterLockSlim _Lock;
        private Queue<int> _EntryQueue;
        private readonly ISymmetricCryptographicProvider _SymmetricEncryptionProvider;
        readonly JsonSerializer _Serializer;

        readonly Type _EntryType;

        private bool disposedValue;

        public string Name { get; }
        public SynchronizationPattern Type { get; }

        public string Path { get; }

        public SynchronizationQueue(string name, SynchronizationPattern type, string path, ISymmetricCryptographicProvider symmetricCryptographicProvider)
        {
            Name = name;
            Type = type;
            Path = path;
            _EntryType = typeof(TEntry);
            _Lock = new ReaderWriterLockSlim();
            _BackingFile = new FileStream(GetQueueFilename(Path), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _Serializer = CreateSerializer();
            _EntryQueue = ReadQueueFromFileInternal() ?? new Queue<int>(); //Safe without lock because we're constructing.
            _SymmetricEncryptionProvider = symmetricCryptographicProvider;
        }

        private static string GetQueueFilename(string path) => System.IO.Path.Combine(path, "_queue.json");
        private static string GetEntryFilename(string path, int id) => System.IO.Path.Combine(path, id.ToString() + ".json");

        private static JsonSerializer CreateSerializer()
        {
            JsonSerializer jsz = new JsonSerializer()
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new ModelSerializationBinder()
            };
            jsz.Converters.Add(new StringEnumConverter());
            return jsz;
        }

        /// <summary>
        /// Internal method to read the queue from the backing file. 
        /// </summary>
        /// <returns></returns>
        private Queue<int> ReadQueueFromFileInternal()
        {
            try
            {
                using (var sr = new StreamReader(_BackingFile, Encoding.UTF8, true, 256, leaveOpen: true))
                {
                    using (var jtr = new JsonTextReader(sr))
                    {
                        return _Serializer.Deserialize<Queue<int>>(jtr);
                    }
                }
            }
            finally
            {
                _BackingFile.Seek(0, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Internal method to write the queue to the backing file.
        /// </summary>
        private void WriteQueueToFileInternal()
        {
            try
            {
                _BackingFile.SetLength(0);

                using (var sw = new StreamWriter(_BackingFile, Encoding.UTF8, 256, leaveOpen: true))
                {
                    using (var jtw = new JsonTextWriter(sw))
                    {
                        _Serializer.Serialize(jtw, _EntryQueue);
                        jtw.Flush();
                        jtw.Close();
                    }
                }
            }
            finally
            {
                _BackingFile.Seek(0, SeekOrigin.Begin);
            }
        }

        public event EventHandler<DataPersistingEventArgs<ISynchronizationQueueEntry>> Enqueuing;
        public event EventHandler<DataPersistedEventArgs<ISynchronizationQueueEntry>> Enqueued;


        private T LockRead<T>(Func<Queue<int>, T> func)
        {
            _Lock.EnterReadLock();
            try
            {
                return func(_EntryQueue);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        private void LockRead(Action<Queue<int>> action)
        {
            _Lock.EnterReadLock();
            try
            {
                action(_EntryQueue);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        private T LockReadUpgradeable<T>(Func<Queue<int>, T> func)
        {
            _Lock.EnterUpgradeableReadLock();
            try
            {
                return func(_EntryQueue);
            }
            finally
            {
                _Lock.ExitUpgradeableReadLock();
            }
        }

        private void LockReadUpgradeable(Action<Queue<int>> action)
        {
            _Lock.EnterUpgradeableReadLock();
            try
            {
                action(_EntryQueue);
            }
            finally
            {
                _Lock.ExitUpgradeableReadLock();
            }
        }

        private T LockWrite<T>(Func<Queue<int>, T> func)
        {
            _Lock.EnterWriteLock();
            try
            {
                return func(_EntryQueue);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        private void LockWrite(Action<Queue<int>> action)
        {
            _Lock.EnterWriteLock();
            try
            {
                action(_EntryQueue);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        public int Count() => LockRead(q => q.Count);

        public void Delete(int id)
        {
            try
            {
                //We don't need to remove the entry from the queue. On Dequeue and Peek, not finding the file will silently move to the next entry.
                File.Delete(GetEntryFilename(Path, id));
            }
            catch (FileNotFoundException)
            {
                //TODO: Verbose Logging
            }
            catch (DirectoryNotFoundException)
            {
                //TODO: Verbose Logging
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                //TODO: Logging.
                throw;
            }
        }

        public ISynchronizationQueueEntry Dequeue()
        {
            return LockWrite<TEntry>(q =>
            {
                try
                {
                    while (true)
                    {
                        var id = q.Dequeue();

                        if (id < 1)
                        {
                            throw new InvalidOperationException("Queue corruption issue.");
                        }

                        var entry = ReadEntryInternal(id);

                        if (null != entry)
                        {
                            WriteQueueToFileInternal();
                            Delete(id);
                            return entry;
                        }

                        //TODO: Log bad entry.
                    }
                }
                catch (InvalidOperationException)
                {
                    //Queue is empty
                    return default;
                }
            });
        }



        private int DequeueBadEntry() => LockWrite(q => q.Dequeue());

        public ISynchronizationQueueEntry Enqueue(IdentifiedData data, SynchronizationQueueEntryOperation operation)
            => Enqueue(CreateEntry(data, operation));

        public TEntry Enqueue(TEntry entry)
        {
            var preevt = new DataPersistingEventArgs<ISynchronizationQueueEntry>(entry, Core.Services.TransactionMode.Commit, AuthenticationContext.Current.Principal);

            Enqueuing?.Invoke(this, preevt);

            if (preevt.Cancel)
            {
                if (preevt.Success)
                {
                    var successevt = new DataPersistedEventArgs<ISynchronizationQueueEntry>(preevt.Data, Core.Services.TransactionMode.Commit, AuthenticationContext.Current.Principal);

                    Enqueued?.Invoke(this, successevt);
                }
                //TODO: Logging
                return entry;
            }

            LockWrite(q =>
            {
                WriteEntryInternal(entry);
                q.Enqueue(entry.Id);
                WriteQueueToFileInternal();
            });

            var postevt = new DataPersistedEventArgs<ISynchronizationQueueEntry>(entry, Core.Services.TransactionMode.Commit, AuthenticationContext.Current.Principal);
            Enqueued?.Invoke(this, postevt);

            return entry;
        }

        private TEntry CreateEntry(IdentifiedData data, SynchronizationQueueEntryOperation operation) => new TEntry()
        {
            CreationTime = DateTime.UtcNow,
            Data = data,
            Id = s_Rand.Next(),
            Operation = operation,
            Type = data.GetType().Name
        };

        public TEntry Get(int id)
        {
            return ReadEntryInternal(id);
        }

        private TEntry ReadEntryInternal(int id)
        {
            var serializer = CreateSerializer();

            try
            {
                using (var fs = new FileStream(GetEntryFilename(Path, id), FileMode.Open, FileAccess.Read))
                {
#if DEBUG
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true, 512, leaveOpen: false))
                    {
                        using (var jtr = new JsonTextReader(sr))
                        {
                            return serializer.Deserialize<TEntry>(jtr);
                        }
                    }
#else
                    byte[] iv = new byte[16];
                    fs.Read(iv, 0, 16);
                    using (var es = this._SymmetricEncryptionProvider.CreateDecryptingStream(fs, this._SymmetricEncryptionProvider.GetContextKey(), iv))
                    {
                        using (var cs = new GZipStream(es, CompressionMode.Decompress))
                        {
                            using (var sr = new StreamReader(cs, Encoding.UTF8, true, 512, leaveOpen: false))
                            {
                                using (var jtr = new JsonTextReader(sr))
                                {
                                    return serializer.Deserialize<TEntry>(jtr);
                                }
                            }
                        }
                    }
#endif
                }
            }
            catch (FileNotFoundException)
            {
                return default;
            }
            catch (DirectoryNotFoundException)
            {
                return default;
            }
            catch (IOException)
            {
                //TODO: Add Logging.
                throw;
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                //TODO: Add logging.
                throw;
            }
        }

        private void WriteEntryInternal(TEntry entry)
        {
            if (null == entry)
            {
                throw new ArgumentNullException(nameof(entry));
            }


            var serializer = CreateSerializer();

            try
            {
                using (var fs = new FileStream(GetEntryFilename(Path, entry.Id), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
#if DEBUG
                    using (var sw = new StreamWriter(fs))
                    {
                        using (var jtw = new JsonTextWriter(sw))
                        {
                            serializer.Serialize(jtw, entry, _EntryType);
                            jtw.Flush();
                        }
                    }
#else
                    var iv = this._SymmetricEncryptionProvider.GenerateIV();
                    fs.Write(iv, 0, iv.Length);
                    using (var es = this._SymmetricEncryptionProvider.CreateEncryptingStream(fs, this._SymmetricEncryptionProvider.GetContextKey(), iv))
                    {
                        using (var cs = new GZipStream(es, CompressionLevel.Fastest))
                        {
                            using (var sw = new StreamWriter(cs))
                            {
                                using (var jtw = new JsonTextWriter(sw))
                                {
                                    serializer.Serialize(jtw, entry, _EntryType);
                                    jtw.Flush();
                                }
                            }
                        }
                    }
#endif
                }
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                //TODO: Logging
                throw;
            }
        }

        public TEntry Peek()
        {
            return LockReadUpgradeable(queue =>
            {
                try
                {
                    while (true)
                    {
                        var id = queue.Peek();

                        if (id < 1)
                        {
                            throw new InvalidOperationException("Queue corruption issue.");
                        }

                        var entry = ReadEntryInternal(id);

                        if (null != entry)
                        {
                            return entry;
                        }


                        _ = DequeueBadEntry();
                        //We do not do a file write here. We will write the next meaninful change.
                        //If SanteDB crashes, we will just silently drop these entries again.

                    }
                }
                catch (InvalidOperationException) {  /* Queue is empty */ }

                return default;
            });
        }

        #region Disposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _BackingFile?.Dispose();
                    _Lock.Dispose();
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~QueueDefinition()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        ISynchronizationQueueEntry ISynchronizationQueue.Peek() => this.Peek();

        ISynchronizationQueueEntry ISynchronizationQueue.Get(int id) => this.Get(id);

        void ISynchronizationQueue.Retry(ISynchronizationDeadLetterQueueEntry queueItem)
        {
            throw new NotImplementedException();
        }

        IQueryResultSet<ISynchronizationQueueEntry> ISynchronizationQueue.Query(NameValueCollection search)
        {
            throw new NotImplementedException();
        }
    }
}
