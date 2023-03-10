﻿/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Classification of the synchronization type
    /// </summary>
    [Flags]
    public enum SynchronizationPattern
    {
        /// <summary>
        /// Inbound queue - The source of the queue is the upstream
        /// </summary>
        UpstreamToLocal = 0x1,
        /// <summary>
        /// Outbound queue - The destination of this queue is to the upstream
        /// </summary>
        LocalToUpstream = 0x2,
        /// <summary>
        /// The queue is for local-local communication
        /// </summary>
        LocalOnly = 0x4,
        /*
        /// <summary>
        /// 
        /// </summary>
        Reserved = 0x8,
        */
        /// <summary>
        /// The queue is both for inbound and outbound 
        /// </summary>
        BiDirectional = LocalToUpstream | UpstreamToLocal,
        /// <summary>
        /// The queue is a deadletter queue.
        /// </summary>
        DeadLetter = 0x80 | LocalOnly,
        /// <summary>
        /// All Queue patterns except for <see cref="DeadLetter"/>
        /// </summary>
        All = BiDirectional | LocalOnly

    }
}
