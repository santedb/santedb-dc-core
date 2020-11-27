/*
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
 * Date: 2020-5-2
 */
using System.Security;

namespace SanteDB.DisconnectedClient.Exceptions
{

    /// <summary>
    /// Token security exception type.
    /// </summary>
    public enum SecurityTokenExceptionType
    {
        TokenExpired,
        InvalidSignature,
        InvalidTokenType,
        KeyNotFound,
        NotYetValid,
        InvalidClaim,
        InvalidIssuer
    }

    /// <summary>
    /// Represents an error with a token
    /// </summary>
    public class SecurityTokenException : SecurityException
    {
	    /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Exceptions.TokenSecurityException"/> class.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <param name="detail">Detail.</param>
        public SecurityTokenException(SecurityTokenExceptionType type, string detail) : base($"{type} - {detail}")
        {
            this.Type = type;
            this.Detail = detail;
        }

	    /// <summary>
        /// Details of the exception
        /// </summary>
        /// <value>The detail.</value>
        public string Detail
        {
            get;
            set;
        }

	    /// <summary>
        /// Gets or sets the type of exception.
        /// </summary>
        /// <value>The type.</value>
        public SecurityTokenExceptionType Type
        {
            get;
            set;
        }
    }
}

