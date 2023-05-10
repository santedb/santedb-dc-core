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
using SanteDB.Core.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.UserInterface.Impl
{
    /// <summary>
    /// Trace user interface provider
    /// </summary>
    public class TracerUserInterfaceInteractionProvider : IUserInterfaceInteractionProvider
    {
        private readonly Tracer m_tracer = new Tracer("UserInterface");

        /// <inheritdoc/>
        public string ServiceName => "Non-Interactive User Interface Provider";

        /// <inheritdoc/>
        public void Alert(string message)
        {
            this.m_tracer.TraceWarning("ALERT: {0}", message);
        }

        /// <inheritdoc/>
        public bool Confirm(string message)
        {
            this.m_tracer.TraceWarning("PROMPT: {0}", message);
            return true;
        }

        /// <inheritdoc/>
        public string Prompt(string message, bool maskEntry = false)
        {
            throw new NotSupportedException("Non-Interactive Environment");
        }

        /// <inheritdoc/>
        public void SetStatus(string taskIdentifier, string statusText, float progressIndicator)
        {
            this.m_tracer.TraceInfo("{0} STATUS: {1:#.#%} {2}", taskIdentifier, progressIndicator, statusText);
        }
    }
}
