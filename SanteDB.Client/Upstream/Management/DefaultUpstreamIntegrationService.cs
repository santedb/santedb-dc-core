/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 */
using SanteDB.Client.Configuration.Upstream;
using SanteDB.Client.Exceptions;
using SanteDB.Client.Http;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.HDSI.Client;
using SanteDB.Rest.Common;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using ZXing.OneD;

namespace SanteDB.Client.Upstream.Management
{
    /// <summary>
    /// An upstream integration service
    /// </summary>
    public class DefaultUpstreamIntegrationService : IUpstreamIntegrationService
    {

        /// <summary>
        /// A certificate principal
        /// </summary>
        private class CertificatePrincipal : SanteDBClaimsPrincipal, ICertificatePrincipal
        {
            /// <summary>
            /// Create a new basic header for this 
            /// </summary>
            public CertificatePrincipal(UpstreamCredentialConfiguration credentialConfiguration)
            {
                if (credentialConfiguration.Conveyance != UpstreamCredentialConveyance.ClientCertificate)
                {
                    throw new ArgumentOutOfRangeException(nameof(credentialConfiguration));
                }
                this.AddIdentity(new SanteDBClaimsIdentity(credentialConfiguration.CredentialName, true, "NONE"));
                this.AuthenticationCertificate = credentialConfiguration.CertificateSecret.Certificate;
            }

            /// <inheritdoc/>
            public X509Certificate2 AuthenticationCertificate { get; }
        }

        /// <summary>
        /// Represents a device principal which is used to represent basic authentication context information
        /// </summary>
        private class HttpBasicTokenPrincipal : SanteDBClaimsPrincipal, ITokenPrincipal
        {

            /// <summary>
            /// Create a new basic header for this 
            /// </summary>
            public HttpBasicTokenPrincipal(UpstreamCredentialConfiguration credentialConfiguration)
            {
                if (credentialConfiguration.Conveyance == UpstreamCredentialConveyance.ClientCertificate)
                {
                    throw new ArgumentOutOfRangeException(nameof(credentialConfiguration));
                }
                this.AddIdentity(new SanteDBClaimsIdentity(credentialConfiguration.CredentialName, true, "NONE"));
                this.TokenType = "BASIC";
                this.AccessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentialConfiguration.CredentialName}:{credentialConfiguration.CredentialSecret}"));
                this.ExpiresAt = DateTimeOffset.Now.AddSeconds(10);
            }

            /// <inheritdoc/>
            public string AccessToken { get; }

            /// <inheritdoc/>
            public string TokenType { get; }

            /// <inheritdoc/>
            public DateTimeOffset ExpiresAt { get; }

            /// <inheritdoc/>
            public string IdentityToken => null;

            /// <inheritdoc/>
            public string RefreshToken => null;

        }

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(DefaultUpstreamIntegrationService));
        private readonly IRestClientFactory m_restClientFactory;
        private readonly IUpstreamManagementService m_upstreamManager;
        private readonly UpstreamConfigurationSection m_configuration;
        private readonly IPatchService m_patchService;
        private readonly ILocalizationService m_localizationService;
        private readonly INetworkInformationService m_networkInformationService;
        private readonly IServiceManager m_serviceManager;
        private readonly IAdhocCacheService m_adhocCache;
        private ConcurrentDictionary<String, Guid> s_templateKeys = new ConcurrentDictionary<string, Guid>();
        private ITokenPrincipal m_devicePrincipal;

        /// <inheritdoc/>
        public string ServiceName => "Upstream Data Provider";

#pragma warning disable CS0067
        /// <inheritdoc/>
        public event EventHandler<RestResponseEventArgs> Responding;
        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        /// <inheritdoc/>
        public event EventHandler<UpstreamIntegrationResultEventArgs> Responded;
#pragma warning restore

        /// <summary>
        /// DI constructor
        /// </summary>
        public DefaultUpstreamIntegrationService(IRestClientFactory restClientFactory,
            INetworkInformationService networkInformationService,
            IConfigurationManager configurationManager,
            IServiceManager serviceManager,
            IUpstreamManagementService upstreamManagementService,
            IPatchService patchService,
            IAdhocCacheService adhocCacheService,
            ILocalizationService localizationService)
        {
            this.m_configuration = configurationManager.GetSection<UpstreamConfigurationSection>();
            this.m_patchService = patchService;
            this.m_restClientFactory = restClientFactory;
            this.m_upstreamManager = upstreamManagementService;
            this.m_localizationService = localizationService;
            this.m_networkInformationService = networkInformationService;
            this.m_serviceManager = serviceManager;
            this.m_adhocCache = adhocCacheService;
        }

        private IRepositoryService GetRepositoryService(Type modelType)
        {
            var repositorytype = typeof(IRepositoryService<>).MakeGenericType(modelType);

            return m_serviceManager.CreateInjected(repositorytype) as IRepositoryService;
        }

        /// <inheritdoc/>
        public IResourceCollection Query(Type modelType, Expression filter, UpstreamIntegrationQueryControlOptions queryControl)
        {
            try
            {
                var filterType = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(modelType, typeof(bool)));
                var method = this.GetType().GetGenericMethod(nameof(Query), new[] { modelType }, new[] { filterType, typeof(UpstreamIntegrationQueryControlOptions) });
                return method.Invoke(this, new object[] { filter, queryControl }) as IResourceCollection;
            }
            catch (TargetInvocationException e)
            {
                throw Activator.CreateInstance(e.InnerException.GetType(), "Error performing action", e) as Exception;
            }
        }

        /// <inheritdoc/>
        public IResourceCollection Query<TModel>(Expression<Func<TModel, bool>> predicate, UpstreamIntegrationQueryControlOptions queryControl) where TModel : IdentifiedData, new()
        {
            try
            {
                var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint<TModel>();
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    this.m_tracer.TraceVerbose("Querying upstream as device {0}/{1}...", typeof(TModel).GetSerializationName(), predicate);
                    var query = QueryExpressionBuilder.BuildQuery(predicate);
                    query.Add(QueryControlParameterNames.HttpCountParameterName, (queryControl?.Count ?? 100).ToString());
                    query.Add(QueryControlParameterNames.HttpOffsetParameterName, (queryControl?.Offset ?? 0).ToString());
                    if (queryControl?.QueryId != null)
                    {
                        query.Add(QueryControlParameterNames.HttpQueryStateParameterName, queryControl?.QueryId.ToString());
                    }
                    client.Credentials = new UpstreamPrincipalCredentials(AuthenticationContext.Current.Principal);

                    client.Requesting += (o, e) =>
                    {
                        if (queryControl?.IfModifiedSince.HasValue == true)
                        {
                            e.AdditionalHeaders[HttpRequestHeader.IfModifiedSince] = queryControl?.IfModifiedSince.Value.ToString();
                        }
                        else if (!String.IsNullOrEmpty(queryControl?.IfNoneMatch))
                        {
                            e.AdditionalHeaders[HttpRequestHeader.IfNoneMatch] = queryControl?.IfNoneMatch;
                        }

                        if (queryControl?.IncludeRelatedInformation == true)
                        {
                            e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.IncludeRelatedObjectsHeader, "true");
                        }
                    };
                    client.Responding += (o, e) => this.Responding?.Invoke(this, e);

                    if (queryControl?.Timeout.HasValue == true)
                    {
                        client.SetTimeout(queryControl.Timeout.Value);
                    }

                    object retVal = null;
                    switch (upstreamService)
                    {
                        case ServiceEndpointType.HealthDataService:
                            retVal = client.Get<Bundle>($"/{typeof(TModel).GetSerializationName()}", query);
                            break;
                        case ServiceEndpointType.AdministrationIntegrationService:
                            retVal = client.Get<AmiCollection>($"/{typeof(TModel).GetSerializationName()}", query);
                            break;
                        default:
                            throw new InvalidOperationException(ErrorMessages.SERVICE_NOT_FOUND);
                    }

                    this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(null, retVal as IdentifiedData));
                    return retVal as IResourceCollection;

                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = typeof(TModel).GetSerializationName() }), e);
            }
        }

        /// <inheritdoc/>
        public IdentifiedData Get(Type modelType, Guid key, Guid? versionKey, UpstreamIntegrationQueryControlOptions options = null)
        {
            try
            {
                var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(modelType);
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    this.m_tracer.TraceVerbose("Fetching upstream as device {0}/{1}", modelType.GetSerializationName(), key);
                    client.Requesting += (o, e) =>
                    {

                        if (options?.IfModifiedSince.HasValue == true)
                        {
                            e.AdditionalHeaders[HttpRequestHeader.IfModifiedSince] = options?.IfModifiedSince.Value.ToString();
                        }
                        else if (!String.IsNullOrEmpty(options?.IfNoneMatch))
                        {
                            e.AdditionalHeaders[HttpRequestHeader.IfNoneMatch] = options?.IfNoneMatch;
                        }

                        if (options?.IncludeRelatedInformation == true)
                        {
                            e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.IncludeRelatedObjectsHeader, "true");
                        }
                    };
                    client.Responding += (o, e) => this.Responding?.Invoke(this, e);

                    if (options?.Timeout.HasValue == true)
                    {
                        client.SetTimeout(options.Timeout.Value);
                    }

                    var requestTarget = $"{modelType.GetSerializationName()}/{key}";
                    if (versionKey.HasValue)
                    {
                        requestTarget += $"/_history/{versionKey}";
                    }

                    var retVal = client.Get<IdentifiedData>(requestTarget);
                    switch (retVal)
                    {
                        case Bundle bdl:
                            retVal = bdl.GetFocalObject();
                            break;
                    }

                    this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(null, retVal));
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = modelType.GetSerializationName() }), e);
            }
        }

        /// <inheritdoc/>
        public TModel Get<TModel>(Guid key, Guid? versionKey, UpstreamIntegrationQueryControlOptions options = null) where TModel : IdentifiedData => (TModel)this.Get(typeof(TModel), key, versionKey, options);

        /// <summary>
        /// Harmonizes the keys on the server version with this version
        /// </summary>
        /// <param name="received"></param>
        /// <param name="submitted"></param>
        private void UpdateToServerCopy(IdentifiedData received, IdentifiedData submitted)
        {
            if (received.BatchOperation == BatchOperationType.Delete || 
                received.BatchOperation == BatchOperationType.Ignore)
            {
                return;
            }

                switch (received)
            {
                case IVersionedData receivedVersioned:
                    var submittedVersioned = submitted as IVersionedData;

                    // Did the server change the version?
                    if(submittedVersioned.VersionKey == receivedVersioned.VersionKey)
                    {
                        return;
                    }

                    this.m_tracer.TraceVerbose("Updating {0} to server version {1}", submitted, received);
                    var idp = ApplicationServiceContext.Current.GetService(typeof(IDataPersistenceService<>).MakeGenericType(submittedVersioned.GetType())) as IDataPersistenceService;
                    if (idp != null)
                    {
                        submittedVersioned.VersionKey = receivedVersioned.VersionKey;
                        submittedVersioned.VersionSequence = receivedVersioned.VersionSequence;
                        submittedVersioned.PreviousVersionKey = receivedVersioned.PreviousVersionKey;
                        idp.Update(submitted);
                    }
                    break;
                case Bundle receivedBundle:
                    var submittedBundle = submitted as Bundle;
                    foreach (var itm in submittedBundle.Item)
                    {
                        this.UpdateToServerCopy(itm, submittedBundle.Item.Find(o => o.Key == itm.Key));
                    }
                    break;
            }
        }

        /// <inheritdoc/>
        public void Insert(IdentifiedData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {

                if (data is Bundle bdl)
                {
                    if (!bdl.Item.Any())
                    {
                        return; // no need to send an empty bundle
                    }
                    else if (bdl.CorrelationKey.HasValue)
                    {
                        data = bdl.WithCorrelationControl(bdl.CorrelationKey.Value);
                    }
                }

                // create the appropriate message
                var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(data.GetType());
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    this.m_tracer.TraceVerbose("Pushing upstream as device {0}/{1}", data.GetType().GetSerializationName(), data.Key);

                    client.Requesting += (o, e) => e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.NoResponse, "true"); // The subscription will just download this data again later so no need for callback - save the bandwidth
                    client.Responding += (o, e) => this.Responding?.Invoke(this, e);

                    // Submit the object
                    var serverResponse = client.Post<IdentifiedData, IdentifiedData>(data.GetType().GetSerializationName(), data);
                    if (serverResponse != null)
                    {
                        this.UpdateToServerCopy(serverResponse, data);
                    }
                    this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(data, serverResponse));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = data.ToString() }), e);
            }
        }


        /// <inheritdoc/>
        public void Obsolete(IdentifiedData data, bool forceObsolete = false)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                // Delete the data on the server
                var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(data.GetType());
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    client.Requesting += (o, e) => e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.NoResponse, "true"); // The subscription will just download this data again later so no need for callback - save the bandwidth

                    switch (data)
                    {
                        case Bundle bdl:
                            {
                                if (!bdl.Item.Any()) { return; }
                                else if(bdl.CorrelationKey.HasValue)
                                {
                                    bdl = bdl.WithCorrelationControl(bdl.CorrelationKey.Value);
                                }
                                bdl.Item.ForEach(i => i.BatchOperation = BatchOperationType.Delete);
                                var serverResponse = client.Post<Bundle, Bundle>($"{typeof(Bundle).GetSerializationName()}", bdl);
                                if (serverResponse != null)
                                {
                                    this.UpdateToServerCopy(serverResponse, bdl);
                                }
                                this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(bdl, serverResponse));
                                break;
                            }
                        case IdentifiedData id:
                            {
                                IdentifiedData response = null;
                                EventHandler<RestRequestEventArgs> versionCheck = null;
                                try
                                {
                                    if (id is IVersionedData ivd && !forceObsolete)
                                    {
                                        versionCheck = (o, e) =>
                                        {
                                            e.AdditionalHeaders.Add(HttpRequestHeader.IfMatch, $"{id.Type}.{ivd.PreviousVersionKey}");
                                        };
                                        client.Requesting += versionCheck;
                                    }
                                    response = client.Delete<IdentifiedData>($"{data.GetType().GetSerializationName()}/{id.Key}");
                                }
                                catch (WebException we) when (we.Response is HttpWebResponse hwr && hwr.StatusCode == HttpStatusCode.Conflict)
                                {
                                    // Attempt to fetch the current object (if we can) and see if it is just a version mismatch on our update - i.e. no conflicts
                                    if (!(data is Bundle))
                                    {
                                        var serverCopy = this.Get(data.GetType(), data.Key.Value, null);
                                        var patch = this.m_patchService.Diff(serverCopy, data);
                                        if (this.m_patchService.Test(patch, serverCopy)) // There is no issue - so just apply the patch locally and resubmit result to the server
                                        {
                                            client.Requesting -= versionCheck;
                                            response = client.Delete<IdentifiedData>($"{data.GetType().GetSerializationName()}/{data.Key}");
                                        }
                                    }
                                }

                                if (response != null)
                                {
                                    this.UpdateToServerCopy(response, id);
                                }
                                this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(id, response));

                                break;
                            }
                        default:
                            throw new NotSupportedException();
                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = data.Type }), e);
            }

        }

        /// <inheritdoc/>
        public void Update(IdentifiedData data, bool forceUpdate = false, bool autoResolveConflict = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                if (data is Bundle bdl)
                {
                    if (!bdl.Item.Any())
                    {
                        return; // no need to send an empty bundle
                    }
                    else if(bdl.CorrelationKey.HasValue)
                    {
                        data = bdl.WithCorrelationControl(bdl.CorrelationKey.Value);
                    }
                }


                // create the appropriate message endpoint
                var patch = data as Patch;
                var upstreamService = patch != null ? UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(patch.AppliesTo.Type) : UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(data.GetType());
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    this.m_tracer.TraceVerbose("Pushing upstream as device {0}/{1}", data.GetType().GetSerializationName(), data.Key);

                    client.Requesting += (o, e) => e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.NoResponse, "true"); // The subscription will just download this data again later so no need for callback - save the bandwidth
                    client.Responding += (o, e) => this.Responding?.Invoke(this, e);

                    Guid newVersionId = Guid.Empty;

                    if (patch != null)
                    {
                        var dataPersistenceService = ApplicationServiceContext.Current.GetService(typeof(IDataPersistenceService<>).MakeGenericType(patch.AppliesTo.Type)) as IDataPersistenceService;

                        // Are we configured to have unsafe patching performed?
                        if (forceUpdate)
                        {
                            client.Requesting += (o, e) => e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.ForceApplyPatchHeaderName, "true");
                            patch.Operation.RemoveAll(o => o.OperationType == PatchOperationType.TestEqual || o.OperationType == PatchOperationType.TestNotEqual); // remove all safety
                        }

                        this.m_tracer.TraceVerbose("Performing patch: {0}", patch);
                        var existingObjectKey = patch.AppliesTo.Key;
                        try
                        {
                            newVersionId = this.ExtractVersionFromPatchResult(client.Patch($"{patch.AppliesTo.Type.GetSerializationName()}/{patch.AppliesTo.Key}", patch.AppliesTo.Tag, patch));
                        }
                        // DETECT CONDITION: Server rejects the patch because there is a conflict (i.e. server copy is not the expected version to apply patch)
                        catch (WebException e) when (e.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Conflict && autoResolveConflict)
                        {
                            this.m_tracer.TraceWarning("Server indicates conflict condition - will attempt to resolve patch automatically - {0}", e.ToHumanReadableString());
                            // Attempt to get the server copy, create a new patch from our object, and then re-submit 
                            var serverCopy = this.Get(patch.AppliesTo.Type, patch.AppliesTo.Key.Value, null);
                            // Can we apply the patch?
                            if (this.m_patchService.Test(patch, serverCopy)) // There is no issue - so just apply the patch locally and resubmit result to the server
                            {
                                var newVersion = this.m_patchService.Patch(patch, serverCopy, true);
                                if (client.Put<IdentifiedData, IdentifiedData>($"{newVersion.GetType().GetSerializationName()}/{newVersion.Key}", newVersion) is IVersionedData ivd)
                                {
                                    newVersionId = ivd.VersionKey.Value;
                                }
                            }
                            else
                            {
                                var idp = ApplicationServiceContext.Current.GetService(typeof(IDataPersistenceService<>).MakeGenericType(patch.AppliesTo.Type)) as IDataPersistenceService;
                                var myCopy = idp.Get(patch.AppliesTo.Key.Value) as IdentifiedData;
                                var serverDiff = this.m_patchService.Diff(serverCopy, myCopy);
                                // The difference between the server version and my copy have no differing properties - so just force the patch
                                if (!serverDiff.Operation.Any(sd => patch.Operation.Any(po => po.Path == sd.Path && sd.OperationType != PatchOperationType.TestEqual && sd.OperationType != PatchOperationType.TestNotEqual)))
                                {
                                    client.Requesting += (o, ev) => ev.AdditionalHeaders.Add(ExtendedHttpHeaderNames.ForceApplyPatchHeaderName, "true");
                                    newVersionId = this.ExtractVersionFromPatchResult(client.Patch($"{patch.AppliesTo.Type.GetSerializationName()}/{patch.AppliesTo.Key}", patch.AppliesTo.Tag, patch));
                                }
                                else
                                {
                                    throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_PATCH_ERR, new { patch = patch }), e);
                                }
                            }
                        }
                        // DETECT CONDITION: Server rejects the patch because it does not know about the object - resubmit the object
                        catch (WebException e) when (e.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotFound && autoResolveConflict)
                        {
                            this.m_tracer.TraceWarning("Server indicates not found on update - will resubmit original object in create mode- {0}", e.ToHumanReadableString());
                            var localObject = dataPersistenceService.Get(patch.AppliesTo.Key.Value) as IdentifiedData;
                            if (localObject is IVersionedData ivd)
                            {
                                ivd.VersionKey = null;
                                ivd.VersionSequence = null;
                                ivd.IsHeadVersion = true;
                            }
                            this.Insert(Bundle.CreateBundle(localObject));
                        }
                        catch (Exception e)
                        {
                            throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_PATCH_ERR, new { patch = patch }), e);
                        }

                        if (newVersionId != Guid.Empty)
                        {
                            var existing = dataPersistenceService.Get(patch.AppliesTo.Key.Value) as IVersionedData;
                            if (existing != null)
                            {
                                existing.VersionKey = newVersionId;
                                existing = dataPersistenceService.Update(existing) as IVersionedData;
                                this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(data, existing as IdentifiedData));
                            }
                        }
                    }
                    else // Regular object
                    {
                        IdentifiedData serverResponse = null;
                        var focalData = data is Bundle bunde && bunde.FocalObjects.Count == 1 ? bunde.GetFocalItems().First() : data;
                        EventHandler<RestRequestEventArgs> versionCheck = (o, e) =>
                        {
                            if (focalData is IVersionedData ivd && ivd.PreviousVersionKey.HasValue)
                            {
                                e.AdditionalHeaders.Add(HttpRequestHeader.IfMatch, $"{data.Type}.{ivd.PreviousVersionKey}");
                            }
                        };

                        try
                        {
                            if (!forceUpdate) // Use the tag to ensure the server does not have this version of the data
                            {
                                client.Requesting += versionCheck; 
                            }

                            serverResponse = client.Put<IdentifiedData, IdentifiedData>($"{data.GetType().GetSerializationName()}/{data.Key}", data);
                        }
                        catch (WebException we) when (we.Response is HttpWebResponse hwr && hwr.StatusCode == HttpStatusCode.Conflict)
                        {
                            // Attempt to fetch the current object (if we can) and see if it is just a version mismatch on our update - i.e. no conflicts
                            if (!(data is Bundle))
                            {
                                var serverCopy = this.Get(data.GetType(), data.Key.Value, null);
                                patch = this.m_patchService.Diff(serverCopy, data);
                                if (this.m_patchService.Test(patch, serverCopy)) // There is no issue - so just apply the patch locally and resubmit result to the server
                                {
                                    data.CopyObjectData(this.m_patchService.Patch(patch, serverCopy, true));
                                    if (data is IVersionedData ivd)
                                    {
                                        ivd.PreviousVersionKey = null;
                                    }
                                    client.Requesting -= versionCheck;
                                    serverResponse = client.Put<IdentifiedData, IdentifiedData>($"{data.GetType().GetSerializationName()}/{data.Key}", data);
                                }
                            }
                        }

                        if (serverResponse != null)
                        {
                            this.UpdateToServerCopy(serverResponse, data);
                        }
                        this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(data, serverResponse));
                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR, new { data = data.Type }), e);
            }
        }

        /// <summary>
        /// Extract version from the patch result
        /// </summary>
        private Guid ExtractVersionFromPatchResult(string remoteTag)
        {
            if (remoteTag.Contains("."))
            {
                return Guid.Parse(remoteTag.Split('.')[1]);
            }
            else if (Guid.TryParse(remoteTag, out var uuid))
            {
                return uuid;
            }
            else
            {
                throw new FormatException(String.Format(ErrorMessages.INVALID_FORMAT, remoteTag, $"ResourceName.{Guid.Empty}"));
            }
        }

        /// <summary>
        /// Get a <see cref="IPrincipal"/> representing the authenticated device with the upstream
        /// </summary>
        public IPrincipal AuthenticateAsDevice(IPrincipal onBehalfOf = null)
        {
            try
            {
                if (this.m_devicePrincipal != null && this.m_devicePrincipal.ExpiresAt.AddMinutes(-2) > DateTimeOffset.Now)
                {
                    return this.m_devicePrincipal;
                }

                var deviceCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Device);
                var applicationCredentialSettings = m_configuration.Credentials.Single(o => o.CredentialType == UpstreamCredentialType.Application);
                if (deviceCredentialSettings == null || applicationCredentialSettings == null)
                {
                    throw new InvalidOperationException(ErrorMessages.UPSTREAM_NOT_CONFIGURED);
                }

                IPrincipal devicePrincipal = null;
                switch (deviceCredentialSettings.Conveyance)
                {
                    case UpstreamCredentialConveyance.Header:
                    case UpstreamCredentialConveyance.Secret:
                        devicePrincipal = new HttpBasicTokenPrincipal(deviceCredentialSettings);
                        break;
                    case UpstreamCredentialConveyance.ClientCertificate:
                        devicePrincipal = new CertificatePrincipal(deviceCredentialSettings);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, deviceCredentialSettings.Conveyance));
                }

                var applicationIdentityProvider = ApplicationServiceContext.Current.GetService<IUpstreamServiceProvider<IApplicationIdentityProviderService>>();
                this.m_devicePrincipal = applicationIdentityProvider.UpstreamProvider.Authenticate(applicationCredentialSettings.CredentialName, devicePrincipal) as ITokenPrincipal;
                return this.m_devicePrincipal;
            }
            catch (Exception e)
            {
                throw new SecurityException(m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_AUTH_ERR), e);
            }
        }



        /// <summary>
        /// Get the upstream template keys
        /// </summary>
        private Guid GetUpstreamTemplateKey(TemplateDefinition template)
        {
            if (template == null)
            {
                return Guid.Empty;
            }
            using (AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
            {
                using (var client = new HdsiServiceClient(this.m_restClientFactory.GetRestClientFor(ServiceEndpointType.HealthDataService)))
                {

                    // Check the cache first
                    TemplateDefinition[] cached = null;
                    if (this.m_adhocCache?.TryGet("server.template.key", out cached) != true)
                    {
                        cached = client.Query<TemplateDefinition>(e => e.ObsoletionTime == null).Item.OfType<TemplateDefinition>().ToArray();
                        this.m_adhocCache?.Add("server.template.key", cached);
                    }

                    var serverTemplate = cached.FirstOrDefault(o => o.Mnemonic == template.Mnemonic);
                    if (null == serverTemplate)
                    {
                        serverTemplate = template;
                        return client.Create(serverTemplate).Key.Value;
                    }
                    else
                    {
                        return serverTemplate.Key.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Map an upstream template key to the local
        /// </summary>
        private IdentifiedData HarmonizeTemplateId(IdentifiedData data)
        {
            switch (data)
            {
                case Bundle bdl:
                    bdl.Item.ForEach(r => this.HarmonizeTemplateId(r));
                    return bdl;
                case TemplateDefinition td:
                    if (!td.Key.HasValue)
                    {
                        GetUpstreamTemplateKey(td); //Will force the insert to upstream.
                        data.Key = td.Key;
                    }
                    return data;
                case IHasTemplate iht:
                    if (iht.Template?.Key.HasValue == true && !iht.TemplateKey.HasValue)
                    {
                        iht.TemplateKey = iht.Template.Key;
                    }
                    else if (!iht.TemplateKey.HasValue)
                    {
                        iht.TemplateKey = GetUpstreamTemplateKey(data.LoadProperty<TemplateDefinition>(nameof(IHasTemplate.Template)));
                    }
                    return data;
                default:
                    return data;
            }
        }

        /// <inheritdoc/>
        IHasTemplate IUpstreamIntegrationService.HarmonizeTemplateId(IHasTemplate iht) => this.HarmonizeTemplateId(iht as IdentifiedData) as IHasTemplate;

        /// <inheritdoc/>
        public object Invoke(Type modelType, string operation, ParameterCollection parameters)
        {
            try
            {
                var upstreamService = UpstreamEndpointMetadataUtil.Current.GetServiceEndpoint(modelType);
                using (var authenticationContext = AuthenticationContext.EnterContext(this.AuthenticateAsDevice()))
                using (var client = this.m_restClientFactory.GetRestClientFor(upstreamService))
                {
                    this.m_tracer.TraceVerbose("Invoking {0} on upstream as device...", operation);
                    client.Credentials = new UpstreamPrincipalCredentials(AuthenticationContext.Current.Principal);
                    client.Responding += (o, e) => this.Responding?.Invoke(this, e);


                    var retVal = client.Post<ParameterCollection, Object>($"{modelType.GetSerializationName()}/${operation}", parameters);

                    this.Responded?.Invoke(this, new UpstreamIntegrationResultEventArgs(null, retVal as IdentifiedData));
                    return retVal;

                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR, new { data = modelType.GetSerializationName() }), e);
            }
        }
    }
}
