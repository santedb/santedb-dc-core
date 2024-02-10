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
using SanteDB.Core.Configuration;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.AMI.Jobs;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// Represents a <see cref="IJobManagerService"/> which operates only on the upstream jobs service
    /// </summary>
    public class UpstreamJobManager : UpstreamServiceBase, IJobManagerService, IJobStateManagerService, IJobScheduleManager
    {
        private readonly IAdhocCacheService m_adhocCache;
        private readonly ILocalizationService m_localizationService;
        private const string CACHE_KEY = "$jobs";

        /// <summary>
        /// DI constructor
        /// </summary>
        public UpstreamJobManager(ILocalizationService localizationService,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IAdhocCacheService adhocCacheService,
            IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_adhocCache = adhocCacheService;
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Remote job schedule container
        /// </summary>
        private class UpstreamJobSchedule : IJobSchedule
        {

            /// <summary>
            /// Create remote scheduling information
            /// </summary>
            public UpstreamJobSchedule(JobScheduleInfo jobScheduleInfo)
            {
                this.Interval = jobScheduleInfo.IntervalXmlSpecified ? (TimeSpan?)jobScheduleInfo.Interval : null;
                this.StartTime = jobScheduleInfo.StartDate;
                this.StopTime = jobScheduleInfo.StopDateSpecified ? (DateTime?)jobScheduleInfo.StopDate : null;
                this.Days = jobScheduleInfo.RepeatOn;
                this.Type = jobScheduleInfo.Type;
            }
            /// <inheritdoc/>
            public TimeSpan? Interval { get; set; }

            /// <inheritdoc/>
            public DateTime StartTime { get; set; }

            /// <inheritdoc/>
            public DateTime? StopTime { get; set; }

            /// <inheritdoc/>
            public DayOfWeek[] Days { get; set; }

            /// <inheritdoc/>
            public JobScheduleType Type { get; set; }

            /// <inheritdoc/>
            public bool AppliesTo(DateTime checkTime, DateTime? lastExecutionTime)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// An upstream job implementation
        /// </summary>
        private class UpstreamJob : IJob, IIdentifiedResource, IJobState
        {
            /// <summary>
            /// Creates a new remote job from an AMI job info class
            /// </summary>
            public UpstreamJob(JobInfo job, IRestClientFactory restClientFactory, IAdhocCacheService adhocCacheService)
            {
                this.Key = job.Key;
                this.Tag = job.Tag;
                this.ModifiedOn = DateTime.Now;
                this.Name = job.Name;
                this.Description = job.Description;
                this.CanCancel = job.CanCancel;
                this.CurrentState = job.State;
                this.LastStartTime = job.LastStart;
                this.LastStopTime = job.LastFinish;
                this.Parameters = job.Parameters?.ToDictionary(o => o.Key, o => Type.GetType($"System.{o.Type}"));
                this.JobType = Type.GetType(job.JobType);
                this.StatusText = job.StatusText;
                this.Progress = job.Progress;
                this.Schedule = job.Schedule.Select(o => new UpstreamJobSchedule(o)).ToList();
                this.m_restClient = restClientFactory;
                this.m_adhocCache = adhocCacheService;
            }

            /// <summary>
            /// Gets the job of the job state
            /// </summary>
            public IJob Job => this;

            /// <summary>
            /// Get the identifier of this job
            /// </summary>
            public Guid Id => this.Key.Value;

            /// <summary>
            /// Gets the name of the job
            /// </summary>
            public string Name { get; }

            /// <inheritdoc/>
            public string Description { get; }

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
            public DateTime? LastStartTime { get; }

            /// <summary>
            /// Gets the time that the job was last finished
            /// </summary>
            public DateTime? LastStopTime { get; }

            /// <summary>
            /// Gets the remote id of the job
            /// </summary>
            public Guid? Key { get; set; }

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
            /// Gets the progress
            /// </summary>
            public float Progress { get; }

            /// <summary>
            /// Gets or sets the remote job schedule
            /// </summary>
            public IEnumerable<IJobSchedule> Schedule { get; set; }

            private readonly IRestClientFactory m_restClient;
            private readonly IAdhocCacheService m_adhocCache;

            /// <summary>
            /// Gets the status text
            /// </summary>
            public string StatusText { get; }

            /// <summary>
            /// Cancel the job
            /// </summary>
            public void Cancel()
            {
                if (this.CanCancel)
                    try
                    {
                        using (var client = this.m_restClient.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                        {
                            client.Post<ParameterCollection, ParameterCollection>($"JobInfo/{this.Key}/$cancel", new ParameterCollection());
                        }
                        this.m_adhocCache?.Remove(UpstreamJobManager.CACHE_KEY);
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
                try
                {
                    var parms = new ParameterCollection()
                    {
                        Parameters = parameters?.Select(o => new Parameter("_", o)).ToList()
                    };
                    using (var client = this.m_restClient.GetRestClientFor(Core.Interop.ServiceEndpointType.AdministrationIntegrationService))
                    {
                        client.Post<ParameterCollection, ParameterCollection>($"JobInfo/{this.Key}/$start", parms);
                    }
                    this.m_adhocCache?.Remove(UpstreamJobManager.CACHE_KEY);

                }
                catch (Exception ex)
                {
                    throw new Exception($"Error running job {this.Key}", ex);
                }
            }

        }

        /// <inheritdoc/>
        public IEnumerable<IJob> Jobs
        {
            get
            {
                try
                {
                    var jobs = this.m_adhocCache?.Get<UpstreamJob[]>(CACHE_KEY);
                    if (jobs != null)
                    {
                        return jobs;
                    }
                    using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                    {
                        jobs = client.Get<AmiCollection>(typeof(JobInfo).GetSerializationName()).CollectionItem.OfType<JobInfo>().Select(o => new UpstreamJob(o, this.RestClientFactory, this.m_adhocCache)).ToArray();

                        this.m_adhocCache?.Add(CACHE_KEY, jobs, new TimeSpan(0, 0, 1));
                        return jobs;
                    }
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(JobInfo) }), e);
                }
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Job and State Manager";

        /// <inheritdoc/>
        public void AddJob(IJob jobType, TimeSpan elapseTime, JobStartType startType = JobStartType.Immediate)
        {
            // Not supported - enforced by the upstream
        }

        /// <inheritdoc/>
        public void AddJob(IJob jobType, JobStartType startType = JobStartType.Immediate)
        {
            // Not supported - enforced by hte upstream
        }

        /// <inheritdoc/>
        public IJob GetJobInstance(Guid jobKey) => this.Jobs.FirstOrDefault(o => o.Id == jobKey);

        /// <inheritdoc/>
        public IJob GetJobInstance(Type jobType) => this.Jobs.OfType<UpstreamJob>().FirstOrDefault(o => o.JobType == jobType);

        /// <inheritdoc/>
        public IEnumerable<IJobSchedule> GetJobSchedules(IJob job) => (this.GetJobInstance(job.Id) as UpstreamJob).Schedule;

        /// <inheritdoc/>
        public IJobState GetJobState(IJob job) => this.GetJobInstance(job.Id) as UpstreamJob;

        /// <inheritdoc/>
        public bool IsJobRegistered(Type jobType) => this.Jobs.Any(o => o.GetType() == jobType);

        /// <inheritdoc/>
        public IJobSchedule SetJobSchedule(IJob job, DayOfWeek[] daysOfWeek, DateTime scheduleTime) => this.SetJobSchedule(job, daysOfWeek, scheduleTime, null);

        /// <summary>
        /// Set job schedule on the AMI
        /// </summary>
        private IJobSchedule SetJobSchedule(IJob job, DayOfWeek[] daysOfWeek, DateTime? scheduleTime, TimeSpan? intervalValue)
        {
            this.Clear(job);
            return this.Add(job, new UpstreamJobSchedule(new JobScheduleInfo()
            {
                RepeatOn = daysOfWeek,
                StartDate = scheduleTime.GetValueOrDefault(),
                Type = scheduleTime.HasValue ? JobScheduleType.Scheduled : JobScheduleType.Interval,
                Interval = intervalValue.GetValueOrDefault(),
                IntervalXmlSpecified = intervalValue.HasValue
            }));
        }

        /// <inheritdoc/>
        public IJobSchedule SetJobSchedule(IJob job, TimeSpan intervalSpan) => this.SetJobSchedule(job, null, null, intervalSpan);

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported</exception>
        public void SetProgress(IJob job, string statusText, float progress)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This method is not supported</exception>
        public void SetState(IJob job, JobStateType state, string statusText = null)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void StartJob(IJob job, object[] parameters)
        {
            if (job != null)
            {
                job.Run(this, EventArgs.Empty, parameters);
            }
        }

        /// <inheritdoc/>
        public void StartJob(Type jobType, object[] parameters) => this.Jobs.OfType<UpstreamJob>().FirstOrDefault(o => o.JobType == jobType)?.Run(this, EventArgs.Empty, parameters);

        /// <inheritdoc/>
        public void ClearJobSchedule(IJob job)
        {
            this.Clear(job);
        }

        /// <inheritdoc/>
        public IEnumerable<IJobSchedule> Get(IJob job) => (this.GetJobInstance(job.Id) as UpstreamJob).Schedule;

        /// <inheritdoc/>
        public void Clear(IJob job)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var jobInfo = client.Get<JobInfo>($"{typeof(JobInfo).GetSerializationName()}/{job.Id}");
                    jobInfo.Schedule = null;
                    jobInfo = client.Put<JobInfo, JobInfo>($"{typeof(JobInfo).GetSerializationName()}/{job.Id}", jobInfo);
                    this.m_adhocCache?.Remove(CACHE_KEY);

                }
            }
            catch (Exception e)
            {
                this._Tracer.TraceError("Error setting job schedule on remote server: {0}", e.Message);
                throw new Exception("Error setting job information on remote server", e);
            }
        }

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, IJobSchedule jobSchedule)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var jobInfo = client.Get<JobInfo>($"{typeof(JobInfo).GetSerializationName()}/{job.Id}");
                    jobInfo.Schedule = new List<JobScheduleInfo>()
                    {
                        new JobScheduleInfo()
                        {
                            Type = jobSchedule.Type,
                            RepeatOn = jobSchedule.Days,
                            StartDate = jobSchedule.StartTime,
                            Interval = jobSchedule.Interval.GetValueOrDefault(),
                            IntervalXmlSpecified = jobSchedule.Interval.HasValue
                        }
                    };
                    jobInfo = client.Put<JobInfo, JobInfo>($"{typeof(JobInfo).GetSerializationName()}/{job.Id}", jobInfo);
                    this.m_adhocCache?.Remove(CACHE_KEY);

                    return new UpstreamJobSchedule(jobInfo.Schedule.First());
                }
            }
            catch (Exception e)
            {
                this._Tracer.TraceError("Error setting job schedule on remote server: {0}", e.Message);
                throw new Exception("Error setting job information on remote server", e);
            }
        }

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, TimeSpan interval, DateTime? stopDate = null) =>
            this.Add(job, new UpstreamJobSchedule(new JobScheduleInfo()
            {
                Type = JobScheduleType.Interval,
                Interval = interval,
                IntervalXmlSpecified = true,
                StopDate = stopDate.GetValueOrDefault(),
                StopDateSpecified = stopDate.HasValue
            }));

        /// <inheritdoc/>
        public IJobSchedule Add(IJob job, DayOfWeek[] repeatOn, DateTime startDate, DateTime? stopDate = null) =>
            this.Add(job, new UpstreamJobSchedule(new JobScheduleInfo()
            {
                Type = JobScheduleType.Scheduled,
                StartDate = startDate,
                RepeatOn = repeatOn,
                StopDate = stopDate.GetValueOrDefault(),
                StopDateSpecified = stopDate.HasValue
            }));

        /// <inheritdoc/>
        public IJob RegisterJob(Type jobType)
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var jobInfo = client.Post<TypeReferenceConfiguration, JobInfo>(typeof(JobInfo).GetSerializationName(), new TypeReferenceConfiguration(jobType));
                    return new UpstreamJob(jobInfo, this.RestClientFactory, this.m_adhocCache);
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(JobInfo) }), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<Type> GetAvailableJobs()
        {
            try
            {
                using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, AuthenticationContext.Current.Principal))
                {
                    var unConfiguredJobs = client.Get<AmiCollection>(typeof(JobInfo).GetSerializationName(), "_unconfigured=true".ParseQueryString()).CollectionItem.OfType<TypeReferenceConfiguration>().ToArray();
                    return unConfiguredJobs.Select(o => o.Type).Union(this.Jobs.OfType<UpstreamJob>().Select(o => o.JobType));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = nameof(JobInfo) }), e);
            }
        }
    }
}
