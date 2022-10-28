using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Client.UserInterface.Impl
{
    /// <summary>
    /// An implementation of the <see cref="IUserInterfaceInteractionProvider"/> which uses the user's console
    /// as the method of obtaining inputs and raising alerts
    /// </summary>
    public class ConsoleUserInterfaceInteractionProvider : IUserInterfaceInteractionProvider
    {
        /// <inheritdoc/>
        public string ServiceName => "Console UI Interaction";

        /// <inheritdoc/>
        public void Alert(string message)
        {
            Console.WriteLine("ALERT: {0}", message);
            Console.WriteLine("Press any key to acknowledge...");
            Console.ReadKey();
        }

        /// <inheritdoc/>
        public bool Confirm(string message)
        {
            char input = (char)0x0;
            char[] validResponses = new char[] { 'n', 'N', 'y', 'Y' };

            do
            {
                Console.WriteLine("{0}? [y]es [n]o:", message);
                input = Console.ReadKey().KeyChar;
            } while (!validResponses.Contains(input));

            return Array.IndexOf(validResponses, input) > 1;
        }

        /// <inheritdoc/>
        public string Prompt(string message, bool maskEntry = false)
        {
            Console.Write("{0}:", message);
            return Console.ReadLine();
        }

        /// <inheritdoc/>
        public void SetStatus(string statusText, float progressIndicator)
        {
            Console.WriteLine("PROGRESS: {0:#%} {1}", progressIndicator, statusText);
        }
    }
}
