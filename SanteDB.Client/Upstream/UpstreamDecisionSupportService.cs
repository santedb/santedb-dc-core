using RestSrvr;
using SanteDB.Client.Upstream.Repositories;
using SanteDB.Core.Cdss;
using SanteDB.Core.Http;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Collection;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Roles;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Serialization;
using SanteDB.Rest.HDSI.Operation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.Upstream
{
    /// <summary>
    /// Represents a decision support service which uses the upstrea
    /// </summary>
    public class UpstreamDecisionSupportService : UpstreamServiceBase, IDecisionSupportService
    {
        /// <summary>
        /// DI ctor
        /// </summary>
        public UpstreamDecisionSupportService(IRestClientFactory restClientFactory, IUpstreamManagementService upstreamManagementService, IUpstreamAvailabilityProvider upstreamAvailabilityProvider, IUpstreamIntegrationService upstreamIntegrationService = null) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
        }

        /// <inheritdoc/>
        public string ServiceName => "Upstream Decision Support Service";

        /// <inheritdoc/>
        public IEnumerable<ICdssResult> Analyze(IdentifiedData collectedData, IDictionary<string, object> parameters, params ICdssLibrary[] librariesToApply)
        {
            using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
            {
                var parameterObject = new ParameterCollection();
                parameterObject.Parameters.Add(new Parameter("target", collectedData));
                parameters?.ForEach(p => parameterObject.Parameters.Add(new Parameter(p.Key, p.Value)));
                if (librariesToApply?.Any() == true)
                {
                    parameterObject.Parameters.Add(new Parameter("libraryId", librariesToApply.Select(o => o.Id).ToArray()));
                }

                CdssAnalyzeResult result = null;
                switch (collectedData)
                {
                    case Act a:
                        result = client.Post<ParameterCollection, CdssAnalyzeResult>($"Act/$analyze", parameterObject);
                        break;
                    case Patient e:
                        result = client.Post<ParameterCollection, CdssAnalyzeResult>($"Patient/$analyze", parameterObject);
                        break;
                    case Bundle b:
                        result = client.Post<ParameterCollection, CdssAnalyzeResult>($"Bundle/$analyze", parameterObject);
                        break;
                }

                foreach (var itm in result.Issues)
                {
                    yield return new CdssDetectedIssueResult(itm);
                }
                foreach (var itm in result.Propose)
                {
                    yield return new CdssProposeResult(itm);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ICdssResult> AnalyzeGlobal(IdentifiedData collectedData, IDictionary<string, object> parameters) => this.Analyze(collectedData, parameters, new ICdssLibrary[0]);

        /// <inheritdoc/>
        public CarePlan CreateCarePlan(Patient patient) => this.CreateCarePlan(patient, false);

        /// <inheritdoc/>
        public CarePlan CreateCarePlan(Patient patient, bool groupAsEncounters) => this.CreateCarePlan(patient, false, null);

        /// <inheritdoc/>
        public CarePlan CreateCarePlan(Patient patient, bool groupAsEncounters, IDictionary<string, object> parameters, params ICdssLibrary[] librariesToUse)
        {
            using (var client = base.CreateRestClient(Core.Interop.ServiceEndpointType.HealthDataService, AuthenticationContext.Current.Principal))
            {

                var requestParameters = new ParameterCollection();

                // Stored patient?
                String requestUrl = String.Empty;
                if (!patient.VersionKey.HasValue)
                {
                    requestParameters.Parameters.Add(new Parameter("targetPatient", patient));
                    requestUrl = "Patient/$generate-careplan";
                }
                else
                {
                    requestUrl = $"Patient/{patient.Key}/$generate-careplan";
                }

                requestParameters.Parameters.Add(new Parameter("asEncounters", groupAsEncounters));
                if (librariesToUse?.Any() == true)
                {
                    requestParameters.Parameters.Add(new Parameter("library", librariesToUse.Select(o => o.Uuid)));
                }

                // We should ask for the results back in VM format?
                parameters?.ForEach(p => requestParameters.Parameters.Add(new Parameter(p.Key, p.Value)));
                client.Accept = SanteDBExtendedMimeTypes.JsonViewModel;
                client.Requesting += (o, e) =>
                {
                    e.AdditionalHeaders.Add(ExtendedHttpHeaderNames.ViewModelHeaderName, "full");
                };

                var retVal = client.Post<ParameterCollection, CarePlan>(requestUrl, SanteDBExtendedMimeTypes.JsonViewModel, requestParameters);
                retVal.PreventDelayLoad();
                retVal.Relationships.ForEach(r => r.TargetAct.PreventDelayLoad());
                // Hack: Prevent delay loading on REST OPERATION CONTEXT
                //RestOperationContext.Current.Data.Add(RestMessageDispatchFormatter.VIEW_MODEL_BYPASS_DELAY_LOAD, true);
                return retVal;
            }
        }
    }
}
