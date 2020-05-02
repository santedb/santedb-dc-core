/*
 * Based on OpenIZ, Copyright (C) 2015 - 2019 Mohawk College of Applied Arts and Technology
 * Copyright (C) 2019 - 2020, Fyfe Software Inc. and the SanteSuite Contributors (See NOTICE.md)
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
 * Date: 2019-11-27
 */
using SanteDB.Core.Diagnostics;
using SanteDB.DisconnectedClient;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;

namespace SanteDB.DisconnectedClient.Diagnostics
{
    /// <summary>
    /// Represents a trace listener that appends data to a file
    /// </summary>
    public class FileTraceWriter : TraceWriter
    {

        // True when disposing
        private bool m_disposing = false;

        // The text writer
        private String m_logFile;

        // Number of logs to keep
        private int m_keepLogs = 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanteDB.DisconnectedClient.Diagnostics.FileTraceListener"/> class.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="fileName">File name.</param>
        public FileTraceWriter(EventLevel filter, String fileName) : base(filter, null)
        {
            // First, we want to remove the oldest log
            String logFileBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "log", fileName + ".log");
            if (!Directory.Exists(Path.GetDirectoryName(logFileBase)))
                Directory.CreateDirectory(Path.GetDirectoryName(logFileBase));

            for (int i = this.m_keepLogs - 1; i > 0; i--)
            {
                string oldFile = String.Format("{0}.{1:000}", logFileBase, i + 1),
                    newFile = String.Format("{0}.{1:000}", logFileBase, i);
                if (File.Exists(newFile))
                {
                    if (File.Exists(oldFile))
                        File.Delete(oldFile);
                    File.Move(newFile, oldFile); // Move SanteDB.log.001 > SanteDB.log.002 ...
                }
            }
            // Move last recorded log file
            if (File.Exists(logFileBase))
                File.Move(logFileBase, String.Format("{0}.001", logFileBase));

            this.m_logFile = logFileBase;

            this.WriteTrace(EventLevel.Informational, "Startup", "SanteDB.DisconnectedClient Version: {0} logging at level [{1}]", typeof(ApplicationContext).Assembly.GetName().Version, filter);
        }

        /// <summary>
        /// Write the trace to the log file
        /// </summary>
        /// <param name="level">Level.</param>
        /// <param name="source">Source.</param>
        /// <param name="format">Format.</param>
        /// <param name="args">Arguments.</param>
        protected override void WriteTrace(System.Diagnostics.Tracing.EventLevel level, string source, string format, params object[] args)
        {
                    try
                    {
                        using (TextWriter tw = File.AppendText(this.m_logFile))
                                tw.WriteLine("{0}@{1} <{2}> [{3:o}]: {4}", source, Thread.CurrentThread.Name, level, DateTime.Now, String.Format(format, args)); // This allows other threads to add to the write queue
                    }
                    catch
                    {
                        ;
                    }
                   
        }

       
    }
}

