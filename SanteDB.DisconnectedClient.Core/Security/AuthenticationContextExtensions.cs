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
        /// Enter device context
        /// </summary>
        public static IDisposable TryEnterDeviceContext()
        {
            try
            {
                var appConfig = ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>();
                // Expired or not exists
                if (!(s_devicePrincipal is IClaimsPrincipal cp) || (cp.FindFirst(SanteDBClaimTypes.Expiration)?.AsDateTime().ToLocalTime() ?? DateTime.MinValue) < DateTime.Now)
                    s_devicePrincipal = ApplicationContext.Current.GetService<IDeviceIdentityProviderService>().Authenticate(appConfig.DeviceName, appConfig.DeviceSecret);
                return AuthenticationContext.EnterContext(s_devicePrincipal);
            }
            catch(Exception e)
            {
                s_tracer.TraceWarning("Could not enter device context, falling back to existing context - {0}", e);
                return AuthenticationContext.EnterContext(AuthenticationContext.Current.Principal);
            }
        }
    }
}
