using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Represents a local implementation of a job management service
    /// </summary>
    public class LocalJobManagerService : IJobManagerService
    {

        // TRacer
        private Tracer m_tracer = Tracer.GetTracer(typeof(LocalJobManagerService));

        // Jobs to be run
        private List<IJob> m_jobs = null;

        // Lock object 
        private object m_lock = new object();

        /// <summary>
        /// Get the jobs currently registered to this manager
        /// </summary>
        public IEnumerable<IJob> Jobs => this.m_jobs;

        /// <summary>
        /// True if the job service is running
        /// </summary>
        public bool IsRunning => this.m_jobs != null;

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "Local Job Management Service";

        /// <summary>
        /// Fired when this service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when this service has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when this service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when this service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Throw an exception if the service is not started
        /// </summary>
        private void ThrowIfNotStarted()
        {
            if (!this.IsRunning)
                throw new InvalidOperationException("Service is not started");
        }

        /// <summary>
        /// Run a background job
        /// </summary>
        /// <param name="o"></param>
        private void RunJobBackground(Object o)
        {
            // Service is not running, so quit
            if (!this.IsRunning)
            {
                this.m_tracer.TraceInfo("Not running job as host job manager service has stopped");
                return;
            }

            var dynParm = (dynamic)o;
            var job = (IJob)dynParm.Job;
            try
            {
                this.m_tracer.TraceInfo("Starting timed job {0}...", job.Name);
                job.Run(this, EventArgs.Empty, null);
                this.m_tracer.TraceInfo("Job {0} completed...", job.Name);
                // Re-queue
                if (dynParm.Elapse != default(TimeSpan))
                    ApplicationServiceContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem((TimeSpan)dynParm.Elapse, this.RunJobBackground, o);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error running job {0}, it will no longer be run : {1}", job.Name, e);
            }
        }

        /// <summary>
        /// Add a job to this service
        /// </summary>
        public void AddJob(IJob jobType, TimeSpan elapseTime)
        {
            this.ThrowIfNotStarted();

            // Add job
            lock (this.m_lock)
                if (this.m_jobs.Any(o => o.GetType() == jobType.GetType()))
                    throw new InvalidOperationException("Job is already registered");
                else
                    this.m_jobs.Add(jobType);

            // Run job
            ApplicationServiceContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(this.RunJobBackground, new { Job = jobType, Elapse = elapseTime });

        }

        /// <summary>
        /// Get a job instance
        /// </summary>
        public IJob GetJobInstance(string jobTypeName)
        {
            this.ThrowIfNotStarted();
            return this.m_jobs.FirstOrDefault(o => o.GetType().FullName == jobTypeName);
        }

        /// <summary>
        /// True if this job type is registered
        /// </summary>
        public bool IsJobRegistered(Type jobType)
        {
            this.ThrowIfNotStarted();
            return this.m_jobs.Any(o => o.GetType() == jobType);
        }

        /// <summary>
        /// Start this service
        /// </summary>
        public bool Start()
        {
            // Start job service
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_jobs = new List<IJob>();

            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Start a job
        /// </summary>
        public void StartJob(IJob job, object[] parameters)
        {
            this.ThrowIfNotStarted();
            ApplicationServiceContext.Current.GetService<IThreadPoolService>().QueueUserWorkItem(this.RunJobBackground, new { Job = job, Elapse = default(TimeSpan) });
        }

        /// <summary>
        /// Stop the specified job service
        /// </summary>
        public bool Stop()
        {
            // Start job service
            this.Stopping?.Invoke(this, EventArgs.Empty);

            foreach (var itm in this.m_jobs.OfType<IDisposable>())
                itm.Dispose();

            this.m_jobs = null;

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}
