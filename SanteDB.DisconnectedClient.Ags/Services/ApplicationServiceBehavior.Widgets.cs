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
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.Data;
using SanteDB.DisconnectedClient.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// DCG Application Services Interface Behavior
    /// </summary>
    public partial class ApplicationServiceBehavior
    {

        /// <summary>
        /// Gets all widgets
        /// </summary>
        public List<AppletWidget> GetWidgets()
        {
            var appletCollection = ApplicationContext.Current.GetService<IAppletManagerService>().Applets;
            var httpq = NameValueCollection.ParseQueryString(RestOperationContext.Current.IncomingRequest.Url.Query);
            var queryExpression = QueryExpressionParser.BuildLinqExpression<AppletWidget>(httpq).Compile();
            var pdp = ApplicationServiceContext.Current.GetService<IPolicyDecisionService>();
            var retVal = appletCollection.WidgetAssets
                .Where(o=>o.Policies?.Any(p=>pdp.GetPolicyOutcome(AuthenticationContext.Current.Principal, p) != SanteDB.Core.Model.Security.PolicyGrantType.Grant) != true)
                .Select(o => (o.Content ?? appletCollection.Resolver(o)) as AppletWidget)
                .Where(queryExpression);

            // Filter by permission
            // Now order by priority to get most preferred
            return retVal
                .GroupBy(o=>o.Name)
                .Select(o=>o.OrderByDescending(w=>w.Priority).First())
                .OrderBy(w=>w.Order)
                .ToList();
        }

        /// <summary>
        /// Gets the specified widget
        /// </summary>
        public Stream GetWidget(String widgetId)
        {
            var appletCollection = ApplicationContext.Current.GetService<IAppletManagerService>().Applets;
            var widget = appletCollection.WidgetAssets.Select(o => new { W = (o.Content ?? appletCollection.Resolver(o)) as AppletWidget, A = o }).Where(o=>o.W.Name == widgetId);

            if (widget.Count() == 0)
                throw new KeyNotFoundException(widgetId);
            else
                return new MemoryStream(appletCollection.RenderAssetContent(widget.OrderByDescending(o=>o.W.Priority).First().A, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

        }
    }
}
