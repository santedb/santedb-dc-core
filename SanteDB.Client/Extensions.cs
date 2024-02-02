using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SanteDB.Client
{
    /// <summary>
    /// Containst extension methods for the dCDR
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Will iterate through the <paramref name="exception"/> and determine whether the exception was caused by a communication/network issue
        /// </summary>
        /// <returns>True if the exception was caused by a communication exception</returns>
        /// <remarks>
        /// We need to know if an exception was a business/application error (i.e. the message was sent to the server and the server rejected it) to place it into the 
        /// dead-letter queue. However, if the exception is merely an indication of a communication exception (timeout due to slow send, proxy error, etc.) then we 
        /// don't want to pollute the outbound queue, rather we want to keep it - pause for a period of time - and retry
        /// </remarks>
        public static bool IsCommunicationException(this Exception exception)
        {
            var isCommunicationException = false;
            while (exception != null)
            {
                isCommunicationException |= exception is SocketException ||  // Socket error
                    exception is WebException we && (we.Status != WebExceptionStatus.ProtocolError) || // Web exception with a non-protocol error
                    exception is NetworkInformationException;
                exception = exception.InnerException;
            }
            return isCommunicationException;
        }

    }
}
