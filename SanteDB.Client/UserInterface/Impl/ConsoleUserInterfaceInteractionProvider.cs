/*
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
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;
using System.Linq;

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
        public Stream SelectFile(string title, string pattern, string path)
        {
            var selectedFile = String.Empty;
            do
            {
                Console.Write($"{title} [Enter File Path]:");
                selectedFile = Console.ReadLine();
            } while (!String.IsNullOrEmpty(selectedFile) && !File.Exists(selectedFile));

            if (String.IsNullOrEmpty(selectedFile))
            {
                return File.OpenRead(selectedFile);
            }
            else
            {
                return null;
            }
        }



        /// <summary>
        /// Save a file 
        /// </summary>
        /// <returns></returns>
        public string SaveFile(String initialPath, string defaultName, Stream fileContents)
        {
            var retVal = Path.Combine(initialPath, defaultName);
            using (var fs = File.Create(retVal))
            {
                fileContents.CopyTo(fs);
            }
            return retVal;
        }

        /// <inheritdoc/>
        public void SetStatus(string taskIdentifier, string statusText, float progressIndicator)
        {
            Console.WriteLine("{0} PROGRESS: {1:0%} {2}", taskIdentifier, progressIndicator, statusText);
        }

    }
}
