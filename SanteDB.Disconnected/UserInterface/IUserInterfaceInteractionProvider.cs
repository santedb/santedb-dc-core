﻿using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.UserInterface
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
        /// Set the application status bar to to the specified value
        /// </summary>
        /// <param name="statusText">The text to display on the status bar</param>
        /// <param name="progressIndicator">The progress indicator</param>
        void SetStatus(String statusText, float progressIndicator);
    }
}
