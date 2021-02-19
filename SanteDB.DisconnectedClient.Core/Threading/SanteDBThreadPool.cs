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
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SanteDB.DisconnectedClient.Threading
{
    /// <summary>
    /// Represents a thread pool which is implemented separately from the default .net
    /// threadpool, this is to reduce the load on the .net framework thread pool
    /// </summary>
    [Obsolete("Use SanteDB.Core.Services.Impl.DefaultThreadPoolService", true)]
    public class SanteDBThreadPool : DefaultThreadPoolService
    {
    }
}