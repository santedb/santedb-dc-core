﻿/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Portions Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
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
 */
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SanteDB.Client.Tickles
{
    /// <summary>
    /// Represents a service which provides tickles for the user (popup messages)
    /// </summary>
    public interface ITickleService : IServiceImplementation
    {
        /// <summary>
        /// Dismiss a tickle
        /// </summary>
        void DismissTickle(Guid tickleId);

        /// <summary>
        /// Send a tickle to the user screen
        /// </summary>
        void SendTickle(Tickle tickle);

        /// <summary>
        /// Get tickles
        /// </summary>
        IEnumerable<Tickle> GetTickles(Expression<Func<Tickle, bool>> filter);
    }
}
