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
using SanteDB.Core.Services;
using System;

namespace SanteDB.Client.UserInterface
{
    /// <summary>
    /// An interface that can provide low level dialogs for confirmations and messages
    /// </summary>
    public interface IUserInterfaceInteractionProvider : IServiceImplementation
    {

        /// <summary>
        /// Shows a confirmation dialog box with <paramref name="message"/>
        /// </summary>
        /// <param name="message">The message to prompt the user for</param>
        /// <returns>True if the user confirmed the dialog box</returns>
        bool Confirm(String message);

        /// <summary>
        /// Shows an alert message box to the 
        /// </summary>
        /// <param name="message">The message to be shown the user</param>
        void Alert(String message);

        /// <summary>
        /// Shows a prompt for the user to enter a response
        /// </summary>
        /// <param name="message">The message to prompt</param>
        /// <param name="maskEntry">True if the entry should be masked</param>
        /// <returns>The data entered by the user</returns>
        String Prompt(String message, bool maskEntry = false);

        /// <summary>
        /// Set the application status bar to to the specified value
        /// </summary>
        /// <param name="taskIdentifier">Since there can be multiple tasks ocurring - this is the task identifier</param>
        /// <param name="statusText">The text to display on the status bar</param>
        /// <param name="progressIndicator">The progress indicator</param>
        void SetStatus(String taskIdentifier, String statusText, float progressIndicator);
    }
}
