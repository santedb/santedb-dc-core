/*
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Client.Tickles
{
    /// <summary>
    /// An implementation of the <see cref="ITickleService"/> which uses memory structures 
    /// to temporarily hold tickles for the user interface
    /// </summary>
    public class InMemoryTickleService : ITickleService
    {
        // Tickles
        private readonly ConcurrentDictionary<Guid, Tickle> m_tickles = new ConcurrentDictionary<Guid, Tickle>();

        /// <inheritdoc/>
        public string ServiceName => "Default Tickle Service";

        /// <inheritdoc/>
        public void DismissTickle(Guid tickleId) => this.m_tickles.TryRemove(tickleId, out _);

        /// <inheritdoc/>
        public IEnumerable<Tickle> GetTickles(Expression<Func<Tickle, bool>> filter) => this.m_tickles.Values.Where(filter.Compile());

        /// <inheritdoc/>
        public void SendTickle(Tickle tickle)
        {
            if (tickle.Id == Guid.Empty)
            {
                tickle.Id = Guid.NewGuid();
            }
            this.m_tickles.TryAdd(tickle.Id, tickle);
        }
    }
}
