﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Services;
using System;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Services
{

    /// <summary>
    /// Integration result arguments
    /// </summary>
    public class IntegrationResultEventArgs : EventArgs
    {

        /// <summary>
        /// Creates a new integration result based on the response
        /// </summary>
        public IntegrationResultEventArgs(IdentifiedData submitted, IdentifiedData result)
        {
            this.SubmittedData = submitted;
            this.ResponseData = result;
        }
        
        /// <summary>
        /// The data that was submitted to the server
        /// </summary>
        public IdentifiedData SubmittedData { get; set; }

        /// <summary>
        /// The data that the server responded with
        /// </summary>
        public IdentifiedData ResponseData { get; set; }

    }

    /// <summary>
    /// Represents an integration service which is responsible for sending and
    /// pulling data to/from remote sources
    /// </summary>
    public interface IIntegrationService : IServiceImplementation
    {

        /// <summary>
        /// Fired on response
        /// </summary>
        event EventHandler<RestResponseEventArgs> Responding;

        /// <summary>
        /// Progress has changed
        /// </summary>
        event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// The remote system has responsed
        /// </summary>
        event EventHandler<IntegrationResultEventArgs> Responded;

        /// <summary>
        /// Find the specified filtered object
        /// </summary>
        Bundle Find(Type modelType, NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null);

        /// <summary>
        /// Find the specified filtered object
        /// </summary>
        Bundle Find<TModel>(NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData;

        /// <summary>
        /// Instructs the integration service to locate a specified object(s)
        /// </summary>
        Bundle Find<TModel>(Expression<Func<TModel, bool>> predicate, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData;

        /// <summary>
        /// Instructs the integration service to retrieve the specified object
        /// </summary>
        IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, IntegrationQueryOptions options = null);

        /// <summary>
        /// Gets a specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of model data to retrieve.</typeparam>
        /// <param name="key">The key of the model.</param>
        /// <param name="versionKey">The version key of the model.</param>
        /// <param name="options">The integrations query options.</param>
        /// <returns>Returns a model.</returns>
        TModel Get<TModel>(Guid key, Guid? versionKey, IntegrationQueryOptions options = null) where TModel : IdentifiedData;

        /// <summary>
        /// Inserts specified data.
        /// </summary>
        /// <param name="data">The data to be inserted.</param>
        void Insert(IdentifiedData data);

        /// <summary>
        /// Determines whether the network is available.
        /// </summary>
        /// <returns>Returns true if the network is available.</returns>
        bool IsAvailable();

        /// <summary>
        /// Obsoletes specified data.
        /// </summary>
        /// <param name="data">The data to be obsoleted.</param>
        void Obsolete(IdentifiedData data, bool forceObsolete = false);

        /// <summary>
        /// Updates specified data.
        /// </summary>
        /// <param name="data">The data to be updated.</param>
        /// <param name="forceUpdate">When true, indicates that update should not do a safety check</param>
        void Update(IdentifiedData data, bool forceUpdate = false);
    }

    /// <summary>
    /// Represents the clinical integration service
    /// </summary>
    public interface IClinicalIntegrationService : IIntegrationService
    {
        /// <summary>
        /// Get the server time drift
        /// </summary>
        TimeSpan GetServerTimeDrift();

        /// <summary>
        /// Determine if the object exists
        /// </summary>
        bool Exists<T>(Guid value);
    }

    /// <summary>
    /// Admin integration service
    /// </summary>
    public interface IAdministrationIntegrationService : IIntegrationService
    {
        /// <summary>
        /// Gets the security user.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Returns the security user for the given key or null if no security user is found.</returns>
        SecurityUser GetSecurityUser(Guid key);
    }
}
