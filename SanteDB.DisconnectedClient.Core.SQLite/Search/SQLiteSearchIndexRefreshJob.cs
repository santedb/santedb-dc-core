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
 * Date: 2020-1-1
 */
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.SQLite.Search.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.SQLite.Search
{
    /// <summary>
    /// A job which indexes the SQLite free text index
    /// </summary>
    public class SQLiteSearchIndexRefreshJob : IJob
    {

        // Tracer for SQLite tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(SQLiteSearchIndexService));
        // Cancel has been requested
        private bool m_cancelRequested = false;

        /// <summary>
        /// Gets the name of the refresh job
        /// </summary>
        public string Name => "SQLite FreeText Search Indexing";

        /// <summary>
        /// Can cancel the job?
        /// </summary>
        public bool CanCancel => true;

        /// <summary>
        /// The current state of the job
        /// </summary>
        public JobStateType CurrentState { get; private set; }

        /// <summary>
        /// Gets the parameters that can be passed
        /// </summary>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>()
        {
            { "since", typeof(DateTime) }
        };

        /// <summary>
        /// Time that the service last started
        /// </summary>
        public DateTime? LastStarted { get; private set; }

        /// <summary>
        /// Gets the time the service last finished
        /// </summary>
        public DateTime? LastFinished
        {
            get
            {
                var lastRun = ApplicationContext.Current.ConfigurationManager.GetAppSetting("santedb.mobile.core.search.lastIndex");
                if (lastRun == null)
                    return null;
                else
                    return DateTime.Parse(lastRun);
            }
            set
            {
                if (this.CurrentState == JobStateType.Completed)
                    ApplicationContext.Current.ConfigurationManager.SetAppSetting("santedb.mobile.core.search.lastIndex", value.ToString());
            }
        }

        /// <summary>
        /// Cancel the running of this job
        /// </summary>
        public void Cancel()
        {
            this.m_tracer.TraceInfo("Cancel re-index has been requested");
            this.m_cancelRequested = true;
        }

        /// <summary>
        /// Run the re-indexing job
        /// </summary>
        public void Run(object sender, EventArgs args, object[] parameters)
        {

            if (this.CurrentState == JobStateType.Running)
            {
                this.m_tracer.TraceWarning("Ignoring start index request: Already indexing");
                return;
            }

            this.LastStarted = DateTime.Now;
            this.CurrentState = JobStateType.Running;
            this.m_tracer.TraceInfo("Starting complete full-text indexing of the primary datastore");
            try
            {
                // Load all entities in database and index them
                int tr = 101, ofs = 0;
                var patientService = ApplicationServiceContext.Current.GetService<IStoredQueryDataPersistenceService<Patient>>();
                Guid queryId = Guid.NewGuid();
                var since = parameters?.FirstOrDefault() as DateTime? ?? this.LastFinished ?? new DateTime(1970, 01, 01);

                int n = 0;

                while (tr > ofs + 50 && !this.m_cancelRequested)
                {

                    if (patientService == null) break;
                    var entities = patientService.Query(e => e.StatusConceptKey != StatusKeys.Obsolete && e.ModifiedOn >= since, queryId, ofs, 50, out tr, AuthenticationContext.SystemPrincipal);

                    // Index 
                    this.m_tracer.TraceInfo("Index Job: Will index {0}..{1} of {2} objects", ofs, ofs + 50, tr);
                    ApplicationServiceContext.Current.GetService<SQLiteSearchIndexService>().IndexEntity(entities.ToArray());

                    // Let user know the status
                    ofs += 50;
                    ApplicationContext.Current.SetProgress(Strings.locale_indexing, (float)ofs / tr);
                }

                if (this.m_cancelRequested)
                    this.CurrentState = JobStateType.Cancelled;
                else
                    this.CurrentState = JobStateType.Completed;

            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error indexing primary database: {0}", e);
                this.CurrentState = JobStateType.Aborted;
                throw;
            }
            finally
            {
                this.LastFinished = DateTime.Now;
                this.m_cancelRequested = false;
            }

        }



    }
}
