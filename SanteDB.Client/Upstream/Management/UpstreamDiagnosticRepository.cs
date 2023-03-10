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
 * Date: 2023-3-10
 */
using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Event;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Upstream diagnostic repository
    /// </summary>
    public class UpstreamDiagnosticRepository : UpstreamServiceBase, IDataPersistenceService<DiagnosticReport>
    {
        private readonly ILocalizationService m_localeService;
        private readonly ILogManagerService m_logManagerService;
        private readonly IConfigurationManager m_configurationService;

        /// <summary>
        /// Defaut CTOR
        /// </summary>
        public UpstreamDiagnosticRepository(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, ILocalizationService localizationService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, ILogManagerService logManagerSerivce = null, IConfigurationManager configurationManager = null, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_localeService = localizationService;
            this.m_logManagerService = logManagerSerivce;
            this.m_configurationService = configurationManager;
        }

        /// <summary>
        /// Get the service name
        /// </summary>
        public string ServiceName => "Upstream Diagnostics Report Submitter";

        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Inserted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Inserting;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Updated;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Updating;
        /// <inheritdoc/>
        public event EventHandler<DataPersistedEventArgs<DiagnosticReport>> Deleted;
        /// <inheritdoc/>
        public event EventHandler<DataPersistingEventArgs<DiagnosticReport>> Deleting;
        /// <inheritdoc/>
        public event EventHandler<QueryResultEventArgs<DiagnosticReport>> Queried;
        /// <inheritdoc/>
        public event EventHandler<QueryRequestEventArgs<DiagnosticReport>> Querying;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievingEventArgs<DiagnosticReport>> Retrieving;
        /// <inheritdoc/>
        public event EventHandler<DataRetrievedEventArgs<DiagnosticReport>> Retrieved;

        /// <inheritdoc/>
        public long Count(Expression<Func<DiagnosticReport, bool>> query, IPrincipal authContext = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Delete(Guid key, TransactionMode transactionMode, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Get(Guid key, Guid? versionKey, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Insert(DiagnosticReport data, TransactionMode transactionMode, IPrincipal principal)
        {
            try
            {
                for(int i = 0; i < data.Attachments.Count; i++)
                {
                    if (data.Attachments[i].GetType().Name == nameof(DiagnosticAttachmentInfo))
                    {
                        switch (data.Attachments[i].FileName)
                        {
                            case "SanteDB.config":
                                using (var ms = new MemoryStream())
                                {
                                    this.m_configurationService.Configuration.Save(ms);
                                    data.Attachments[i] = new DiagnosticBinaryAttachment()
                                    {
                                        Content = ms.ToArray(),
                                        ContentType = "text/xml",
                                        FileDescription = "Configuration",
                                        FileName = "santedb.config.xml",
                                        FileSize = ms.Length
                                    };
                                }
                                break;
                            case "SanteDB.log":
                                var newestFile = this.m_logManagerService.GetLogFiles().OrderByDescending(o => o.LastWriteTime).First();
                                using (var fr = newestFile.OpenText())
                                {
                                    data.Attachments[i] = new DiagnosticTextAttachment()
                                    {
                                        Content = fr.ReadToEnd(),
                                        ContentType = "text/plain",
                                        FileDescription = "Log File",
                                        FileName = newestFile.Name,
                                        LastWriteDate = newestFile.LastWriteTime
                                    };
                                }
                                break;
                        }
                    }

                
                }

                data.ApplicationInfo = new DiagnosticApplicationInfo(Assembly.GetEntryAssembly() ?? this.GetType().Assembly);
                using (var client = base.CreateAmiServiceClient())
                {
                    return client.SubmitDiagnosticReport(data);
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);

            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<DiagnosticReport> Query(Expression<Func<DiagnosticReport, bool>> query, IPrincipal principal)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<DiagnosticReport> Query(Expression<Func<DiagnosticReport, bool>> query, int offset, int? count, out int totalResults, IPrincipal principal, params ModelSort<DiagnosticReport>[] orderBy)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public DiagnosticReport Update(DiagnosticReport data, TransactionMode transactionMode, IPrincipal principal)
        {
            throw new NotSupportedException();
        }
    }
}
