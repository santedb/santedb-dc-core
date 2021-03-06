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
using System;
using System.Collections.Generic;

using SanteDB.DisconnectedClient.Services.Attributes;
using SanteDB.Core.Model.Entities;
using System.IO;
using Newtonsoft.Json;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using SanteDB.Core.Applets.ViewModel;
using SanteDB.Core.Model.Collection;
using System.Text;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Constants;
using System.Linq;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Caching;
using SanteDB.Core.Model.Interfaces;
using System.Linq.Expressions;
using SanteDB.DisconnectedClient.Services.Model;
using SanteDB.DisconnectedClient.Security;
using System.Reflection;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Applets.Services;
using System.Text.RegularExpressions;
using SanteDB.DisconnectedClient;
using SanteDB.Core.Security;

namespace SanteDB.DisconnectedClient.Services.ServiceHandlers
{
    /// <summary>
    /// Represents an IMS service handler
    /// </summary>
    [RestService("/__hdsi")]
    public partial class HdsiService
    {

        // UTF8 BOM
        private readonly String c_utf8bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

        // Tracer 
        private Tracer m_tracer = Tracer.GetTracer(typeof(HdsiService));

        // View model serliazer
        private JsonViewModelSerializer m_serializer = new JsonViewModelSerializer();

        ///// <summary>
        ///// Creates a bundle.
        ///// </summary>
        ///// <param name="bundleToInsert">The bundle to be inserted.</param>
        ///// <returns>Returns the inserted bundle.</returns>
        //[RestOperation(Method = "POST", UriPath = "/Bundle", FaultProvider = nameof(HdsiFault))]
        //[Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        //[return: RestMessage(RestMessageFormat.SimpleJson)]
        //public Bundle CreateBundle([RestMessage(RestMessageFormat.SimpleJson)]Bundle bundleToInsert)
        //{
        //    IBatchRepositoryService bundleService = ApplicationContext.Current.GetService<IBatchRepositoryService>();
        //    return bundleService.Insert(bundleToInsert);
        //}

        /// <summary>
        /// Creates the entity relationship.
        /// </summary>
        /// <param name="entityRelationship">The entity relationship.</param>
        /// <returns>Returns the created entity relationship.</returns>
        [RestOperation(Method = "POST", UriPath = "/EntityRelationship", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        public EntityRelationship CreateEntityRelationship([RestMessage(RestMessageFormat.SimpleJson)] EntityRelationship entityRelationship)
        {
            var erRepositoryService = ApplicationContext.Current.GetService<IRepositoryService<EntityRelationship>>();

            return erRepositoryService.Insert(entityRelationship);
        }

        /// <summary>
        /// Gets an entity
        /// </summary>
        /// <returns>Returns an entity.</returns>
        [RestOperation(Method = "GET", UriPath = "/Entity", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.QueryClinicalData)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public IdentifiedData GetEntity()
        {
            return this.GetEntity<Entity>();
        }

        /// <summary>
        /// Gets an entity
        /// </summary>
        /// <returns>Returns an entity.</returns>
        [RestOperation(Method = "GET", UriPath = "/Material", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.QueryClinicalData)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public IdentifiedData GetMaterial()
        {
            return this.GetEntity<Material>();
        }

        /// <summary>
        /// Gets an entity
        /// </summary>
        /// <returns>Returns an entity.</returns>
        [RestOperation(Method = "GET", UriPath = "/ManufacturedMaterial", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.QueryClinicalData)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public IdentifiedData GetManufacturedMaterial()
        {
            return this.GetEntity<ManufacturedMaterial>();
        }

        /// <summary>
        /// Deletes the act
        /// </summary>
        [RestOperation(Method = "DELETE", UriPath = "/EntityRelationship", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.DeleteClinicalData)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public EntityRelationship DeleteEntityRelationship()
        {
            var erRepositoryService = ApplicationContext.Current.GetService<IRepositoryService<EntityRelationship>>();

            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

            if (search.ContainsKey("_id"))
            {
                // Force load from DB
                var keyid = Guid.Parse(search["_id"].FirstOrDefault());
                return erRepositoryService.Obsolete(keyid);
            }
            else
                throw new ArgumentNullException("_id");
        }


        /// <summary>
        /// Get entity internal
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        private IdentifiedData GetEntity<TEntity>() where TEntity : Entity
        {
            var entityService = ApplicationContext.Current.GetService<IRepositoryService<TEntity>>();
            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

            if (search.ContainsKey("_id"))
            {
                // Force load from DB
                ApplicationContext.Current.GetService<IDataCachingService>().Remove(Guid.Parse(search["_id"].FirstOrDefault()));
                var entityId = Guid.Parse(search["_id"].FirstOrDefault());
                var entity = entityService.Get(entityId);
                return entity;
            }
            else
            {
                {
                    int totalResults = 0,
                        offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
                        count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

                    IEnumerable<Entity> retVal = null;

                    // Any filter
                    if (search.ContainsKey("any") || search.ContainsKey("any[]"))
                    {

                        this.m_tracer.TraceVerbose("Freetext search: {0}", MiniHdsiServer.CurrentContext.Request.Url.Query);

                        var values = search.ContainsKey("any") ? search["any"] : search["any[]"];
                        // Filtes
                        var fts = ApplicationContext.Current.GetService<IFreetextSearchService>();
                        retVal = fts.Search<Entity>(values.ToArray(), offset, count, out totalResults);
                        search.Remove("any");
                        search.Remove("any[]");
                    }

                    if (search.Keys.Count(o => !o.StartsWith("_")) > 0)
                    {
                        var predicate = QueryExpressionParser.BuildLinqExpression<TEntity>(search);
                        this.m_tracer.TraceVerbose("Searching Entities : {0} / {1}", MiniHdsiServer.CurrentContext.Request.Url.Query, predicate);

                        var tret = entityService.Find(predicate, offset, count, out totalResults);
                        if (retVal == null)
                            retVal = tret;
                        else
                            retVal = retVal.OfType<IIdentifiedEntity>().Intersect(tret.OfType<IIdentifiedEntity>(), new KeyComparer()).OfType<Entity>();
                    }

                    // Serialize the response
                    return new Bundle()
                    {
                        Item = retVal.OfType<IdentifiedData>().ToList(),
                        Offset = offset,
                        Count = retVal.Count(),
                        TotalResults = totalResults
                    };
                }
            }
        }


        //      /// <summary>
        ///// Gets providers.
        ///// </summary>
        ///// <returns>Returns a list of providers.</returns>
        //      [RestOperation(Method = "GET", UriPath = "/Provider", FaultProvider = nameof(HdsiFault))]
        //      [Demand(PermissionPolicyIdentifiers.QueryClinicalData)]
        //      [return: RestMessage(RestMessageFormat.SimpleJson)]
        //      public IdentifiedData GetProvider()
        //      {

        //          var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
        //          var providerService = ApplicationContext.Current.GetService<IProviderRepositoryService>();

        //          if (search.ContainsKey("_id"))
        //          {
        //              // Force load from DB
        //              ApplicationContext.Current.GetService<IDataCachingService>().Remove(Guid.Parse(search["_id"].FirstOrDefault()));
        //              var provider = providerService.Get(Guid.Parse(search["_id"].FirstOrDefault()), Guid.Empty);
        //              // Ensure expanded
        //              //JniUtil.ExpandProperties(patient, search);
        //              return provider;
        //          }
        //          else
        //          {
        //              var predicate = QueryExpressionParser.BuildLinqExpression<Provider>(search);

        //              int totalResults = 0,
        //                offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
        //                count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;


        //              this.m_tracer.TraceVerbose("Searching Providers : {0} / {1}", MiniHdsiServer.CurrentContext.Request.Url.Query, predicate);

        //              var retVal = providerService.Find(predicate, offset, count, out totalResults);

        //              // Serialize the response
        //              return new Bundle()
        //              {
        //                  Item = retVal.OfType<IdentifiedData>().ToList(),
        //                  Offset = offset,
        //                  Count = count,
        //                  TotalResults = totalResults
        //              };
        //          }
        //      }

        /// <summary>
        /// Get a template
        /// </summary>
        [RestOperation(Method = "GET", UriPath = "/Act/Template", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.Login)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public Act GetActTemplate()
        {
            var templateString = this.GetTemplateString();
            // Load the data from the template string
            var retVal = this.m_serializer.DeSerialize<Act>(templateString);
            retVal.Key = Guid.NewGuid();
            foreach (var itm in retVal.Participations)
                itm.Key = Guid.NewGuid();
            foreach (var itm in retVal.Relationships)
                itm.Key = Guid.NewGuid();
            foreach (var itm in retVal.Participations.Where(o => o.PlayerEntityKey == AuthenticationContext.Current.Session.UserEntity.Key))
                itm.PlayerEntity = AuthenticationContext.Current.Session.UserEntity;
            // Delayload
            return retVal;
        }

        /// <summary>
        /// Get a template
        /// </summary>
        [RestOperation(Method = "GET", UriPath = "/Entity/Template", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.Login)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public Entity GetEntityTemplate()
        {
            var templateString = this.GetTemplateString();
            // Load the data from the template string
            var retVal = this.m_serializer.DeSerialize<Entity>(templateString);
            retVal.Key = Guid.NewGuid();
            foreach (var itm in retVal.Participations) itm.Key = Guid.NewGuid();
            foreach (var itm in retVal.Relationships) itm.Key = Guid.NewGuid();

            //retVal.SetDelayLoad(true);
            return retVal;
        }

        /// <summary>
        /// Get the template string
        /// </summary>
        private String GetTemplateString()
        {

            var appletManagerService = ApplicationContext.Current.GetService<IAppletManagerService>();
            var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
            // The template to construct
            List<String> templateId = search["templateId"];

            // Attempt to get the template definition
            var template = appletManagerService.Applets.GetTemplateDefinition(templateId.First());

            // Load and replace constants
            var templateBytes = template.DefinitionContent;
            if (templateBytes == null)
                templateBytes = appletManagerService.Applets.Resolver?.Invoke(appletManagerService.Applets.ResolveAsset(template.Definition)) as byte[];

            var templateString = Encoding.UTF8.GetString(templateBytes);

            //if (templateString.StartsWith(c_utf8bom))
            //    templateString = templateString.Remove(0, c_utf8bom.Length);
            var regex = new Regex(@"\{\{uuid\}\}");

            this.m_tracer.TraceVerbose("Template {0} (Pre-Populated): {1}", templateId, templateString);
            var securityUser = AuthenticationContext.Current.Session.SecurityUser;
            var userEntity = AuthenticationContext.Current.Session.UserEntity;
            templateString = templateString.Replace("{{today}}", DateTime.Today.ToString("o"))
                .Replace("{{now}}", DateTime.Now.ToString("o"))
                .Replace("{{userId}}", securityUser.Key.ToString())
                .Replace("{{userEntityId}}", userEntity?.Key.ToString())
                .Replace("{{facilityId}}", userEntity?.Relationships.FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation)?.TargetEntityKey.ToString());
            templateString = regex.Replace(templateString, (o) => Guid.NewGuid().ToString());
            this.m_tracer.TraceVerbose("Template {0} (Post-Populated): {1}", templateId, templateString);
            return templateString;
        }


        ///// <summary>
        ///// Gets the user profile of the current user.
        ///// </summary>
        ///// <returns>Returns the user profile of the current user.</returns>
        //[return: RestMessage(RestMessageFormat.SimpleJson)]
        //[RestOperation(UriPath = "/UserEntity", Method = "GET")]
        //[Demand(PermissionPolicyIdentifiers.Login)]
        //public IdentifiedData GetUserEntity()
        //{
        //    var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
        //    var securityService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();

        //    if (search.ContainsKey("_id"))
        //    {
        //        // Force load from DB
        //        ApplicationContext.Current.GetService<IDataCachingService>().Remove(Guid.Parse(search["_id"].FirstOrDefault()));
        //        var provider = securityService.GetUserEntity(Guid.Parse(search["_id"].FirstOrDefault()), Guid.Empty);
        //        // Ensure expanded
        //        //JniUtil.ExpandProperties(patient, search);
        //        return provider;
        //    }
        //    else
        //    {
        //        var predicate = QueryExpressionParser.BuildLinqExpression<UserEntity>(search);

        //        int totalResults = 0,
        //          offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
        //          count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;

        //        this.m_tracer.TraceVerbose("Searching User Entity : {0} / {1}", MiniHdsiServer.CurrentContext.Request.Url.Query, predicate);

        //        var retVal = securityService.FindUserEntity(predicate, offset, count, out totalResults);

        //        // Serialize the response
        //        return new Bundle()
        //        {
        //            Item = retVal.OfType<IdentifiedData>().ToList(),
        //            Offset = offset,
        //            Count = count,
        //            TotalResults = totalResults
        //        };
        //    }
        //}

        /// <summary>
        /// Handle a fault
        /// </summary>
        public ErrorResult HdsiFault(Exception e)
        {
            return new ErrorResult(e);
        }

        ///// <summary>
        ///// Saves the user profile.
        ///// </summary>
        ///// <param name="user">The users modified profile information.</param>
        ///// <returns>Returns the users updated profile.</returns>
        //[return: RestMessage(RestMessageFormat.SimpleJson)]
        //[RestOperation(UriPath = "/UserEntity", Method = "PUT")]
        //[Demand(PermissionPolicyIdentifiers.Login)]
        //public UserEntity UpdateUserEntity([RestMessage(RestMessageFormat.SimpleJson)] UserEntity user)
        //{
        //    var query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
        //    ISecurityRepositoryService securityRepositoryService = ApplicationContext.Current.GetService<ISecurityRepositoryService>();
        //    AuthenticationContext.Current?.Session?.ClearCached();
        //    //IDataPersistenceService<UserEntity> persistenceService = ApplicationContext.Current.GetService<IDataPersistenceService<UserEntity>>();
        //    return securityRepositoryService.SaveUserEntity(user);
        //}



        ///// <summary>
        ///// Search places
        ///// </summary>
        //[RestOperation(Method = "GET", UriPath = "/Place", FaultProvider = nameof(HdsiFault))]
        //[return: RestMessage(RestMessageFormat.SimpleJson)]
        //public IdentifiedData GetPlace()
        //{
        //    var search = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);
        //    var placeService = ApplicationContext.Current.GetService<IPlaceRepositoryService>();

        //    if (search.ContainsKey("_id"))
        //        return placeService.Get(Guid.Parse(search["_id"].FirstOrDefault()), Guid.Empty);
        //    else
        //    {
        //        var predicate = QueryExpressionParser.BuildLinqExpression<Place>(search);
        //        this.m_tracer.TraceVerbose("Searching Places : {0} / {1}", MiniHdsiServer.CurrentContext.Request.Url.Query, predicate);

        //        int totalResults = 0,
        //            offset = search.ContainsKey("_offset") ? Int32.Parse(search["_offset"][0]) : 0,
        //            count = search.ContainsKey("_count") ? Int32.Parse(search["_count"][0]) : 100;
        //        var retVal = placeService.Find(predicate, offset, count, out totalResults);

        //        return new Bundle()
        //        {
        //            Item = retVal.OfType<IdentifiedData>().ToList(),
        //            Offset = offset,
        //            Count = count,
        //            TotalResults = totalResults
        //        };
        //    }
        //}

        ///// <summary>
        ///// Updates an entity.
        ///// </summary>
        ///// <param name="entityToUpdate">The entity to be updated.</param>
        ///// <returns>Returns the updated entity.</returns>
        //[RestOperation(Method = "PUT", UriPath = "/entity", FaultProvider = nameof(HdsiFault))]
        //[Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        //[return: RestMessage(RestMessageFormat.SimpleJson)]
        //public Entity UpdateEntity([RestMessage(RestMessageFormat.SimpleJson)]Entity entityToUpdate)
        //{
        //    //IEntityRepositoryService repository = ApplicationContext.Current.GetService<IEntityRepositoryService>();
        //    // Get all the acts if none were supplied, and all of the relationships if none were supplied
        //    //return repository.Save(entityToUpdate).GetLocked() as Entity;
        //}

        /// <summary>
        /// Updates an entity.
        /// </summary>
        /// <param name="entityToUpdate">The entity to be updated.</param>
        /// <returns>Returns the updated entity.</returns>
        [RestOperation(Method = "PUT", UriPath = "/EntityExtension", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        [return: RestMessage(RestMessageFormat.SimpleJson)]
        public Entity UpdateEntityExtension([RestMessage(RestMessageFormat.SimpleJson)]EntityExtension extensionToSave)
        {
            var entityRepository = ApplicationContext.Current.GetService<IRepositoryService<Entity>>();
            var query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

            Guid entityKey = Guid.Empty;

            if (query.ContainsKey("_id") && Guid.TryParse(query["_id"][0], out entityKey))
            {
                var entity = entityRepository.Get(entityKey).Copy() as Entity;
                if (entity != null)
                {

                    // Add extension if not already exists
                    entity.Extensions.RemoveAll(o => o == null);
                    var extension = entity.Extensions.FirstOrDefault(o => o.LoadProperty<ExtensionType>("ExtensionType").Name == extensionToSave.ExtensionType.Name);
                    if (extension != null)
                        entity.Extensions.Remove(extension);
                    entity.Extensions.Add(extensionToSave);
                    return entityRepository.Save(entity);

                }
                else
                    throw new ArgumentException("Entity not found");

            }
            else
            {
                throw new ArgumentException("Entity not found");
            }
        }
        /// <summary>
        /// Updates the entity relationship.
        /// </summary>
        /// <param name="entityRelationship">The entity relationship.</param>
        /// <returns>Returns the updated entity relationship.</returns>
        [RestOperation(Method = "PUT", UriPath = "/EntityRelationship", FaultProvider = nameof(HdsiFault))]
        [Demand(PermissionPolicyIdentifiers.WriteClinicalData)]
        public EntityRelationship UpdateEntityRelationship([RestMessage(RestMessageFormat.SimpleJson)] EntityRelationship entityRelationship)
        {
            var erRepositoryService = ApplicationContext.Current.GetService<IRepositoryService<EntityRelationship>>();

            return erRepositoryService.Save(entityRelationship);
        }


        ///// <summary>
        ///// Updates a manufactured material.
        ///// </summary>
        ///// <param name="manufacturedMaterial">The manufactured material to be updated.</param>
        ///// <returns>Returns the updated manufactured material.</returns>
        //[RestOperation(Method = "PUT", UriPath = "/ManufacturedMaterial", FaultProvider = nameof(HdsiFault))]
        //      [Demand(PermissionPolicyIdentifiers.Login)]
        //      [return: RestMessage(RestMessageFormat.SimpleJson)]
        //      public ManufacturedMaterial UpdateManufacturedMaterial([RestMessage(RestMessageFormat.SimpleJson)] ManufacturedMaterial manufacturedMaterial)
        //      {
        //          var query = NameValueCollection.ParseQueryString(MiniHdsiServer.CurrentContext.Request.Url.Query);

        //          Guid manufacturedMaterialKey = Guid.Empty;
        //          Guid manufacturedMaterialVersionKey = Guid.Empty;

        //          if (query.ContainsKey("_id") && Guid.TryParse(query["_id"][0], out manufacturedMaterialKey) && query.ContainsKey("_versionId") && Guid.TryParse(query["_versionId"][0], out manufacturedMaterialVersionKey))
        //          {
        //              if (manufacturedMaterial.Key == manufacturedMaterialKey && manufacturedMaterial.VersionKey == manufacturedMaterialVersionKey)
        //              {
        //                  var manufacturedMaterialRepositoryService = ApplicationContext.Current.GetService<IMaterialRepositoryService>();

        //                  return manufacturedMaterialRepositoryService.Save(manufacturedMaterial);
        //              }
        //              else
        //              {
        //                  throw new ArgumentException("Manufactured Material not found");
        //              }
        //          }
        //          else
        //          {
        //              throw new ArgumentException("Manufactured Material not found");
        //          }
        //      }
    }

    /// <summary>
    /// Key comparion
    /// </summary>
    internal class KeyComparer : IEqualityComparer<IIdentifiedEntity>
    {
        public bool Equals(IIdentifiedEntity x, IIdentifiedEntity y)
        {
            return x.Key == y.Key;
        }

        public int GetHashCode(IIdentifiedEntity obj)
        {
            return obj.GetHashCode();
        }
    }
}