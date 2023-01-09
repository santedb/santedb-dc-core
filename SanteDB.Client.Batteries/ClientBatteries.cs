using SanteDB.Client.Configuration.Upstream;
using SanteDB.Core;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SanteDB.Client.Batteries
{
    /// <summary>
    /// Client application context 
    /// </summary>
    public static class ClientBatteries 
    {
        /// <summary>
        /// The <see cref="AppDomain.CurrentDomain"/> data string for the data directory
        /// </summary>
        public const string DATA_DIRECTORY = "DataDirectory";
        /// <summary>
        /// The <see cref="AppDomain.CurrentDomain"/> data key for configuration directory
        /// </summary>
        public const string CONFIG_DIRECTORY = "ConfigDirectory";

        /// <summary>
        /// Upstream credentials
        /// </summary>
        internal static UpstreamCredentialConfiguration UpstreamCredentials { get; private set; }

        /// <summary>
        /// Set data
        /// </summary>
        public static void Initialize(String dataDirectory, String configDirectory, UpstreamCredentialConfiguration defaultUpstreamCredentials)
        {
            AppDomain.CurrentDomain.SetData(DATA_DIRECTORY, dataDirectory);
            AppDomain.CurrentDomain.SetData(CONFIG_DIRECTORY, configDirectory);
            UpstreamCredentials = defaultUpstreamCredentials;
        }

        /// <summary>
        /// Attempts to restore the environment from <paramref name="configDirectory"/>
        /// </summary>
        /// <remarks>After a Windows Update, if the software is running in C:\Windows\SysWow64\config\... then an update will move
        /// the directory to C:\Windows.old\ - the method will attempt to restore this</remarks>
        /// <returns></returns>
        public static bool RestoreEnvironment()
        {

            var configDirectory = AppDomain.CurrentDomain.GetData(CONFIG_DIRECTORY)?.ToString();
            var dataDirectory = AppDomain.CurrentDomain.GetData(DATA_DIRECTORY)?.ToString();
            if(String.IsNullOrEmpty(configDirectory) || String.IsNullOrEmpty(dataDirectory))
            {
                throw new InvalidOperationException($"{CONFIG_DIRECTORY} and {DATA_DIRECTORY} AppDomain parameters must be set");
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Path in Windows.old
                var oldPath = configDirectory.Replace(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Path.Combine(Path.ChangeExtension(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "OLD"), "Windows"))
                    .ToUpper();

                if (Environment.Is64BitOperatingSystem && Environment.Is64BitProcess)
                    oldPath = oldPath.Replace("SYSTEM32", "SYSWOW64") // HACK: System folders are rewritten but the backup folders are not
                    ;

                try
                {
                    Trace.WriteLine($"New configuration at {configDirectory} doesn't exist");
                    Trace.WriteLine($"Checking for old configuration at {oldPath}...");

                    if (File.Exists(oldPath))
                    {
                        Trace.WriteLine($"Old configuration at {oldPath} found, will restore...");

                        // Copy the config file
                        if (!Directory.Exists(Path.GetDirectoryName(configDirectory)))
                            Directory.CreateDirectory(Path.GetDirectoryName(configDirectory));
                        File.Copy(oldPath, configDirectory);

                        var oldDataPath = dataDirectory.Replace(
                            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                            Path.Combine(Path.ChangeExtension(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "OLD"), "Windows"))
                            .ToUpper()
                            .Replace("SYSTEM32", "SYSWOW64");

                        if (Directory.Exists(dataDirectory))
                            Directory.Delete(dataDirectory, true);
                        Directory.Move(oldDataPath, dataDirectory);
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"No configuration at {oldPath} to restore...", "RESTORE_UPDATE");

                        return false;
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"ERROR: Checking for old configuration at {oldPath}...");
                    throw new Exception($"Could not restore files from WINDOWS.OLD please consult system administrator", e);
                }
            }
            else
                return false;
        }
    }
}
