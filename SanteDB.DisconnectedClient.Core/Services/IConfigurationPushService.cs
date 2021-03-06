﻿/*
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2021-2-9
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Services
{

    /// <summary>
    /// An interface which can push and/or configure the DCG configuration with a third party
    /// </summary>
    public interface IConfigurationPushService
    {

        /// <summary>
        /// Configure the specified target device with the specified username and software
        /// </summary>
        List<Uri> Configure(Uri targetUri, String userName, String password, IDictionary<String, Object> configuration);

        /// <summary>
        /// Gets the specified remote software package 
        /// </summary>
        IConfigurationTarget GetTarget(Uri targetUri);

        // TODO: Add more methods to this which will be more useful for future configuration solutions
    }
}
