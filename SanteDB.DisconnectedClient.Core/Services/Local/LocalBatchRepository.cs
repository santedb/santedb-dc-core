﻿/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Local batch repository service
    /// </summary>
    public class LocalBatchRepository :
        GenericLocalRepository<Bundle>
    {

        /// <summary>
        /// Find the specified bundle (Not supported)
        /// </summary>
        public override IEnumerable<Bundle> Find(Expression<Func<Bundle, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Find the specfied bundle (not supported)
        /// </summary>
        public override IEnumerable<Bundle> Find(Expression<Func<Bundle, bool>> query, int offset, int? count, out int totalResults, params ModelSort<Bundle>[] orderBy)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get the specified bundle (not supported)
        /// </summary>
        public override Bundle Get(Guid key)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get the specified bundle (not supported)
        /// </summary>
        public override Bundle Get(Guid key, Guid versionKey)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Insert the bundle
        /// </summary>
        public override Bundle Insert(Bundle data)
        {
            // We need permission to insert all of the objects
            foreach (var itm in data.Item)
            {
                var irst = typeof(IRepositoryService<>).MakeGenericType(itm.GetType());
                var irsi = ApplicationContext.Current.GetService(irst);
                if (irsi is ISecuredRepositoryService)
                    (irsi as ISecuredRepositoryService).DemandWrite(itm);
            }
            return base.Insert(data);
        }

        /// <summary>
        /// Obsoleting bundles are not supported
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override Bundle Obsolete(Guid key)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Save the specified bundle
        /// </summary>
        /// <summary>
        /// Save the specified bundle
        /// </summary>
        public override Bundle Save(Bundle data)
        {
            // We need permission to insert all of the objects
            foreach (var itm in data.Item)
            {
                var irst = typeof(IRepositoryService<>).MakeGenericType(itm.GetType());
                var irsi = ApplicationContext.Current.GetService(irst);
                if (irsi is ISecuredRepositoryService)
                    (irsi as ISecuredRepositoryService).DemandAlter(itm);
            }

            // Demand permission
            this.DemandAlter(data);

            var persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<Bundle>>();
            var businessRulesService = ApplicationContext.Current.GetService<IBusinessRulesService<Bundle>>();

            if (persistenceService == null)
                throw new InvalidOperationException($"Unable to locate {nameof(IDataPersistenceService<Bundle>)}");

            data = this.Validate(data);

            data = businessRulesService?.BeforeUpdate(data) ?? data;

            // Before we update we need to get a reference to the old object so we can patch
            // Entry point
            IdentifiedData old = null;
            if (data.EntryKey != null)
            {
                var type = data.Entry.GetType();
                var idps = typeof(IDataPersistenceService<>).MakeGenericType(type);
                var dataService = ApplicationContext.Current.GetService(idps) as IDataPersistenceService;
                old = (dataService.Get(data.EntryKey.Value) as IdentifiedData).Clone();
            }

            data = persistenceService.Update(data, TransactionMode.Commit, AuthenticationContext.Current.Principal);

            // Patch
            if (old != null)
            {
                var diff = ApplicationContext.Current.GetService<IPatchService>()?.Diff(old, data.Entry);
                if (diff != null)
                    ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(diff, SynchronizationOperationType.Update);
                else
                    ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Update);
            }
            else
                ApplicationContext.Current.GetService<IQueueManagerService>()?.Outbound.Enqueue(data, SynchronizationOperationType.Update);

            businessRulesService?.AfterUpdate(data);
            return data;
        }
    }
}