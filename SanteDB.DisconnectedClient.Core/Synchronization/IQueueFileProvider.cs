﻿/*
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
using SanteDB.Core.Model;
using System;

namespace SanteDB.DisconnectedClient.Core.Synchronization
{
    /// <summary>
    /// Queue file provider
    /// </summary>
    public interface IQueueFileProvider
    {

        /// <summary>
        /// Save queue data
        /// </summary>
        String SaveQueueData(IdentifiedData data);

        /// <summary>
        /// Remove queue data
        /// </summary>
        void RemoveQueueData(String pathSpec);

        /// <summary>
        /// Get queue data
        /// </summary>
        IdentifiedData GetQueueData(String pathSpec, Type typeSpec);

        /// <summary>
        /// Get the raw queue data
        /// </summary>
        byte[] GetQueueData(String pathSpec);

        /// <summary>
        /// Copy queue data
        /// </summary>
        string CopyQueueData(string data);
    }
}
