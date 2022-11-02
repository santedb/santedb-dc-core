using SanteDB.Client.Exceptions;
using SanteDB.Client.Upstream;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.AMI.Auth;
using SanteDB.Core.Model.AMI.Collections;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Messaging.AMI.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Represents a policy information service which communicates with an upstream policy information service
    /// </summary>
    public class UpstreamPolicyInformationService : UpstreamServiceBase, IPolicyInformationService
    {
        private readonly ILocalizationService m_localizationSerice;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(UpstreamPolicyInformationService));

        /// <inheritdoc/>
        public string ServiceName => throw new NotImplementedException();

        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamPolicyInformationService(ILocalizationService localizationService,
            IRestClientFactory restClientFactory,
            IUpstreamManagementService upstreamManagementService,
            IUpstreamIntegrationService integrationService = null) : base(restClientFactory, upstreamManagementService, integrationService)
        {
            this.m_localizationSerice = localizationService;
        }

        /// <inheritdoc/>
        public void AddPolicies(object securable, PolicyGrantType rule, IPrincipal principal, params string[] policyOids)
        {
            if(securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if(!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    foreach (var itm in policyOids)
                    {
                        client.AddPolicy(securable as IIdentifiedData, itm, rule);
                    }
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException( this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);

            }
        }

        /// <inheritdoc/>
        public IEnumerable<IPolicyInstance> GetPolicies(object securable)
        {
            if (securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return new IPolicyInstance[0];
            }

            try
            {
                switch (securable)
                {
                    case SecurityDevice sd:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            return client.GetDevices(d => d.Name == sd.Name).CollectionItem.OfType<SecurityDeviceInfo>().First().Policies.Select(o => new UpstreamPolicyInstance(sd, o.Policy, o.Grant));
                        }
                    case SecurityApplication sa:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            return client.GetApplications(a => a.Name == sa.Name).CollectionItem.OfType<SecurityApplicationInfo>().First().Policies.Select(o => new UpstreamPolicyInstance(sa, o.Policy, o.Grant));
                        }
                    case SecurityRole sr:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            return client.GetRoles(a => a.Name == sr.Name).CollectionItem.OfType<SecurityRoleInfo>().First().Policies.Select(o => new UpstreamPolicyInstance(sr, o.Policy, o.Grant));
                        }
                    case IPrincipal ipr:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            return client.GetUsers(o => o.UserName == ipr.Identity.Name).CollectionItem.OfType<SecurityUserInfo>().First().Policies.Select(o => new UpstreamPolicyInstance(ipr, o.Policy, o.Grant));
                        }
                    case IIdentity iid:
                        using (var client = this.CreateAmiServiceClient())
                        {
                            return client.GetUsers(o => o.UserName == iid.Name).CollectionItem.OfType<SecurityUserInfo>().First().Policies.Select(o => new UpstreamPolicyInstance(iid, o.Policy, o.Grant));
                        }
                    case Act act:
                        using (var client = this.CreateHdsiServiceClient())
                        {
                            return client.Get<Act>(act.Key.Value, null).Policies.Select(o => new UpstreamPolicyInstance(act, o.Policy, PolicyGrantType.Grant));
                        }
                    case Entity ent:
                        using (var client = this.CreateHdsiServiceClient())
                        {
                            return client.Get<Entity>(ent.Key.Value, null).Policies.Select(o => new UpstreamPolicyInstance(ent, o.Policy, PolicyGrantType.Grant));
                        }
                    default:
                        throw new NotSupportedException(String.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, securable.GetType().Name));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);
            }

        }

        /// <inheritdoc/>
        public IEnumerable<IPolicy> GetPolicies()
        {
            if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return new IPolicy[0];
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    return client.FindPolicy(o => o.ObsoletionTime == null).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Key.Value, o.Oid, o.Name, o.CanOverride));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);

            }
        }

        /// <inheritdoc/>
        public IPolicy GetPolicy(string policyOid)
        {
            if(!this.IsUpstreamConfigured)
            {
                return null;
            }

            try
            {
                using (var client = this.CreateAmiServiceClient())
                {
                    return client.FindPolicy(p => p.Oid == policyOid).CollectionItem.OfType<SecurityPolicy>().Select(o => new GenericPolicy(o.Key.Value, o.Oid, o.Name, o.CanOverride)).FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_GEN), e);
            }
        }

        /// <inheritdoc/>
        public IPolicyInstance GetPolicyInstance(object securable, string policyOid) => this.GetPolicies(securable).FirstOrDefault(o => o.Policy.Oid == policyOid);

        /// <inheritdoc/>
        public bool HasPolicy(object securable, string policyOid) => this.GetPolicies(securable).Any(o => o.Policy.Oid == policyOid);

        /// <inheritdoc/>
        public void RemovePolicies(object securable, IPrincipal principal, params string[] oid)
        {
            if (securable == null)
            {
                throw new ArgumentNullException(nameof(securable));
            }
            else if (!this.IsUpstreamConfigured)
            {
                this.m_tracer.TraceWarning("Upstream is not conifgured - skipping policy check");
                return;
            }

            try
            {
                var policyKeys = this.GetPolicies(securable).Where(o => oid.Contains(o.Policy.Oid)).Select(o => o.Policy.Key).ToList();
                switch (securable)
                {
                    case SecurityDevice sd:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityDevice/{sd.Key}/policy/{o}"));
                        }
                        break;
                    case SecurityApplication sa:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityApplication/{sa.Key}/policy/{o}"));
                        }
                        break;
                    case SecurityRole sr:
                        using (var client = this.CreateRestClient(Core.Interop.ServiceEndpointType.AdministrationIntegrationService, principal))
                        {
                            policyKeys.ForEach(o => client.Delete<object>($"SecurityRole/{sr.Key}/policy/{o}"));
                        }
                        break;
                    default:
                        throw new NotSupportedException(String.Format(ErrorMessages.NOT_SUPPORTED_IMPLEMENTATION, securable.GetType().Name));
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localizationSerice.GetString(ErrorMessageStrings.SEC_POL_ASSIGN, new { securable = securable, policyOids = String.Join(";", oid) }), e);
            }
        }
    }
}
