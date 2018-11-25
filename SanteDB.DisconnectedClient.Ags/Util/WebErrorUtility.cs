using RestSrvr;
using RestSrvr.Exceptions;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Security;
using SanteDB.DisconnectedClient.Core.Exceptions;
using SanteDB.DisconnectedClient.Core.Security.Audit;
using SanteDB.DisconnectedClient.Xamarin.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security;

namespace SanteDB.DisconnectedClient.Ags.Util
{
    /// <summary>
    /// Represents a web-error utility function
    /// </summary>
    public static class WebErrorUtility
    {

        /// <summary>
        /// Classify the provided exception and set any error headers
        /// </summary>
        /// <param name="enableBehavior">When true, instructs the classifier to append headers and audit the action</param>
        /// <param name="error">The error to classify</param>
        public static int ClassifyException(Exception error, bool enableBehavior = true)
        {

            // Formulate appropriate response
            if (error is PolicyViolationException)
            {
                var pve = error as PolicyViolationException;
                if (pve.PolicyDecision == SanteDB.Core.Model.Security.PolicyGrantType.Elevate ||
                    pve.PolicyId == PermissionPolicyIdentifiers.Login)
                {
                    // Ask the user to elevate themselves
                    var authHeader = $"Bearer realm=\"{RestOperationContext.Current.IncomingRequest.Url.Host}\" error_code=\"insufficient_scope\" scope=\"{pve.PolicyId}\"";
                    if (enableBehavior)
                    {
                        AuditUtil.AuditRestrictedFunction(error, RestOperationContext.Current.IncomingRequest.Url, authHeader);
                        RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", authHeader);
                    }
                    return 401;
                }
                else
                {
                    if (enableBehavior)
                        AuditUtil.AuditRestrictedFunction(error, RestOperationContext.Current.IncomingRequest.Url, "HTTP-403");
                    return 403;
                }
            }
            else if (error is SecurityTokenException)
            {
                // TODO: Audit this
                if (enableBehavior)
                {
                    var authHeader = $"Bearer realm=\"{RestOperationContext.Current.IncomingRequest.Url.Host}\" error=\"invalid_token\" error_description=\"{error.Message}\"";
                    AuditUtil.AuditRestrictedFunction(error, RestOperationContext.Current.IncomingRequest.Url, authHeader);
                    RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", authHeader);
                }
                return 401;

            }
            else if (error is SecurityException)
            {
                if (enableBehavior)
                    AuditUtil.AuditRestrictedFunction(error, RestOperationContext.Current.IncomingRequest.Url, "HTTP-403");
                return 403;
            }
            else if (error is LimitExceededException)
            {
                if (enableBehavior)
                {
                    RestOperationContext.Current.OutgoingResponse.StatusDescription = "Too Many Requests";
                    RestOperationContext.Current.OutgoingResponse.Headers.Add("Retry-After", "1200");
                }
                return 429;
            }
            else if (error is UnauthorizedAccessException)
            {
                if (enableBehavior)
                    AuditUtil.AuditRestrictedFunction(error, RestOperationContext.Current.IncomingRequest.Url, "HTTP-403");
                return 403;
            }
            else if (error is FaultException)
            {
                return (error as FaultException).StatusCode;
            }
            else if (error is Newtonsoft.Json.JsonException ||
                error is System.Xml.XmlException)
                return 400;
            else if (error is DuplicateKeyException || error is DuplicateNameException)
                return 409;
            else if (error is FileNotFoundException || error is KeyNotFoundException)
                return 404;
            else if (error is DetectedIssueException)
                return 422;
            else if (error is NotImplementedException)
                return 501;
            else if (error is NotSupportedException)
                return 405;
            else
                return 500;

        }
    }
}
