/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
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
 * Date: 2019-11-27
 */
using RestSrvr;
using RestSrvr.Exceptions;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Patch;
using SanteDB.Core.Services;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Interop;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Security;
using SanteDB.Rest.Common;
using SanteDB.Rest.HDSI;
using SanteDB.Rest.HDSI.Resources;
using System;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Health Data Services Interface 
    /// </summary>
    public class HdsiServiceBehavior : HdsiServiceBehaviorBase
    {
        /// <summary>
        /// HDSI service behavior
        /// </summary>
        public HdsiServiceBehavior() : base(
            new Rest.Common.ResourceHandlerTool(
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
                .ToList(), typeof(IHdsiServiceContract)))
        {
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
                        return restClient.Get<IdentifiedData>($"{resourceType}/{id}");
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
                        return restClient.Get<IdentifiedData>($"{resourceType}/{id}/history/{versionId}");
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
                        return restClient.Get<IdentifiedData>($"/{resourceType}", RestOperationContext.Current.IncomingRequest.QueryString.ToList().ToArray());
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
            ; // This does nothing
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


    }
}
