using RestSrvr;
using RestSrvr.Exceptions;
using RestSrvr.Message;
using SanteDB.DisconnectedClient.Core;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Ags.Behaviors
{
    /// <summary>
    /// Checks request against magic generated 
    /// </summary>
    public class AgsMagicServiceBehavior : IServiceBehavior, IServicePolicy
    {
        /// <summary>
        /// Apply the service policy
        /// </summary>
        public void Apply(RestRequestMessage request)
        {
            if (request.Headers["X-OIZMagic"] == ApplicationContext.Current.ExecutionUuid.ToString() &&
                request.UserAgent == $"SanteDB-DC {ApplicationContext.Current.ExecutionUuid}" ||
                ApplicationContext.Current.ExecutionUuid.ToString() == ApplicationContext.Current.Configuration.GetAppSetting("http.bypassMagic"))
                ;
            else
            {
                // Something wierd with the appp, show them the nice message
                if (request.UserAgent.StartsWith("SanteDB"))
                {
                    throw new FaultException<String>(403, "Hmm, something went wrong. For security's sake we can't show the information you requested. Perhaps restarting the application will help");
                }
                else // User is using a browser to try and access this? How dare they
                {
                    RestOperationContext.Current.OutgoingResponse.ContentType = "text/html";
                    throw new FaultException<Stream>(403, new GZipStream(typeof(AgsMagicServiceBehavior).Assembly.GetManifestResourceStream("SanteDB.DisconnectedClient.Ags.Resources.antihaxor"), SharpCompress.Compressors.CompressionMode.Decompress));
                }
            }
        }

        /// <summary>
        /// Add this service behavior
        /// </summary>
        public void ApplyServiceBehavior(RestService service, ServiceDispatcher dispatcher)
        {
            dispatcher.AddServiceDispatcherPolicy(this);
        }
    }
}
