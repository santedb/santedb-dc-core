/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: justi
 * Date: 2019-1-12
 */
using System;


namespace SanteDB.DisconnectedClient.Xamarin.Services.Attributes
{
    /// <summary>
    /// Annotates an operation on a rest service
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RestOperationAttribute : Attribute
    {
        /// <summary>
        /// Rest operation
        /// </summary>
        public RestOperationAttribute()
        {

        }

        /// <summary>
        /// Gets or sets the fault provider
        /// </summary>
        public string FaultProvider { get; set; }

        /// <summary>
        /// Filter of the HTTP method
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// URL template for the operation
        /// </summary>
        public String UriPath { get; set; }


    }
}