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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using SanteDB.Core.Security;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Security;
using SanteDB.DisconnectedClient.Services;
using SanteDB.Messaging.HDSI.Client;

namespace SanteDB.DisconnectedClient.Interop.HDSI
{
    /// <summary>
    /// Represents an HDSI integration service which sends and retrieves data from the IMS.
    /// </summary>
    public class HdsiIntegrationService : IClinicalIntegrationService
    {
	    // Tests to remove due to the mobile / server auto-calculating them
	    private readonly string[] m_removePatchTest =
        {
            "creationTime",
            "obsoletionTime",
            "previousVersion",
            "sequence"
        };

	    // Cached credential
	    private IPrincipal m_cachedCredential;

	    // Last ping
	    private DateTime m_lastPing;

        // Known uuids from the server
        private HashSet<Guid> m_knownUuids = new HashSet<Guid>();

        // Options
        private ServiceOptions m_options;

	    // Have we let the user know?
	    private bool m_tickleSent;

	    // Tracer
	    private readonly Tracer m_tracer = Tracer.GetTracer(typeof(HdsiIntegrationService));

	    /// <summary>
        /// Gets the specified model object
        /// </summary>
        public Bundle Find(Type modelType, NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null)
        {
            try
            {
                var method = this.GetType().GetRuntimeMethod("Find", new[] { typeof(NameValueCollection), typeof(int), typeof(int?), typeof(IntegrationQueryOptions) }).MakeGenericMethod(modelType);
                return method.Invoke(this, new object[] { filter, offset, count, options }) as Bundle;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

	    /// <summary>
        /// Finds the specified model
        /// </summar>y
        public Bundle Find<TModel>(NameValueCollection filter, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            var predicate = QueryExpressionParser.BuildLinqExpression<TModel>(filter, null, false);
            return this.Find(predicate, offset, count, options);
        }

	    /// <summary>
        /// Finds the specified model
        /// </summary>
        public Bundle Find<TModel>(Expression<Func<TModel, bool>> predicate, int offset, int? count, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            try
            {
                if (this.IsAvailable())
                {
                    HdsiServiceClient client = this.GetServiceClient();
                    client.Client.Requesting += IntegrationQueryOptions.CreateRequestingHandler(options);
                    client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                    client.Client.Credentials = this.GetCredentials(client.Client);
                    if (client.Client.Credentials == null)
                    {
                        return null;
                    }

                    if (options?.Timeout.HasValue == true)
                    {
                        client.Client.Description.Endpoint[0].Timeout = options.Timeout.Value;
                    }

                    this.m_tracer.TraceVerbose("Performing HDSI query ({0}):{1}", typeof(TModel).FullName, predicate);

                    var retVal = client.Query<TModel>(predicate, offset, count, queryId: options?.QueryId);
                    this.Responded?.Invoke(this, new IntegrationResultEventArgs(null, retVal));
                    //retVal?.Reconstitute();
                    return retVal;
                }
                return null;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }


	    /// <summary>
        /// Gets the specified model object
        /// </summary>
        public IdentifiedData Get(Type modelType, Guid key, Guid? version, IntegrationQueryOptions options = null)
        {
            try
            {
                var method = this.GetType().GetRuntimeMethod("Get", new[] { typeof(Guid), typeof(Guid?), typeof(IntegrationQueryOptions) }).MakeGenericMethod(modelType);
                return method.Invoke(this, new object[] { key, version, options }) as IdentifiedData;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

	    /// <summary>
        /// Gets a specified model.
        /// </summary>
        /// <typeparam name="TModel">The type of model data to retrieve.</typeparam>
        /// <param name="key">The key of the model.</param>
        /// <param name="versionKey">The version key of the model.</param>
        /// <param name="options">The integrations query options.</param>
        /// <returns>Returns a model.</returns>
        public TModel Get<TModel>(Guid key, Guid? versionKey, IntegrationQueryOptions options = null) where TModel : IdentifiedData
        {
            try
            {
                HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                client.Client.Requesting += IntegrationQueryOptions.CreateRequestingHandler(options);
                client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                client.Client.Credentials = this.GetCredentials(client.Client);
                if (client.Client.Credentials == null)
                {
	                return null;
                }

                this.m_tracer.TraceVerbose("Performing HDSI GET ({0}):{1}v{2}", typeof(TModel).FullName, key, versionKey);
                var retVal = client.Get<TModel>(key, versionKey);

                if (retVal is Bundle)
                {
                    (retVal as Bundle)?.Reconstitute();
                    retVal = (retVal as Bundle).Entry;
                }

                var integrationEvent = new IntegrationResultEventArgs(null, retVal);
                this.Responded?.Invoke(this, integrationEvent);
                return integrationEvent.ResponseData as TModel;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

	    /// <summary>
        /// Get the difference between the server and this device's time
        /// </summary>
        public TimeSpan GetServerTimeDrift()
        {
            try
            {
                //var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                var networkInformationService = ApplicationContext.Current.GetService<INetworkInformationService>();
                if (networkInformationService.IsNetworkAvailable)
                {
                    HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(restClient);
                    client.Client.Credentials = new NullCredentials();
                    client.Client.Description.Endpoint[0].Timeout = 20000;
                    var drift = TimeSpan.Zero;
                    client.Client.Responded += (o, e) =>
                    {
                        if (e.Headers != null)
                        {
                            if (e.Headers.ContainsKey("X-GeneratedOn"))
                            {
	                            drift = DateTime.Parse(e.Headers["X-GeneratedOn"]).Subtract(DateTime.Now);
                            }
                            else if (DateTime.TryParse(e.Headers["Date"], out var serverTime))
                            {
	                            drift = serverTime.Subtract(DateTime.Now);
                            }
                        }
                    };
                    client.Ping();
                    return drift;
                }

                return TimeSpan.Zero;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError($"Unable to determine server time drift: {e}");
                return TimeSpan.Zero;
            }
        }

	    /// <summary>
        /// Inserts specified data.
        /// </summary>
        /// <param name="data">The data to be inserted.</param>
        public void Insert(IdentifiedData data)
        {
            try
            {
                if (!(data is Bundle || data is Entity || data is Act))
                {
	                return;
                }

                if (data is Bundle)
                {
	                data.Key = null;
                }

                HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                client.Client.Credentials = this.GetCredentials(client.Client);
                client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                if (client.Client.Credentials == null)
                {
	                return;
                }

                // Special case = Batch submit of data with an entry point
                var submission = (data as Bundle)?.Entry ?? data;
                client.Client.Requesting += (o, e) =>
                {
                    var bund = e.Body as Bundle;
                    if (!(bund?.Entry is UserEntity)) // not submitting a user entity so we only submit ACT
                    {
	                    bund?.Item.RemoveAll(i => !(i is Act || i is Person && !(i is UserEntity)));// || i is EntityRelationship));
                    }

                    if (bund != null)
                    {
                        bund.Key = Guid.NewGuid();
                        e.Cancel = bund.Item.Count == 0;
                    }

                };

                // Create method
                var method = typeof(HdsiServiceClient).GetRuntimeMethods().FirstOrDefault(o => o.Name == "Create" && o.GetParameters().Length == 1);
                method = method.MakeGenericMethod(submission.GetType());

                this.m_tracer.TraceVerbose("Performing HDSI INSERT {0}", submission);
                var result = method.Invoke(client, new object[] { submission }) as IdentifiedData;

                if (result is IVersionedEntity iver)
                {
	                this.UpdateToServerCopy(iver, data as IVersionedEntity);
                }
                else if(result is Bundle bundle)
                {
                    var submissionBundle = data as Bundle;
                    foreach(var itm in bundle.Item)
                    {
                        var original = submissionBundle.Item.FirstOrDefault(o => o.Key == itm.Key);
                        if(itm is IVersionedEntity itmVer && original is IVersionedEntity)
                        {
	                        this.UpdateToServerCopy(itmVer, original as IVersionedEntity);
                        }
                    }
                }
                this.Responded?.Invoke(this, new IntegrationResultEventArgs(data, result));
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e.InnerException) as Exception;
            }
        }

	    /// <summary>
        /// Determines whether the network is available.
        /// </summary>
        /// <returns>Returns true if the network is available.</returns>
        public bool IsAvailable()
        {
            try
            {
                //var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                var networkInformationService = ApplicationContext.Current.GetService<INetworkInformationService>();
                if (networkInformationService.IsNetworkAvailable)
                {
	                if (this.m_lastPing < DateTime.Now.AddSeconds(-30))
                    {
                        HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(restClient);
                        client.Client.Credentials = new NullCredentials();
                        client.Client.Description.Endpoint[0].Timeout = 5000;
                        this.m_lastPing = DateTime.Now;
                        return this.IsValidVersion(client) &&
                            client.Ping();
                    }

	                return true;
                }

                return false;
            }
            catch (Exception e)
            {
                this.m_tracer.TraceInfo($"Unable to determine network state: {e}");
                return false;
            }
        }

	    /// <summary>
        /// Obsoletes specified data.
        /// </summary>
        /// <param name="data">The data to be obsoleted.</param>
        public void Obsolete(IdentifiedData data, bool unsafeObsolete = false)
        {
            try
            {

                if (!(data is Bundle || data is Entity || data is Act || data is EntityRelationship)) // || data is EntityRelationship))
                {
	                return;
                }

                HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                client.Client.Credentials = this.GetCredentials(client.Client);
                client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                if (client.Client.Credentials == null)
                {
	                return;
                }

                // Force an update
                if (unsafeObsolete)
                {
	                client.Client.Requesting += (o, e) => e.AdditionalHeaders["X-SanteDB-Unsafe"] = "true";
                }
                else
                {
	                client.Client.Requesting += (o, e) => e.AdditionalHeaders["If-Match"] = data.Tag;
                }

                var method = typeof(HdsiServiceClient).GetRuntimeMethods().FirstOrDefault(o => o.Name == "Obsolete" && o.GetParameters().Length == 1);
                method = method.MakeGenericMethod(data.GetType());
                this.m_tracer.TraceVerbose("Performing HDSI OBSOLETE {0}", data);

                var iver = method.Invoke(client, new object[] { data }) as IVersionedEntity;
                if (iver != null)
                {
	                this.UpdateToServerCopy(iver, data as IVersionedEntity);
                }

                // Indicate that the server has responded
                this.Responded?.Invoke(this, new IntegrationResultEventArgs(data, iver as IdentifiedData));

            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

	    /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

	    /// <summary>
        /// The server has responded to the request
        /// </summary>
        public event EventHandler<IntegrationResultEventArgs> Responded;

	    /// <summary>
        /// Fired on response
        /// </summary>
        public event EventHandler<RestResponseEventArgs> Responding;

	    /// <summary>
        /// Service name
        /// </summary>
        public string ServiceName => "HDSI Clinical Integration Service";

	    /// <summary>
        /// Updates specified data.
        /// </summary>
        /// <param name="data">The data to be updated.</param>
        public void Update(IdentifiedData data, bool unsafeUpdate = false)
        {
            try
            {

                // HACK
                if (!(data is Bundle || data is Entity || data is Act || data is Patch))
                {
	                return;
                }

                if (data is Patch &&
                    !typeof(Entity).GetTypeInfo().IsAssignableFrom((data as Patch).AppliesTo.Type.GetTypeInfo()) &&
                    !typeof(Act).GetTypeInfo().IsAssignableFrom((data as Patch).AppliesTo.Type.GetTypeInfo()))
                {
	                return;
                }

                HdsiServiceClient client = this.GetServiceClient(); //new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
                client.Client.Credentials = this.GetCredentials(client.Client);
                client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                if (client.Client.Credentials == null)
                {
	                return;
                }

                // Force an update
                if (unsafeUpdate)
                {
	                client.Client.Requesting += (o, e) => e.AdditionalHeaders["X-Patch-Force"] = "true";
                }

                // Special case = Batch submit of data with an entry point
                var submission = (data as Bundle)?.Entry ?? data;
                var existing = submission;
                
                // Assign a uuid for this submission
                if (data is Bundle && data.Key == null)
                {
	                data.Key = Guid.NewGuid();
                }

                // TODO: In MDM mode on the server patching will def cause an issue as we don't process the location header sent back from the server
                // we need to update the server to send back an appropriate location header and to fetch the local.
                if (submission is Patch)
                {
                    var patch = submission as Patch;

                    // Patch for update on times (obsolete, creation time, etc. always fail so lets remove them)
                    patch.Operation.RemoveAll(o => o.OperationType == PatchOperationType.Test && this.m_removePatchTest.Contains(o.Path));
                    this.m_tracer.TraceVerbose("Performing HDSI UPDATE (PATCH) {0}", patch);

                    var existingKey = patch.AppliesTo.Key;
                    // Get the object and then update
                    var idp = typeof(IDataPersistenceService<>).MakeGenericType(patch.AppliesTo.Type);
                    var idpService = ApplicationContext.Current.GetService(idp) as IDataPersistenceService;
                    existing = idpService.Get(existingKey.Value) as IdentifiedData;
                    var newUuid = Guid.Empty;

                    try
                    {
                        newUuid = client.Patch(patch);
                    }
                    catch (WebException e)
                    {

                        switch ((e.Response as HttpWebResponse).StatusCode)
                        {
                            case HttpStatusCode.Conflict: // Try to resolve the conflict in an automated way
                                this.m_tracer.TraceWarning("Will attempt to force PATCH {0}", patch);

                                // Condition 1: Can we apply the patch without causing any issues (ignoring version)
                                client.Client.Requesting += (o, evt) =>
                                {
                                    evt.AdditionalHeaders["X-Patch-Force"] = "true";
                                };

                                // Configuration dictates only safe patch
                                if (ApplicationContext.Current.Configuration.GetSection<SynchronizationConfigurationSection>().SafePatchOnly)
                                {
                                    // First, let's grab the item
                                    var serverCopy = this.Get(patch.AppliesTo.Type, patch.AppliesTo.Key.Value, null);
                                    if (ApplicationContext.Current.GetService<IPatchService>().Test(patch, serverCopy))
                                    {
	                                    newUuid = client.Patch(patch);
                                    }
                                    else
                                    {
                                        // There are no intersections of properties between the object we have and the server copy
                                        var serverDiff = ApplicationContext.Current.GetService<IPatchService>().Diff(existing, serverCopy);
                                        if (!serverDiff.Operation.Any(sd => patch.Operation.Any(po => po.Path == sd.Path && sd.OperationType != PatchOperationType.Test)))
                                        {
	                                        newUuid = client.Patch(patch);
                                        }
                                        else
                                        {
	                                        throw;
                                        }
                                    }
                                }
                                else /// unsafe patch ... meh
                                {
	                                newUuid = client.Patch(patch);
                                }

                                break;
                            case HttpStatusCode.NotFound: // We tried to update something that doesn't exist on the server? That's odd
                                this.m_tracer.TraceWarning("Server reported patch target doesn't exist! {0}", patch);
                                var svcType = typeof(IDataPersistenceService<>).MakeGenericType(patch.AppliesTo.Type);
                                var persistenceService = ApplicationContext.Current.GetService(svcType) as IDataPersistenceService;
                                var localObject = persistenceService.Get(patch.AppliesTo.Key.Value);

                                // Re-queue for create
                                // First, we have to remove the "replaces version" key as it doesn't make much sense
                                if (localObject is IVersionedEntity)
                                {
	                                (localObject as IVersionedEntity).PreviousVersionKey = null;
                                }

                                this.Insert(Bundle.CreateBundle(localObject as IdentifiedData));
                                break;
                        }
                    }

                    // Update the local version key to the server version key
                    if (existing is IVersionedEntity iver)
                    {
                        this.m_tracer.TraceVerbose("Patch successful - VersionId of {0} to {1}", existing, newUuid);
                        iver.VersionKey = newUuid;
                        idpService.Update(existing);
                    }

                }
                else // regular update 
                {
                    // Force an update
                    if (!unsafeUpdate)
                    {
	                    client.Client.Requesting += (o, e) => e.AdditionalHeaders["If-Match"] = data.Tag;
                    }

                    client.Client.Requesting += (o, e) => (e.Body as Bundle)?.Item.RemoveAll(i => !(i is Act || i is Patient || i is Provider || i is UserEntity)); // || i is EntityRelationship));

                    var method = typeof(HdsiServiceClient).GetRuntimeMethods().FirstOrDefault(o => o.Name == "Update" && o.GetParameters().Length == 1);
                    method = method.MakeGenericMethod(submission.GetType());
                    
                    this.m_tracer.TraceVerbose("Performing HDSI UPDATE (FULL) {0}", data);

                    var iver = method.Invoke(client, new object[] { submission }) as IVersionedEntity;
                    if (iver != null)
                    {
	                    this.UpdateToServerCopy(iver, submission as IVersionedEntity);
                    }

                    // Notify updated
                    this.Responded?.Invoke(this, new IntegrationResultEventArgs(existing, iver as IdentifiedData));
                }

            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }

        }

	    /// <summary>
        /// Gets current credentials
        /// </summary>
        private Credentials GetCredentials(IRestClient client)
        {
            try
            {
                var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();

                if(this.m_cachedCredential == null ||
                    !(this.m_cachedCredential is TokenClaimsPrincipal) ||this.m_cachedCredential is IClaimsPrincipal claimsPrincipal && 
                    (claimsPrincipal.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                {
	                this.m_cachedCredential = ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
                }

                return client.Description.Binding.Security.CredentialProvider.GetCredentials(this.m_cachedCredential);
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error authentication for synchronization: {0}", e);
                throw new SecurityException("Error authenticating for synchronization. Perhaps the device has been reconfigured?", e);
            }
        }

	    /// <summary>
        /// Get service client
        /// </summary>
        private HdsiServiceClient GetServiceClient()
        {
            var retVal = new HdsiServiceClient(ApplicationContext.Current.GetRestClient("hdsi"));
            retVal.Client.Accept = "application/xml";
            return retVal;
        }

	    /// <summary>
        /// Throws an exception if the specified service client has the invalid version
        /// </summary>
        private bool IsValidVersion(HdsiServiceClient client)
        {
            var expectedVersion = typeof(IdentifiedData).GetTypeInfo().Assembly.GetName().Version;
            if (this.m_options == null)
            {
	            this.m_options = client.Options();
            }

            if (this.m_options == null)
            {
	            return false;
            }

            var version = new Version(this.m_options.InterfaceVersion);
            // Major version must match & minor version must match. Example:
            // Server           Client          Result
            // 0.6.14.*         0.6.14.*        Compatible
            // 0.7.0.*          0.6.14.*        Not compatible (server newer)
            // 0.7.0.*          0.9.0.0         Compatible (client newer)
            // 0.8.0.*          1.0.0.0         Not compatible (major version mis-match)
            this.m_tracer.TraceVerbose("HDSI server indicates version {0}", this.m_options.InterfaceVersion);
            return version < expectedVersion;
        }

	    /// <summary>
        /// Update the version identifier to the server identifier
        /// </summary>
        public void UpdateToServerCopy(IVersionedEntity newData, IVersionedEntity submittedData)
        {
            this.m_tracer.TraceVerbose("Updating to remote version {0}", newData);

            // Update the ETag of the current version
            var idp = typeof(IDataPersistenceService<>).MakeGenericType(newData.GetType());
            var idpService = ApplicationContext.Current.GetService(idp) as IDataPersistenceService;

            if (idpService != null)
            {
                submittedData.VersionKey = newData.VersionKey;
                submittedData.VersionSequence = newData.VersionSequence;
                submittedData.PreviousVersionKey = newData.PreviousVersionKey;
                idpService.Update(submittedData);
            }
        }

        /// <summary>
        /// PErform a HEAD operation
        /// </summary>
        public bool Exists<TModel>(Guid key)
        {
            try
            {
                if (m_knownUuids.Contains(key)) return true; // Already tested
                else if (m_knownUuids.Count > 1000) m_knownUuids.Clear();

                HdsiServiceClient client = this.GetServiceClient(); //new ImsiServiceClient(ApplicationContext.Current.GetRestClient("imsi"));
                client.Client.Responding += (o, e) => this.Responding?.Invoke(o, e);
                client.Client.Credentials = this.GetCredentials(client.Client);
                if (client.Client.Credentials == null) return false;

                this.m_tracer.TraceVerbose("Performing HDSI HEAD ({0}):{1}", typeof(TModel).FullName, key);
                var retVal = client.Client.Head($"{typeof(TModel).Name}/{key}");

                if (retVal.ContainsKey("ETag"))
                    m_knownUuids.Add(key);
                return retVal.ContainsKey("ETag");
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}