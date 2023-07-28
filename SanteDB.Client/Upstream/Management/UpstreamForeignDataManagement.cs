/*
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
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Alien;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream foreign data management classes
    /// </summary>
    public class UpstreamForeignDataManagement : UpstreamServiceBase, IRepositoryService<ForeignDataMap>, IForeignDataManagerService
    {
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Foreign data submission
        /// </summary>
        private class UpstreamForeignDataSubmission : IForeignDataSubmission, IDisposable
        {
            private readonly ForeignDataInfo m_upstreamData;
            private readonly IRestClient m_restClient;

            /// <summary>
            /// Constructor for upstream data wrapper
            /// </summary>
            public UpstreamForeignDataSubmission(ForeignDataInfo upstreamForeignData, IRestClient restClient)
            {
                this.m_upstreamData = upstreamForeignData;
                this.m_restClient = restClient;
            }

            /// <inheritdoc/>
            public string Name => this.m_upstreamData.Name;

            /// <inheritdoc/>
            public String Description => this.m_upstreamData.Description;

            /// <inheritdoc/>
            public ForeignDataStatus Status => this.m_upstreamData.Status;

            /// <inheritdoc/>
            public Guid ForeignDataMapKey => this.m_upstreamData.ForeignDataMap;

            /// <inheritdoc/>
            public IDictionary<String, String> ParameterValues => this.m_upstreamData.Parameters.ToDictionary(o => o.Key, o => o.Value);

            /// <inheritdoc/>
            public IEnumerable<DetectedIssue> Issues => this.m_upstreamData.Issues;

            /// <inheritdoc/>
            public Guid? Key
            {
                get => this.m_upstreamData.Key;
                set => throw new NotSupportedException();
            }

            /// <inheritdoc/>
            public string Tag => this.m_upstreamData.Tag;

            /// <inheritdoc/>
            public DateTimeOffset ModifiedOn => this.m_upstreamData.ModifiedOn;

            /// <inheritdoc/>
            public Guid? UpdatedByKey => this.m_upstreamData.UpdatedByKey;

            /// <inheritdoc/>
            public DateTimeOffset? UpdatedTime => this.m_upstreamData.UpdatedTime;

            /// <inheritdoc/>
            public Guid? CreatedByKey => this.m_upstreamData.CreatedByKey;

            /// <inheritdoc/>
            public Guid? ObsoletedByKey => this.m_upstreamData.ObsoletedByKey;

            /// <inheritdoc/>
            public DateTimeOffset CreationTime => this.m_upstreamData.CreationTime;

            /// <inheritdoc/>
            public DateTimeOffset? ObsoletionTime => this.m_upstreamData.ObsoletionTime;

            public void AddAnnotation<T>(T annotation)
            {
                throw new NotImplementedException();
            }

            public IAnnotatedResource CopyAnnotations(IAnnotatedResource other)
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc/>
            public void Dispose() => this.m_restClient.Dispose();

            public IEnumerable<T> GetAnnotations<T>()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Get the rejection stream
            /// </summary>
            public Stream GetRejectStream()
            {
                return new MemoryStream(this.m_restClient.Get($"/{typeof(ForeignDataInfo).GetSerializationName()}/{this.Key}/_file/reject"));
            }

            /// <summary>
            /// Get the source stream
            /// </summary>
            public Stream GetSourceStream()
            {
                return new MemoryStream(this.m_restClient.Get($"/{typeof(ForeignDataInfo).GetSerializationName()}/{this.Key}/_file/source"));
            }

            public void RemoveAnnotation(object annotation)
            {
                throw new NotImplementedException();
            }

            public void RemoveAnnotations<T>()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamForeignDataManagement(ILocalizationService localizationService, IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Gets the name of the service
        /// </summary>
        public string ServiceName => "Upstream Foreign Data Manager";

        /// <inheritdoc/>
        public IForeignDataSubmission Delete(Guid foreignDataId)
        {
            try
            {
                var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
                return new UpstreamForeignDataSubmission(client.Delete<ForeignDataInfo>($"{typeof(ForeignDataInfo).GetSerializationName()}/{foreignDataId}"), client);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "ForeignData" }), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Execute(Guid foreignDataId)
        {
            try
            {
                var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
                return new UpstreamForeignDataSubmission(client.Post<ParameterCollection, ForeignDataInfo>($"{typeof(ForeignDataInfo).GetSerializationName()}/{foreignDataId}/$execute", new ParameterCollection()), client);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "ForeignData" }), e);
            }

        }

        /// <inheritdoc/>
        public IQueryResultSet<IForeignDataSubmission> Find(Expression<Func<IForeignDataSubmission, bool>> query)
        {
            var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
            return new UpstreamQueryResultSet<IForeignDataSubmission, ForeignDataInfo, AmiCollection>(client, "ForeignData", query, o => new UpstreamForeignDataSubmission(o, client));
        }

        /// <inheritdoc/>
        public IQueryResultSet<ForeignDataMap> Find(Expression<Func<ForeignDataMap, bool>> query)
        {
            var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
            return new UpstreamQueryResultSet<ForeignDataMap, AmiCollection>(client, query);
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Get(Guid foreignDataId)
        {
            try
            {
                var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
                return new UpstreamForeignDataSubmission(client.Get<ForeignDataInfo>($"{typeof(ForeignDataInfo).GetSerializationName()}/{foreignDataId}"), client);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "ForeignData" }), e);
            }
        }

        /// <inheritdoc/>
        public ForeignDataMap Get(Guid key, Guid versionKey)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ForeignDataMap Insert(ForeignDataMap data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ForeignDataMap Save(ForeignDataMap data)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Schedule(Guid foreignDataId)
        {
            try
            {
                var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
                return new UpstreamForeignDataSubmission(client.Post<ParameterCollection, ForeignDataInfo>($"{typeof(ForeignDataInfo).GetSerializationName()}/{foreignDataId}/$schedule", new ParameterCollection()), client);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "ForeignData" }), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Stage(Stream inputStream, string name, string description, Guid foreignDataMapKey, IDictionary<String, String> parameterValues)
        {
            try
            {
                List<MultiPartFormData> stageSubmission = new List<MultiPartFormData>();
                stageSubmission.Add(new MultiPartFormData("description", description));
                stageSubmission.Add(new MultiPartFormData("map", foreignDataMapKey.ToString()));
                stageSubmission.AddRange(parameterValues.Select(o => new MultiPartFormData(o.Key, o.Value)));

                using (var ms = new MemoryStream())
                {
                    inputStream.CopyTo(ms);
                    stageSubmission.Add(new MultiPartFormData("source", ms.ToArray(), "text/csv", name, true));
                }
                var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal);
                var submission = client.Post<List<MultiPartFormData>, ForeignDataInfo>($"{typeof(ForeignDataInfo).GetSerializationName()}", $"multipart/form-data; boundary={Guid.NewGuid():N}", stageSubmission);
                return new UpstreamForeignDataSubmission(submission, client);
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = "ForeignData" }), e);
            }

        }

        ForeignDataMap IRepositoryService<ForeignDataMap>.Delete(Guid key)
        {
            throw new NotImplementedException();
        }

        ForeignDataMap IRepositoryService<ForeignDataMap>.Get(Guid key)
        {
            throw new NotImplementedException();
        }
    }
}
