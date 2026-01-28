/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Client.Disconnected.Data.Synchronization;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Parameters;
using SanteDB.Core.Model.Roles;
using SanteDB.Rest.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Rest.Operations
{
    /// <summary>
    /// Subscribe to a single resource
    /// </summary>
    /// <remarks>Flags the reasource(s) such that it is downloaded on regular synchronization requests</remarks>
    public class SubscribeToResourceOperation : IApiChildOperation
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(SubscribeToResourceOperation));
        private readonly ISynchronizationService m_synchronizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public SubscribeToResourceOperation(ISynchronizationService synchronizationService)
        {
            this.m_synchronizationService = synchronizationService;       
        }

        /// <inheritdoc/>
        public string Name => "subscribe";

        /// <inheritdoc/>
        public ChildObjectScopeBinding ScopeBinding => ChildObjectScopeBinding.Instance;

        /// <inheritdoc/>
        public Type[] ParentTypes => new[] {
            typeof(Patient),
            typeof(Person)
        };

        /// <inheritdoc/>
        public object Invoke(Type scopingType, object scopingKey, ParameterCollection parameters)
        {
            if(scopingKey is Guid scopeUuid || Guid.TryParse(scopingKey.ToString(), out scopeUuid))
            {
                this.m_synchronizationService.SubscribeTo(scopingType, scopeUuid);
                return null;
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INVALID_TYPE, typeof(Guid), scopingKey.ToString()));
            }
        }
    }
}
