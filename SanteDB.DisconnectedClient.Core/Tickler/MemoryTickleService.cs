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
using SanteDB.DisconnectedClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.DisconnectedClient.Tickler
{
    /// <summary>
    /// Represents a tickle service that just uses memory
    /// </summary>
    public class MemoryTickleService : ITickleService
    {

        /// <summary>
        /// Gets the name of the service 
        /// </summary>
        public String ServiceName => "Default Tickle Service";

        // Tickles
        private List<Tickle> m_tickles = new List<Tickle>();

        /// <summary>
        /// Dismiss a tickle
        /// </summary>
        public void DismissTickle(Guid tickleId)
        {
            lock (this.m_tickles)
                this.m_tickles.RemoveAll(o => o.Id == tickleId);
        }

        /// <summary>
        /// Get tickles
        /// </summary>
        public IEnumerable<Tickle> GetTickles(Expression<Func<Tickle, bool>> filter)
        {
            return this.m_tickles.Where(filter.Compile()).Where(o => o.Expiry > DateTime.Now);
        }

        /// <summary>
        /// Send a tickle
        /// </summary>
        /// <param name="tickle"></param>
        public void SendTickle(Tickle tickle)
        {
            lock (this.m_tickles)
                this.m_tickles.Add(tickle);
        }
    }
}
