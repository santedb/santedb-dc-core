using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Extension methods for syntactic sugar in the OAuth namespace of SanteDB.Client
    /// </summary>
    internal static class ClientOAuthExtensions
    {
        /// <summary>
        /// Use <see cref="HttpListenerResponse.AppendHeader(string, string)"/> to set a cookie manually. This bypasses the default HTTP listener cookie logic which conflicts with some user-agents (ex. Chrome).
        /// </summary>
        /// <param name="response">The HTTP Listener Response to append the header to.</param>
        /// <param name="name">The name of the cookie.</param>
        /// <param name="value">The value of the cookie. Null will set the value to nothing in the response and clears the cookie.</param>
        /// <param name="maxAge">The number of seconds the cookie should live for. The default value is 0 which will remove the cookie from the user-agent.</param>
        /// <param name="path">The path of the cookie. The default is / which sets the cookie for the entire host.</param>
        /// <returns>The <see cref="HttpListenerResponse"/> provided in <paramref name="response"/></returns>
        /// <remarks>This method appends the HttpOnly and Discard attributes to the Set-Cookie header. See https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie for details.</remarks>
        public static HttpListenerResponse SetAuthCookie(this HttpListenerResponse response, string name, string value = null, int maxAge = 0, string path = "/")
        {
            if (null != response)
                response.AppendHeader("Set-Cookie", $"{name}={value ?? string.Empty}; Max-Age={maxAge}; Path={path}; HttpOnly; Discard");

            return response;
        }
    }
}
