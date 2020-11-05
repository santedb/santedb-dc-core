using RestSrvr;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Applets.ViewModel.Json;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.Entities;

using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
                .GroupBy(o=>o.Mnemonic)
                .Select(o=>o.OrderByDescending(t=>t.Priority).FirstOrDefault())
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
        
        public Stream GetTemplateView(string templateId)
        {
            throw new NotImplementedException();
        }

        public Stream GetTemplateForm(string templateId)
        {
            throw new NotImplementedException();
        }
    }
}
