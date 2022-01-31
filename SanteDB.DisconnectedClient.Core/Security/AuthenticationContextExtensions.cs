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
 * Date: 2022-1-17
 */
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Services;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Authentication context
    /// </summary>
    public static class AuthenticationContextExtensions
    {

        // Cached device principal
        private static IPrincipal s_devicePrincipal;

        // TRacer
        private static readonly Tracer s_tracer = Tracer.GetTracer(typeof(AuthenticationContextExtensions));

        /// <summary>
        /// Enter device context, throwing any exceptions
        /// </summary>
        public static IDisposable EnterDeviceContext()
        {
            var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
            // Expired or not exists
            if (!(s_devicePrincipal is IClaimsPrincipal cp) || (cp.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                s_devicePrincipal = ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
            return AuthenticationContext.EnterContext(s_devicePrincipal);
        }

        /// <summary>
        /// Enter device context
        /// </summary>
        public static IDisposable TryEnterDeviceContext()
        {
            try
            {
                return EnterDeviceContext();
            }
            catch(Exception e)
            {
                s_tracer.TraceWarning("Could not enter device context, falling back to existing context - {0}", e);
                return AuthenticationContext.EnterContext(AuthenticationContext.Current.Principal);
            }
        }
    }
}
