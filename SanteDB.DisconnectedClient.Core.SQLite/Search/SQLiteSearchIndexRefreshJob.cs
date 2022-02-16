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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.i18n;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.SQLite.Search
{
    /// <summary>
    /// A job which indexes the SQLite free text index
    /// </summary>
    public class SQLiteSearchIndexRefreshJob : IJob
    {

        /// <summary>
        /// Get the identifier of the job
        /// </summary>
        public Guid Id => Guid.Parse("EBC6308D-8BAF-40E5-BC27-D471588A7EDC");

        // Tracer for SQLite tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteSearchIndexRefreshJob));

        // Cancel has been requested
        private bool m_cancelRequested = false;
        private readonly SQLiteSearchIndexService m_searchIndexService;
        private readonly IJobStateManagerService m_jobStateManager;

        /// <summary>
        /// DI ctor
        /// </summary>
        public SQLiteSearchIndexRefreshJob(SQLiteSearchIndexService searchIndexService, IJobStateManagerService jobStateManager)
        {
            this.m_searchIndexService = searchIndexService;
            this.m_jobStateManager = jobStateManager;
        }

        /// <summary>
        /// Gets the name of the refresh job
        /// </summary>
        public string Name => "SQLite FreeText Search Indexing";

        /// <inheritdoc/>
        public string Description => "Refreshes the SQLite FreeText search";

        /// <summary>
        /// Can cancel the job?
        /// </summary>
        public bool CanCancel => true;

        /// <summary>
        /// Gets the parameters that can be passed
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>()
        {
            { "since", typeof(DateTime) }
        };

        /// <summary>
        /// Cancel the running of this job
        /// </summary>
        public void Cancel()
        {
            this.m_tracer.TraceInfo("Cancel re-index has been requested");
            this.m_cancelRequested = true;
            this.m_jobStateManager.SetProgress(this, "Cancel Requested", 0.0f);
        }

        /// <summary>
        /// Run the re-indexing job
        /// </summary>
        public void Run(object sender, EventArgs args, object[] parameters)
        {

            this.m_tracer.TraceInfo("Starting complete full-text indexing of the primary datastore");
            try
            {

                var jobState = this.m_jobStateManager.GetJobState(this);

                // Load all entities in database and index them
                int tr = 1, ofs = 0;
                var patientService = ApplicationServiceContext.Current.GetService<IStoredQueryDataPersistenceService<Patient>>();
                Guid queryId = Guid.NewGuid();
                var since = parameters?.FirstOrDefault() as DateTime? ?? jobState.LastStopTime ?? new DateTime(1970, 01, 01);

                while (ofs < tr && !this.m_cancelRequested)
                {

                    if (patientService == null) break;
                    var entities = patientService.Query(e => e.StatusConceptKey != StatusKeys.Obsolete && e.ModifiedOn >= since, queryId, ofs, 50, out tr, AuthenticationContext.SystemPrincipal);

                    // Index 
                    this.m_tracer.TraceInfo("Index Job: Will index {0}..{1} of {2} objects", ofs, ofs + 50, tr);
                    this.m_searchIndexService.IndexEntity(entities.ToArray());

                    // Let user know the status
                    ofs += 50;

                    this.m_jobStateManager.SetProgress(this, Strings.locale_indexing, (float)ofs / tr);
                }

                if (this.m_cancelRequested)
                    this.m_jobStateManager.SetState(this, JobStateType.Cancelled);
                else
                    this.m_jobStateManager.SetState(this, JobStateType.Completed);

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error indexing primary database: {0}", e);
                this.m_jobStateManager.SetState(this, JobStateType.Aborted);
                throw;
            }
            finally
            {
                this.m_cancelRequested = false;
            }

        }



    }
}
