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
using RestSrvr.Exceptions;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.DisconnectedClient.Exceptions;
using SanteDB.DisconnectedClient.Security.Audit;
using SanteDB.DisconnectedClient.Exceptions;
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
                        RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", authHeader);

                    return 401;
                }
                else
                {
                    return 403;
                }
            }
            else if (error is SecurityTokenException)
            {
                // TODO: Audit this
                if (enableBehavior)
                {
                    var authHeader = $"Bearer realm=\"{RestOperationContext.Current.IncomingRequest.Url.Host}\" error=\"invalid_token\" error_description=\"{error.Message}\"";
                    RestOperationContext.Current.OutgoingResponse.AddHeader("WWW-Authenticate", authHeader);
                }
                return 401;

            }
            else if (error is SecurityException)
            {
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
            else if (error is DomainStateException)
                return 503;
            else
                return 500;

        }
    }
}
