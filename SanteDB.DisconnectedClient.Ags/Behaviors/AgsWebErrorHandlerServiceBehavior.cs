using RestSrvr;
using RestSrvr.Message;
using SanteDB.Core.Applets.Services;
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient.Ags.Util;
using SanteDB.DisconnectedClient.Core;
using System;
using System.IO;
using System.Linq;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Represents a web error handler that is intended for intercepting web errors
    /// </summary>
    public class AgsWebErrorHandlerServiceBehavior : IServiceBehavior, IServiceErrorHandler
    {
        /// <summary>
        /// Apply the service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.ErrorHandlers.Insert(0, this);
        }

        /// <summary>
        /// True if this service can handle the error
        /// </summary>
        public bool HandleError(Exception error)
        {
            var errCode = WebErrorUtility.ClassifyException(error, false);
            return ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.ErrorAssets).Any(o => o.ErrorCode == errCode);
        }

        /// <summary>
        /// Provide the fault
        /// </summary>
        public bool ProvideFault(Exception error, RestResponseMessage response)
        {
            var errCode = WebErrorUtility.ClassifyException(error, true);
            var hdlr = ApplicationContext.Current.GetService<IAppletManagerService>().Applets.SelectMany(o => o.ErrorAssets).FirstOrDefault(o => o.ErrorCode == errCode);

            // Grab the asset handler
            try
            {
                response.Body = new MemoryStream(new byte[0]);
                RestOperationContext.Current.OutgoingResponse.Redirect(hdlr.Asset);
                return true;
            }
            catch (Exception e)
            {
                Tracer.GetTracer(typeof(AgsWebErrorHandlerServiceBehavior)).TraceError("Could not provide fault: {0}", e.ToString());
                throw;
            }
        }
    }
}
