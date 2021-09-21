/*
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
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.AMI;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Jobs;
using SanteDB.DisconnectedClient.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.Services.Remote
{
    /// <summary>
    /// A job management service which is just chained to the central server
    /// </summary>
    public class RemoteJobManager : IJobManagerService
    {
        /// <summary>
        /// Represents a remote job
        /// </summary>
        private class RemoteJob : IJob, IAmiIdentified
        {

            /// <summary>
            /// Creates a new remote job from an AMI job info class
            /// </summary>
            public RemoteJob(JobInfo job)
            {
                this.Key = job.Key;
                this.Tag = job.Tag;
                this.ModifiedOn = DateTime.Now;
                this.Name = job.Name;
                this.CanCancel = job.CanCancel;
                this.CurrentState = job.State;
                this.LastFinished = job.LastFinish;
                this.LastStarted = job.LastStart;
                this.Parameters = job.Parameters?.ToDictionary(o => o.Key, o => Type.GetType(o.Type));
                this.JobType = Type.GetType(job.JobType);
            }

            /// <summary>
            /// Get the identifier of this job
            /// </summary>
            public Guid Id => Guid.Parse(this.Key);

            /// <summary>
            /// Gets the name of the job
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets whether the job can be cancelled
            /// </summary>
            public bool CanCancel { get; }

            /// <summary>
            /// Gets the current state
            /// </summary>
            public JobStateType CurrentState { get; }

            /// <summary>
            /// Gets the parameters to the job
            /// </summary>
            public IDictionary<string, Type> Parameters { get; }

            /// <summary>
            /// Gets the time the job was last started
            /// </summary>
            public DateTime? LastStarted { get; }

            /// <summary>
            /// Gets the time that the job was last finished
            /// </summary>
            public DateTime? LastFinished { get; }

            /// <summary>
            /// Gets the remote id of the job
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// Gets the tag
            /// </summary>
            public string Tag { get; }

            /// <summary>
            /// Gets the last modified date
            /// </summary>
            public DateTimeOffset ModifiedOn { get; }

            /// <summary>
            /// Gets or sets the job type
            /// </summary>
            public Type JobType { get; }

            /// <summary>
            /// Cancel the job
            /// </summary>
            public void Cancel()
            {
                if (this.CanCancel)
                    try
                    {
                        using (var client = RemoteJobManager.GetRestClient())
                            client.Delete<JobInfo>($"JobInfo/{this.Key}");
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error canceling job {this.Key}", e);
                    }
            }

            /// <summary>
            /// Run the specified job
            /// </summary>
            public void Run(object sender, EventArgs e, object[] parameters)
            {
                if (parameters?.Length != this.Parameters?.Count)
                    throw new ArgumentException("Invalid number of arguments to job");
                else
                    try
                    {
                        using (var client = RemoteJobManager.GetRestClient())
                        {
                            var ji = new JobInfo(this);
                            if (ji.Parameters != null)
                                for (int i = 0; i < ji.Parameters.Count; i++)
                                    ji.Parameters[i].Value = parameters[i];
                            client.Put<JobInfo, JobInfo>($"JobInfo/{this.Key}", client.Accept, ji);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error running job {this.Key}", ex);
                    }
            }
        }

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(RemoteJobManager));

        /// <summary>
        /// Gets the rest client
        /// </summary>
        /// <returns></returns>
        private static IRestClient GetRestClient()
        {
            var retVal = ApplicationContext.Current.GetRestClient("ami");
            retVal.Accept = "application/xml";
            return retVal;
        }

        /// <summary>
        /// Gets the jobs which can be executed on the management service
        /// </summary>
        public IEnumerable<IJob> Jobs
        {
            get
            {
                try
                {
                    using (var client = GetRestClient())
                        return client.Get<AmiCollection>("JobInfo").CollectionItem.OfType<JobInfo>().Select(o => new RemoteJob(o)).ToList();
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error getting job information from remote server: {0}", e.Message);
                    throw new Exception("Error getting job information from remote server", e);
                }
            }
        }

        /// <summary>
        /// Returns true if the job manager is running
        /// </summary>
        public bool IsRunning => true;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Remote Job Manager";

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the service is started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Add a job to the job manager
        /// </summary>
        public void AddJob(IJob jobType, TimeSpan elapseTime, JobStartType jobStartType = JobStartType.Immediate)
        {
            // Not supported
        }

        /// <summary>
        /// Get the job instance
        /// </summary>
        public IJob GetJobInstance(Guid jobId)
        {
            return this.Jobs.OfType<RemoteJob>().FirstOrDefault(o => o.Id == jobId);
        }

        /// <summary>
        /// Get whether the job is registered
        /// </summary>
        public bool IsJobRegistered(Type jobType)
        {
            return this.Jobs.OfType<RemoteJob>().Any(o => o.JobType == jobType);
        }

        /// <summary>
        /// Start the service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.Started?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Start a job
        /// </summary>
        public void StartJob(IJob job, object[] parameters)
        {
            if (job != null)
                job.Run(this, EventArgs.Empty, parameters);
        }

        /// <summary>
        /// Start a job
        /// </summary>
        public void StartJob(Type job, object[] parameters)
        {
            var jobInfo = this.Jobs.OfType<RemoteJob>().FirstOrDefault(o => o.JobType == job);
            if (jobInfo != null)
                jobInfo.Run(this, EventArgs.Empty, parameters);
        }
        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
