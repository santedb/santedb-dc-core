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
        public void SetStatus(string statusText, float progressIndicator)
        {
            this.m_tracer.TraceInfo("STATUS: {0:#.#%} {1}", progressIndicator, statusText);
        }
    }
}
