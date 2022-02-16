﻿/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
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
 * Date: 2021-8-27
 */
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Services.Local
{
    /// <summary>
    /// Represents a local implementation of a job management service
    /// </summary>
    [Obsolete("Use SanteDB.Core.Jobs.DefaultJobManagerService", true)]
    public class LocalJobManagerService : DefaultJobManagerService
    {
        /// <summary>
        /// Creates a new local job manager
        /// </summary>
        public LocalJobManagerService(IThreadPoolService threadPool, IServiceManager serviceManager, IJobStateManagerService jobStateManager = null, IJobScheduleManager cronTabManager = null) : base(threadPool, serviceManager, jobStateManager, cronTabManager)
        {
        }
    }
}
