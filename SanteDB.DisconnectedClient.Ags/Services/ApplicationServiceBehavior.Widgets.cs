/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core.Applets.Model;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Ags.Contracts;
using SanteDB.DisconnectedClient.Ags.Model;
using SanteDB.DisconnectedClient.Core;
using SanteDB.DisconnectedClient.Core.Services;
using SanteDB.DisconnectedClient.Xamarin.Data;
using SanteDB.DisconnectedClient.Xamarin.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Services
{
    /// <summary>
    /// The application services behavior
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
            return appletCollection.WidgetAssets.Select(o=>(o.Content ??  appletCollection.Resolver(o)) as AppletWidget).Where(queryExpression).ToList();
        }

        /// <summary>
        /// Gets the specified widget
        /// </summary>
        public Stream GetWidget(String widgetId)
        {
            var appletCollection = ApplicationContext.Current.GetService<IAppletManagerService>().Applets;
            var widget = appletCollection.WidgetAssets.FirstOrDefault(o => ((o.Content ?? appletCollection.Resolver(o)) as AppletWidget).Name == widgetId);
            if (widget == null)
                throw new KeyNotFoundException(widgetId);
            else
                return new MemoryStream(appletCollection.RenderAssetContent(widget, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

        }
    }
}
