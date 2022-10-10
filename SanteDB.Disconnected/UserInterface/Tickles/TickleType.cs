using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Disconnected.UserInterface.Tickles
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
        /// The tickle is a security task which occurred
        /// </summary>
        SecurityTask = Task | Security,
        /// <summary>
        /// The tickle is a security error
        /// </summary>
        SecurityError = Danger | Security,
        /// <summary>
        /// The tickle is a security / information tickle
        /// </summary>
        SecurityInformation = Information | Security

    }
}
