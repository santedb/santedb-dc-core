using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

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
        /// <param name="statusText">The text to display on the status bar</param>
        /// <param name="progressIndicator">The progress indicator</param>
        void SetStatus(String statusText, float progressIndicator);
    }
}
