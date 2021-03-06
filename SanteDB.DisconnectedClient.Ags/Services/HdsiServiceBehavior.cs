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
using RestSrvr;
using RestSrvr.Exceptions;
using SanteDB.Core;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Attributes;
using SanteDB.Rest.HDSI;
using SanteDB.Rest.HDSI.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Health Data Services Interface 
    /// </summary>
    public class HdsiServiceBehavior : HdsiServiceBehaviorBase
    {

        // Resource handler tool
        private ResourceHandlerTool m_resourceHandler;

        /// <summary>
        /// Get resource handler
        /// </summary>
        /// <returns></returns>
        protected override ResourceHandlerTool GetResourceHandler()
        {
            if (this.m_resourceHandler == null)
                this.m_resourceHandler = new Rest.Common.ResourceHandlerTool(
                typeof(PatientResourceHandler).Assembly.ExportedTypes
                .Union(AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a =>
                {
                    try
                    {
                        return a.ExportedTypes;
                    }
                    catch { return Type.EmptyTypes; }
                }))
                .OfType<Type>()
                .Where(t => t != null && !t.IsAbstract && !t.IsInterface && typeof(IApiResourceHandler).IsAssignableFrom(t))
                .ToList(), typeof(IHdsiServiceContract));
            return this.m_resourceHandler;
        }

        /// <summary>
        /// HDSI service behavior
        /// </summary>
        public HdsiServiceBehavior()
        {
        }

        /// <summary>
        /// Resolve the specified code
        /// </summary>
        /// <param name="parms"></param>
        public override void ResolvePointer(NameValueCollection parms)
        {
            // create only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true" ||
                parms["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);

                        var result = restClient.Invoke<CodeSearchRequest, IdentifiedData>("SEARCH", "_ptr", "application/x-www-form-urlencoded", new CodeSearchRequest(parms));
                        if (result != null)
                        {
                            RestOperationContext.Current.OutgoingResponse.StatusCode = (int)HttpStatusCode.SeeOther;
                            if (result is IVersionedEntity versioned)
                                RestOperationContext.Current.OutgoingResponse.AddHeader("Location", this.CreateContentLocation(result.GetType().GetSerializationName(), versioned.Key.Value, "_history", versioned.VersionKey.Value) + "?_upstream=true");
                            else
                                RestOperationContext.Current.OutgoingResponse.AddHeader("Location", this.CreateContentLocation(result.GetType().GetSerializationName(), result.Key.Value) + "?_upstream=true");
                        }
                        else
                            throw new KeyNotFoundException($"Object not found");

                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                base.ResolvePointer(parms);
            }
        }

        /// <summary>
        /// Create the specified resource
        /// </summary>
        public override IdentifiedData Create(string resourceType, IdentifiedData body)
        {
            // create only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<IdentifiedData, IdentifiedData>($"{resourceType}", restClient.Accept, body);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Create(resourceType, body);
            }
        }

        public override IdentifiedData CreateUpdate(string resourceType, string id, IdentifiedData body)
        {
            // create only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<IdentifiedData, IdentifiedData>($"{resourceType}/{id}", restClient.Accept, body);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.CreateUpdate(resourceType, id, body);
            }
        }

        /// <summary>
        /// Delete the specified object on the server
        /// </summary>
        public override IdentifiedData Delete(string resourceType, string id)
        {
            // Only on the remote server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Requesting += (o, e) => e.AdditionalHeaders.Add("X-Delete-Mode", RestOperationContext.Current.IncomingRequest.Headers["X-Delete-Mode"] ?? "OBSOLETE");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Delete<IdentifiedData>($"{resourceType}/{id}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Delete(resourceType, id);
            }
        }

        /// <summary>
        /// Get the specified resource
        /// </summary>
        public override IdentifiedData Get(string resourceType, string id)
        {
            // Delete only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        var retVal = restClient.Get<IdentifiedData>($"{resourceType}/{id}");
                        // Do we have a local?
                        if (retVal is Entity entity &&
                            ApplicationServiceContext.Current.GetService<IDataPersistenceService<Entity>>().Get(entity.Key.Value, null, true, AuthenticationContext.SystemPrincipal) == null) 
                            entity.AddTag("$upstream", "true");
                        else if (retVal is Act act &&
                            ApplicationServiceContext.Current.GetService<IDataPersistenceService<Act>>().Get(act.Key.Value, null, true, AuthenticationContext.SystemPrincipal) != null) 
                            act.AddTag("$upstream", "true");
                        return retVal;
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Get(resourceType, id);
            }
        }

        /// <summary>
        /// Copy the specified resource from the remote to this instance
        /// </summary>
        [Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        public override IdentifiedData Copy(string resourceType, string id)
        {
            if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                try
                {
                    var integrationService = ApplicationServiceContext.Current.GetService<IClinicalIntegrationService>();
                    var handler = this.GetResourceHandler().GetResourceHandler<IHdsiServiceContract>(resourceType);

                    ApplicationContext.Current.SetProgress(Strings.locale_downloading, 0.0f);
                    var remote = integrationService.Get(handler.Type, Guid.Parse(id), null);
                    ApplicationServiceContext.Current.GetService<IDataCachingService>().Remove(remote.Key.Value);
                    ApplicationContext.Current.SetProgress(Strings.locale_downloading, 0.25f);

                    Bundle insertBundle = new Bundle();
                    insertBundle.Add(remote);

                    // Fetch all missing relationships
                    if(remote is Entity entity)
                    {
                        var persistence = ApplicationServiceContext.Current.GetService<IDataPersistenceService<Entity>>();
                        foreach (var itm in entity.Relationships.ToArray())
                        {
                            if (!typeof(EntityRelationshipTypeKeyStrings).GetFields().Any(f => itm.RelationshipTypeKey.ToString().Equals((String)f.GetValue(null), StringComparison.OrdinalIgnoreCase)))
                            {
                                entity.Relationships.Remove(itm);
                                continue;
                            }

                            var record = persistence.Get(itm.TargetEntityKey.Value, null, true, AuthenticationContext.Current.Principal);
                            if(record == null) // Download and insert
                            {
                                record = integrationService.Get<Entity>(itm.TargetEntityKey.Value, null);
                                insertBundle.Add(record);
                                ApplicationServiceContext.Current.GetService<IDataCachingService>().Remove(record.Key.Value);
                            }
                        }
                    }   
                    else if(remote is Act act)
                    {
                        var persistence = ApplicationServiceContext.Current.GetService<IDataPersistenceService<Act>>();
                        foreach (var itm in act.Relationships)
                        {
                            var record = persistence.Get(itm.TargetActKey.Value, null, true, AuthenticationContext.Current.Principal);
                            if (record == null) // Download and insert
                            {
                                record = integrationService.Get<Act>(itm.TargetActKey.Value, null);
                                insertBundle.Add(record);
                            }
                        }
                    }

                    ApplicationContext.Current.SetProgress(Strings.locale_downloading, 0.5f);

                    // Now we want to fetch all participations which have a relationship with the downloaded object if the object is a patient
                    if (remote is Patient patient)
                    {
                        // We want to update the patient so that this SDL is linked
                        int ofs = 0, tr = 1;
                        while (ofs < tr)
                        {
                            var related = integrationService.Find<Act>(o => o.Participations.Any(p => p.PlayerEntityKey == patient.Key), ofs, 25);
                            related.Item.ForEach(o => ApplicationServiceContext.Current.GetService<IDataCachingService>().Remove(o.Key.Value));

                            tr = related.TotalResults;
                            ofs += related.Item.Count;
                            insertBundle.Item.AddRange(related.Item);
                        }

                        // Handle MDM just in case
                        while (ofs < tr)
                        {
                            var related = integrationService.Find<Act>(o => o.Participations.Any(p => p.PlayerEntity.Relationships.Where(r=>r.RelationshipType.Mnemonic == "MDM-Master").Any(r=>r.SourceEntityKey == patient.Key)), ofs, 25);
                            related.Item.ForEach(o => ApplicationServiceContext.Current.GetService<IDataCachingService>().Remove(o.Key.Value));

                            tr = related.TotalResults;
                            ofs += related.Item.Count;
                            insertBundle.Item.AddRange(related.Item);
                        }
                    }

                    // Insert 
                    ApplicationServiceContext.Current.GetService<IDataPersistenceService<Bundle>>()?.Insert(insertBundle, TransactionMode.Commit, AuthenticationContext.Current.Principal);

                    // Clear cache
                    ApplicationServiceContext.Current.GetService<IDataCachingService>().Clear();
                    return remote;
                }
                catch (Exception e)
                {
                    this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                    throw;
                }
            else
                throw new FaultException(502);

        }

        /// <summary>
        /// Get a specific version of the object
        /// </summary>
        public override IdentifiedData GetVersion(string resourceType, string id, string versionId)
        {
            // Delete only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<IdentifiedData>($"{resourceType}/{id}/_history/{versionId}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.GetVersion(resourceType, id, versionId);
            }
        }

        /// <summary>
        /// Get history of the object
        /// </summary>
        public override IdentifiedData History(string resourceType, string id)
        {
            // Delete only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Get<IdentifiedData>($"{resourceType}/{id}/history");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.History(resourceType, id);
            }
        }

        /// <summary>
        /// Perform options
        /// </summary>
        public override ServiceOptions Options()
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        return restClient.Options<ServiceOptions>("/");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Options();
            }
        }

        /// <summary>
        /// Perform a patch
        /// </summary>
        public override void Patch(string resourceType, string id, Patch body)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        var patchId = restClient.Patch<Patch>($"/{resourceType}/{id}", "application/xml+sdb-patch", RestOperationContext.Current.IncomingRequest.Headers["If -Match"], body);
                        RestOperationContext.Current.OutgoingResponse.SetETag(patchId);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                base.Patch(resourceType, id, body);
            }
        }

        /// <summary>
        /// Get options for resource
        /// </summary>
        /// <param name="resourceType"></param>
        /// <returns></returns>
        public override ServiceResourceOptions ResourceOptions(string resourceType)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        return restClient.Options<ServiceResourceOptions>($"/{resourceType}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.ResourceOptions(resourceType);
            }
        }

        /// <summary>
        /// Perform search
        /// </summary>
        public override IdentifiedData Search(string resourceType)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        // This NVC is UTF8 compliant
                        var nvc = SanteDB.Core.Model.Query.NameValueCollection.ParseQueryString(RestOperationContext.Current.IncomingRequest.Url.Query);
                        var retVal = restClient.Get<IdentifiedData>($"/{resourceType}", nvc.Select(o=>new KeyValuePair<String, Object>(o.Key, o.Value)).ToArray());

                        if(retVal is Bundle bundle)
                        {
                            bundle.Item
                                .OfType<ITaggable>()
                                .Select(o =>
                                {
                                    // Do we have a local?
                                    if (o is Entity entity &&
                                        ApplicationServiceContext.Current.GetService<IDataPersistenceService<Entity>>()?.Get(entity.Key.Value, null, true, AuthenticationContext.SystemPrincipal) != null)
                                        return o;
                                    o.AddTag("$upstream", "true");
                                    return o;
                                }).ToList();
                        }
                        return retVal;
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Search(resourceType);
            }
        }

        /// <summary>
        /// Throws an exception if the service is not ready
        /// </summary>
        public override void ThrowIfNotReady()
        {
            if (!ApplicationServiceContext.Current.GetService<AgsService>().IsRunning)
                throw new DomainStateException();
        }

        public override IdentifiedData Update(string resourceType, string id, IdentifiedData body)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Put<IdentifiedData, IdentifiedData>($"/{resourceType}/{id}", restClient.Accept, body);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.Update(resourceType, id, body);
            }
        }

        public override object AssociationSearch(string resourceType, string key, string childResourceType)
        {
            // Perform only on the external server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        // This NVC is UTF8 compliant
                        var nvc = SanteDB.Core.Model.Query.NameValueCollection.ParseQueryString(RestOperationContext.Current.IncomingRequest.Url.Query);
                        var retVal = restClient.Get<object>($"/{resourceType}/{key}/{childResourceType}", nvc.Select(o => new KeyValuePair<String, Object>(o.Key, o.Value)).ToArray());

                        if (retVal is Bundle bundle)
                        {
                            bundle.Item
                                .OfType<ITaggable>()
                                .Select(o =>
                                {
                                    // Do we have a local?
                                    if (o is Entity entity &&
                                        ApplicationServiceContext.Current.GetService<IDataPersistenceService<Entity>>()?.Get(entity.Key.Value, null, true, AuthenticationContext.SystemPrincipal) != null)
                                        return o;
                                    o.AddTag("$upstream", "true");
                                    return o;
                                }).ToList();
                        }
                        return retVal;
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.AssociationSearch(resourceType, key, childResourceType);
            }
        }

        public override object AssociationRemove(string resourceType, string key, string childResourceType, string scopedEntityKey)
        {
            // Only on the remote server
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Requesting += (o, e) => e.AdditionalHeaders.Add("X-Delete-Mode", RestOperationContext.Current.IncomingRequest.Headers["X-Delete-Mode"] ?? "OBSOLETE");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Delete<object>($"{resourceType}/{key}/{childResourceType}/{scopedEntityKey}");
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.AssociationRemove(resourceType, key, childResourceType, scopedEntityKey);
            }
        }

        public override object AssociationGet(string resourceType, string key, string childResourceType, string scopedEntityKey)
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        var retVal = restClient.Get<IdentifiedData>($"{resourceType}/{key}/{childResourceType}/{scopedEntityKey}");
                        // Do we have a local?
                        if (retVal is Entity entity &&
                            ApplicationServiceContext.Current.GetService<IDataPersistenceService<Entity>>().Get(entity.Key.Value, null, true, AuthenticationContext.SystemPrincipal) == null)
                            entity.AddTag("$upstream", "true");
                        else if (retVal is Act act &&
                            ApplicationServiceContext.Current.GetService<IDataPersistenceService<Act>>().Get(act.Key.Value, null, true, AuthenticationContext.SystemPrincipal) != null)
                            act.AddTag("$upstream", "true");
                        return retVal;
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.AssociationGet(resourceType, key, childResourceType, scopedEntityKey);
            }
        }

        public override object AssociationCreate(string resourceType, string key, string childResourceType, object body)
        {
            if (RestOperationContext.Current.IncomingRequest.QueryString["_upstream"] == "true")
            {
                if (ApplicationContext.Current.GetService<INetworkInformationService>().IsNetworkAvailable)
                    try
                    {
                        var restClient = ApplicationContext.Current.GetRestClient("hdsi");
                        restClient.Responded += (o, e) => RestOperationContext.Current.OutgoingResponse.SetETag(e.ETag);
                        return restClient.Post<object, object>($"{resourceType}/{childResourceType}", restClient.Accept, body);
                    }
                    catch (Exception e)
                    {
                        this.m_traceSource.TraceError("Error performing online operation: {0}", e.InnerException);
                        throw;
                    }
                else
                    throw new FaultException(502);
            }
            else
            {
                return base.AssociationCreate(resourceType, key, childResourceType, body);
            }
        }
    }
}
