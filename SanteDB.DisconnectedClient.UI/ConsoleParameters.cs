﻿/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-7-23
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MohawkCollege.Util.Console.Parameters;

namespace SanteDB.DisconnectedClient.UI
{
    /// <summary>
    /// Console parameters
    /// </summary>
    public class ConsoleParameters
    {

        /// <summary>
        /// Starts the IMS in debug mode
        /// </summary>
        [Parameter("debug")]
        public bool Debug { get; set; }

        /// <summary>
        /// Reset configuration
        /// </summary>
        [Parameter("reset")]
        public bool Reset { get; set; }

        [Parameter("hdpi")]
        public bool HdpiFix { get; set; }
    }
}
