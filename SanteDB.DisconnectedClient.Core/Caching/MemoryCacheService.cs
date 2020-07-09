/*
 * Based on OpenIZ, Copyright (C) 2015 - 2020 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Services;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Caching
{
    /// <summary>
    /// Memory cache service
    /// </summary>
    public class MemoryCacheService : IDataCachingService, IDaemonService
    {

        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Memory Caching Service";

        /// <summary>
        /// Cache of data
        /// </summary>
        private EventHandler<ModelMapEventArgs> m_mappingHandler = null;
        private EventHandler<ModelMapEventArgs> m_mappedHandler = null;

        private object lockInstance = new object();

        // Memory cache configuration
        private Tracer m_tracer = Tracer.GetTracer(typeof(MemoryCacheService));

        /// <summary>
        /// True when the memory cache is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return this.m_mappedHandler != null && m_mappedHandler != null;
            }
        }

        public long Size
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Service is starting
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Service has started
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Service is stopping
        /// </summary>
        public event EventHandler Stopped;
        /// <summary>
        /// Service has stopped
        /// </summary>
        public event EventHandler Stopping;
        public event EventHandler<DataCacheEventArgs> Added;
        public event EventHandler<DataCacheEventArgs> Updated;
        public event EventHandler<DataCacheEventArgs> Removed;

        /// <summary>
        /// Start the service
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            this.m_tracer.TraceInfo("Starting Memory Caching Service...");

            this.Starting?.Invoke(this, EventArgs.Empty);

            // subscribe to events
            this.Added += (o, e) => this.EnsureCacheConsistency(e);
            this.Updated += (o, e) => this.EnsureCacheConsistency(e);
            this.Removed += (o, e) => this.EnsureCacheConsistency(e);

            // Initialization parameters - Load concept dictionary
            ApplicationContext.Current.Started += (os, es) => ApplicationContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((a) =>
             {

                 // Seed the cache
                 try
                 {
                     this.m_tracer.TraceInfo("Loading concept dictionary ...");
                     //ApplicationContext.Current.GetService<IDataPersistenceService<Concept>>().Query(q => q.StatusConceptKey == StatusKeys.Active);
                     //ApplicationContext.Current.GetService<IDataPersistenceService<IdentifierType>>().Query(q => true);
                     //ApplicationContext.Current.GetService<IDataPersistenceService<AssigningAuthority>>().Query(q => true);

                     foreach (var i in ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>().SubscribeTo)
                         ApplicationContext.Current.GetService<IDataPersistenceService<Place>>().Get(Guid.Parse(i), null, false, AuthenticationContext.SystemPrincipal);

                     // Seed cache
                     this.m_tracer.TraceInfo("Loading materials dictionary...");
                     ApplicationContext.Current.GetService<IDataPersistenceService<Material>>()?.Query(q => q.StatusConceptKey == StatusKeys.Active, AuthenticationContext.SystemPrincipal);
                     ApplicationContext.Current.GetService<IDataPersistenceService<ManufacturedMaterial>>()?.Query(q => q.StatusConceptKey == StatusKeys.Active, AuthenticationContext.SystemPrincipal);

                     // handles when a item is being mapped
                     if (this.m_mappingHandler == null)
                     {
                         this.m_mappingHandler = (o, e) =>
                         {
                             var obj = MemoryCache.Current.TryGetEntry(e.Key);
                             if (obj != null)
                             {
                                 var cVer = obj as IVersionedEntity;
                                 var dVer = e.ModelObject as IVersionedEntity;
                                 if (cVer?.VersionSequence <= dVer?.VersionSequence) // Cache is older than this item
                                 {
                                     e.ModelObject = obj as IdentifiedData;
                                     e.Cancel = true;
                                 }
                             }
                             //this.GetOrUpdateCacheItem(e);
                         };

                         // Handles when an item is no longer being mapped
                         this.m_mappedHandler = (o, e) =>
                         {
                             //MemoryCache.Current.RegisterCacheType(e.ObjectType);
                             //this.GetOrUpdateCacheItem(e);
                         };

                         // Subscribe to message mapping
                         ModelMapper.MappingToModel += this.m_mappingHandler;
                         ModelMapper.MappedToModel += this.m_mappedHandler;

                         // Now start the clean timers
                         this.m_tracer.TraceInfo("Starting clean timers...");

                         Action<Object> cleanProcess = null, pressureProcess = null;

                         cleanProcess = o =>
                         {
                             MemoryCache.Current.Clean();
                             ApplicationContext.Current.GetService<IThreadPoolService>()?.QueueUserWorkItem(new TimeSpan(ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Cache.MaxDirtyAge), cleanProcess, null);
                         };
                         pressureProcess = o =>
                         {
                             MemoryCache.Current.ReducePressure();
                             ApplicationContext.Current.GetService<IThreadPoolService>()?.QueueUserWorkItem(new TimeSpan(ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Cache.MaxDirtyAge), pressureProcess, null);
                         };

                         // Register processes on a delay
                         ApplicationContext.Current.GetService<IThreadPoolService>()?.QueueUserWorkItem(new TimeSpan(ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Cache.MaxDirtyAge), pressureProcess, null);
                         ApplicationContext.Current.GetService<IThreadPoolService>()?.QueueUserWorkItem(new TimeSpan(ApplicationContext.Current.Configuration.GetSection<ApplicationConfigurationSection>().Cache.MaxDirtyAge), cleanProcess, null);

                     }
                 }
                 catch
                 {
                     this.m_tracer.TraceWarning("Caching will be disabled due to cache load error");
                 }

             });
            // Now we start timers
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Ensure cache consistency
        /// </summary>
        private void EnsureCacheConsistency(DataCacheEventArgs e)
        {
            //// Relationships should always be clean of source/target so the source/target will load the new relationship
            if (e.Object is ActParticipation)
            {
                var ptcpt = (e.Object as ActParticipation);
                MemoryCache.Current.RemoveObject(ptcpt.Key.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(ptcpt.SourceEntityKey.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(ptcpt.ActKey.GetValueOrDefault());
            }
            else if (e.Object is ActRelationship)
            {
                var rel = (e.Object as ActRelationship);
                MemoryCache.Current.RemoveObject(rel.Key.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(rel.SourceEntityKey.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(rel.TargetActKey.GetValueOrDefault());
            }
            else if (e.Object is EntityRelationship)
            {
                var rel = (e.Object as EntityRelationship);
                MemoryCache.Current.RemoveObject(rel.Key.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(rel.SourceEntityKey.GetValueOrDefault());
                MemoryCache.Current.RemoveObject(rel.TargetEntityKey.GetValueOrDefault());
            }
            

        }

        /// <summary>
        /// Either gets or updates the existing cache item
        /// </summary>
        /// <param name="e"></param>
        private void GetOrUpdateCacheItem(ModelMapEventArgs e)
        {
            var cacheItem = MemoryCache.Current.TryGetEntry(e.Key);
            if (cacheItem == null)
                MemoryCache.Current.AddUpdateEntry(e.ModelObject);
            else
            {
                // Obsolete?
                var cVer = cacheItem as IVersionedEntity;
                var dVer = e.ModelObject as IVersionedEntity;
                if (cVer?.VersionSequence < dVer?.VersionSequence) // Cache is older than this item
                    MemoryCache.Current.AddUpdateEntry(dVer);
                e.ModelObject = cacheItem as IdentifiedData;
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Stopping
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            ModelMapper.MappingToModel -= this.m_mappingHandler;
            ModelMapper.MappedToModel -= this.m_mappedHandler;

            this.m_mappingHandler = null;
            this.m_mappedHandler = null;

            MemoryCache.Current.Clear();

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Gets the specified cache item
        /// </summary>
        /// <returns></returns>
        public TData GetCacheItem<TData>(Guid key) where TData : IdentifiedData
        {
            return MemoryCache.Current.TryGetEntry(key) as TData;
        }

        /// <summary>
        /// Gets the specified cache item
        /// </summary>
        /// <returns></returns>
        public Object GetCacheItem(Guid key)
        {
            return MemoryCache.Current.TryGetEntry(key);
        }


        /// <summary>
        /// Add the specified item to the memory cache
        /// </summary>
        public void Add(IdentifiedData data)
        {
            IdentifiedData[] elements = null;
            if (data is Bundle bundle)
                elements = bundle.Item.ToArray();
            else
                elements = new IdentifiedData[1] { data };

            foreach (var d in elements)
            {
                var exist = MemoryCache.Current.TryGetEntry(d.Key);
                MemoryCache.Current.AddUpdateEntry(d);
                if (exist != null)
                    this.Updated?.Invoke(this, new DataCacheEventArgs(d));
                else
                    this.Added?.Invoke(this, new DataCacheEventArgs(d));
            }
        }

        /// <summary>
        /// Remove the object from the cache
        /// </summary>
        public void Remove(Guid key)
        {
            var exist = MemoryCache.Current.TryGetEntry(key);
            if (exist != null)
            {
                MemoryCache.Current.RemoveObject(key);
                this.Removed?.Invoke(this, new DataCacheEventArgs(exist));
            }
        }

        /// <summary>
        /// Clear the memory cache
        /// </summary>
        public void Clear()
        {
            MemoryCache.Current.Clear();
        }
    }
}
