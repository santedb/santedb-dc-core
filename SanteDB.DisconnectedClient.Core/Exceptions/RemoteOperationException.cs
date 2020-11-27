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
 * Date: 2020-5-13
 */
using System;

namespace SanteDB.DisconnectedClient.Exceptions
{
    /// <summary>
    /// Remote operation exception
    /// </summary>
    public class RemoteOperationException : Exception
    {
	    /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException()
        {

        }

	    /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException(string message) : base(message)
        {

        }

	    /// <summary>
        /// Creates a new remote operation exception
        /// </summary>
        public RemoteOperationException(string message, Exception cause) : base (message, cause)
        {

        }
    }
}
