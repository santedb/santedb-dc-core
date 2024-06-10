/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-6-21
 */
namespace SanteDB.Client.Tickles
{
    /// <summary>
    /// Represents the type of tickle
    /// </summary>
    public enum TickleType
    {
        /// <summary>
        /// Represents an informational tickle, which can be dismissed by the user
        /// </summary>
        Information = 1,
        /// <summary>
        /// Represents a danger tickle
        /// </summary>
        Danger = 2,
        /// <summary>
        /// Toast
        /// </summary>
        Toast = 4,
        /// <summary>
        /// Represents a task the user must perform before the tickle can be dismissed
        /// </summary>
        Task = 8,
        /// <summary>
        /// Represents a tickle related to security
        /// </summary>
        Security = 16,
        /// <summary>
        /// The tickle represents a security tickle
        /// </summary>
        SecurityTask = Task | Security,
        /// <summary>
        /// The tickle represents an error related to a security control
        /// </summary>
        SecurityError = Danger | Security,
        /// <summary>
        /// The tickle represents an informational message related to security control
        /// </summary>
        SecurityInformation = Information | Security

    }
}
