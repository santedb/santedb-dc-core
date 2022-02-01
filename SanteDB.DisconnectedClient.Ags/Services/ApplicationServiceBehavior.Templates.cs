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
using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;

using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// Application service behavior for templates
    /// </summary>
    public partial class ApplicationServiceBehavior
    {

        // Security repository
        private ISecurityRepositoryService m_securityRepository = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>();

        /// <summary>
        /// Get all templates from the host
        /// </summary>
        public List<AppletTemplateDefinition> GetTemplates()
        {
            var appletManager = ApplicationServiceContext.Current.GetService<IAppletManagerService>();

            var httpQuery = NameValueCollection.ParseQueryString(RestOperationContext.Current.IncomingRequest.Url.Query);
            var query = QueryExpressionParser.BuildLinqExpression<AppletTemplateDefinition>(httpQuery, null, true);
            return appletManager.Applets
                .SelectMany(o => o.Templates)
                .GroupBy(o => o.Mnemonic)
                .Select(o => o.OrderByDescending(t => t.Priority).FirstOrDefault())
                .Where(query.Compile()).ToList();

        }

        /// <summary>
        /// Get the template definition in JSON
        /// </summary>
        public IdentifiedData GetTemplateDefinition(string templateId)
        {
            // First, get the template definition
            var appletManager = ApplicationServiceContext.Current.GetService<IAppletManagerService>();
            var parameters = RestOperationContext.Current.IncomingRequest.QueryString.Keys.OfType<String>().ToDictionary(o => o, o => RestOperationContext.Current.IncomingRequest.QueryString[o]);

            // Add context parameters
            var userEntity = this.m_securityRepository.GetUserEntity(AuthenticationContext.Current.Principal.Identity);
            if (!parameters.ContainsKey("userEntityId"))
                parameters.Add("userEntityId", userEntity?.Key.ToString());
            if (!parameters.ContainsKey("facilityId"))
                parameters.Add("facilityId", userEntity.GetRelationships().FirstOrDefault(o => o.RelationshipTypeKey == EntityRelationshipTypeKeys.DedicatedServiceDeliveryLocation)?.TargetEntityKey?.ToString());

            return appletManager.Applets.GetTemplateInstance(templateId, parameters);
        }

        /// <summary>
        /// Get the template view
        /// </summary>
        public void GetTemplateView(string templateId)
        {
            var appletManager = ApplicationServiceContext.Current.GetService<IAppletManagerService>();
            var template = appletManager.Applets.GetTemplateDefinition(templateId);
            if (template == null)
                throw new KeyNotFoundException($"Template {templateId} not found");
            RestOperationContext.Current.OutgoingResponse.Redirect(template.View);
        }

        /// <summary>
        /// Get the specified template form
        /// </summary>
        public void GetTemplateForm(string templateId)
        {
            var appletManager = ApplicationServiceContext.Current.GetService<IAppletManagerService>();
            var template = appletManager.Applets.GetTemplateDefinition(templateId);
            if (template == null)
                throw new KeyNotFoundException($"Template {templateId} not found");
            RestOperationContext.Current.OutgoingResponse.Redirect(template.Form);
        }
    }
}
