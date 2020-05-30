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
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.AMI.Diagnostics;
using SanteDB.Core.Model.Serialization;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.DisconnectedClient;
using SanteDB.DisconnectedClient.Configuration;
using SanteDB.DisconnectedClient.Security.Audit;
using SanteDB.DisconnectedClient.Services;
using SanteDB.DisconnectedClient.i18n;
using SanteDB.DisconnectedClient.Security;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using SanteDB.Core.Services;
using SanteDB.Core;
using SanteDB.Core.Jobs;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security;

namespace SanteDB.DisconnectedClient.Backup
{
    /// <summary>
    /// Xamarin backup service
    /// </summary>
    public class DefaultBackupService : IBackupService, IDaemonService
    {

        /// <summary>
        /// Configuration backup service
        /// </summary>
        public String ServiceName => "Default Configuration Backup Service";

        /// <summary>
        /// Magic bytes
        /// </summary>
        private static readonly byte[] MAGIC = { (byte)'S', (byte)'D', (byte)'B', (byte)2 };

        /// <summary>
        /// True if the backup service is running
        /// </summary>
        public bool IsRunning => false;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(DefaultBackupService));

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;
        /// <summary>
        /// Fired when the service has started
        /// </summary>
        public event EventHandler Started;
        /// <summary>
        /// Fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;
        /// <summary>
        /// Fired when the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Get backup directory
        /// </summary>
        protected virtual string GetBackupDirectory(BackupMedia media)
        {
            String retVal = String.Empty;
            switch (media)
            {
                case BackupMedia.Private:
                    retVal = Path.Combine(ApplicationServiceContext.Current.GetService<IConfigurationPersister>().ApplicationDataDirectory, "backup");
                    if (!Directory.Exists(retVal))
                        Directory.CreateDirectory(retVal);
                    break;
                case BackupMedia.Public:
                    retVal = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    // Sometimes my documents isn't available
                    if (String.IsNullOrEmpty(retVal))
                        retVal = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
                    break;
                default:
                    throw new PlatformNotSupportedException("Don't support external media on this platform");
            }
            return retVal;
        }

        /// <summary>
        /// Get the last backup date
        /// </summary>
        private string GetLastBackup(BackupMedia media)
        {
            var directoryName = this.GetBackupDirectory(media);
            this.m_tracer.TraceInfo("Checking {0} for backups...", directoryName);
            System.Diagnostics.Trace.TraceInformation(directoryName);
            return Directory.GetFiles(directoryName, "*.sdbk").OrderByDescending(o => o).FirstOrDefault();

        }

        /// <summary>
        /// TAR Archive
        /// </summary>
        private void BackupDirectory(TarWriter archive, String dir, String rootDirectory)
        {
            if (Path.GetFileName(dir) == "log") return;

            // Backup
            foreach (var itm in Directory.GetDirectories(dir))
                if (Path.GetFileName(itm).Equals("backup", StringComparison.OrdinalIgnoreCase) || 
                    Path.GetFileName(itm).Equals("restore", StringComparison.OrdinalIgnoreCase))
                    this.m_tracer.TraceWarning("Skipping {0} ", itm);
                else
                     this.BackupDirectory(archive, itm, rootDirectory);

            // Add files
            foreach (var itm in Directory.GetFiles(dir))
            {
                try
                {
                    this.m_tracer.TraceVerbose("Backing up {0}", itm);
                    archive.Write(itm.Replace(rootDirectory, ""), File.OpenRead(itm), DateTime.Now);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not add file {0} to backup : {1}", itm, e);
                }
            }

        }

        /// <summary>
        /// Perform backup on the specified media
        /// </summary>
        public void Backup(BackupMedia media, String password = null)
        {

            // Make a determination that the user is allowed to perform this action
            if (AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal)
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.ExportClinicalData).Demand();

            // Get the output medium
            var directoryName = this.GetBackupDirectory(media);
            string fileName = Path.Combine(directoryName, $"sdbdc-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm")}.tar");

            // Confirm if the user really really wants to backup
            if (String.IsNullOrEmpty(password) &&
                !ApplicationContext.Current.Confirm(Strings.locale_backup_confirm)) return;

            // TODO: Audit the backup to the data to the central server
            AuditUtil.AuditDataExport();

            // Try to backup the data
            try
            {

                this.m_tracer.TraceInfo("Beginning backup to {0}..", fileName);
                ApplicationContext.Current?.SetProgress(Strings.locale_backup, 0.25f);
                // Backup folders first
                var sourceDirectory = ApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory;
                try
                {
                    using (var fs = File.Create(fileName))
                    using (var writer = new SharpCompress.Writers.Tar.TarWriter(fs, new TarWriterOptions(SharpCompress.Common.CompressionType.None, true)))
                    {
                        this.BackupDirectory(writer, sourceDirectory, sourceDirectory);

                        var appInfo = new DiagnosticReport()
                        {
                            ApplicationInfo = new DiagnosticApplicationInfo(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(o => o.DefinedTypes.Any(t => t.Name == "Program" || t.Name == "SplashActivity")) ?? typeof(SanteDBConfiguration).Assembly)
                        };

                        // Output appInfo
                        using (var ms = new MemoryStream())
                        {
                            XmlSerializer xsz = XmlModelSerializerFactory.Current.CreateSerializer(appInfo.GetType());
                            xsz.Serialize(ms, appInfo);
                            ms.Flush();
                            ms.Seek(0, SeekOrigin.Begin);
                            writer.Write(".appinfo.xml", ms, DateTime.Now);
                        }

                        // Output declaration statement
                        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes($"User {(AuthenticationContext.Current?.Principal?.Identity?.Name ?? "SYSTEM")} created this backup on {DateTime.Now}. The end user was asked to confirm this decision to backup and acknolwedges all responsibilities for guarding this file.")))
                            writer.Write("DISCLAIMER.TXT", ms, DateTime.Now);

                        // Output databases
                        var dcc = ApplicationServiceContext.Current.GetService<IDataConnectionManager>();
                        var dbBackupLocation = dcc.Backup("");
                        this.BackupDirectory(writer, dbBackupLocation, dbBackupLocation);
                        try
                        {
                            Directory.Delete(dbBackupLocation, true);
                        }
                        catch { }
                    }

                    this.m_tracer.TraceInfo("Beginning compression {0}..", fileName);

                    using (var fileStream = File.Create(Path.ChangeExtension(fileName, "sdbk")))
                    {

                        Stream outStream = fileStream;
                        try
                        {
                            // Write header
                            fileStream.Write(MAGIC, 0, MAGIC.Length);

                            // Encrypt
                            if (!String.IsNullOrEmpty(password))
                            {
                                fileStream.WriteByte(1);

                                var cryptProvider = AesCryptoServiceProvider.Create();
                                var passKey = ASCIIEncoding.ASCII.GetBytes(password);
                                passKey = Enumerable.Range(0, 32).Select(o => passKey.Length > o ? passKey[o] : (byte)0).ToArray();
                                cryptProvider.Key = passKey;

                                cryptProvider.GenerateIV();
                                fileStream.Write(BitConverter.GetBytes(cryptProvider.IV.Length), 0, 4);
                                fileStream.Write(cryptProvider.IV, 0, cryptProvider.IV.Length);
                                outStream = new CryptoStream(fileStream, cryptProvider.CreateEncryptor() , CryptoStreamMode.Write);
                            }
                            else
                                fileStream.WriteByte(0);
                            using (var fs = File.OpenRead(fileName))
                            using (var gzs = new GZipStream(outStream, CompressionMode.Compress))
                            {
                                fs.CopyTo(gzs);
                            }
                        }
                        finally
                        {
                            if (outStream != fileStream) outStream.Close();
                        }
                    }
                }
                finally
                {
                    File.Delete(fileName);
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error backing up to {0}: {1}", fileName, ex);
                throw;
            }
        }

        /// <summary>
        /// Restore the specified data
        /// </summary>
        public void Restore(BackupMedia media, String backupDescriptor = null, String password = null)
        {
            // Get the last backup
            String backupFile = null;
            if (String.IsNullOrEmpty(backupDescriptor))
                backupFile = this.GetLastBackup(media);
            else
            {
                var directoryName = this.GetBackupDirectory(media);
                backupFile = Path.ChangeExtension(Path.Combine(directoryName, backupDescriptor), ".sdbk");
                if (!File.Exists(backupFile))
                    throw new FileNotFoundException($"Cannot find backup with descriptor {backupDescriptor}");
            }

            if (!ApplicationContext.Current.Confirm(String.Format(Strings.locale_backup_restore_confirm, new FileInfo(backupFile).CreationTime.ToString("ddd MMM dd, yyyy"))))
                return;

            try
            {
                this.m_tracer.TraceInfo("Beginning restore of {0}...", backupFile);
                ApplicationContext.Current?.SetProgress(Strings.locale_backup_restore, 0.0f);
                var sourceDirectory = ApplicationContext.Current.ConfigurationPersister.ApplicationDataDirectory;

                using (var fs = File.OpenRead(backupFile))
                {
                    // Validate header
                    byte[] header = new byte[MAGIC.Length];
                    fs.Read(header, 0, MAGIC.Length);
                    if (!header.SequenceEqual(MAGIC))
                        throw new InvalidOperationException("Backup file is invalid");

                    Stream inStream = fs;
                    try
                    {
                        // Encrypted?
                        if (fs.ReadByte() == 1)
                        {
                            // Read length of IV
                            byte[] ivLengthByte = new byte[4];
                            fs.Read(ivLengthByte, 0, 4);
                            var ivLength = BitConverter.ToInt32(ivLengthByte, 0);

                            // Read IV
                            byte[] iv = new byte[ivLength];
                            fs.Read(iv, 0, ivLength);

                            // Now Create crypto stream with password
                            if (String.IsNullOrEmpty(password))
                                throw new SecurityException("This backup archive is encrypted. You must provide a password");
                            else
                            {
                                var desCrypto = AesCryptoServiceProvider.Create();
                                var passKey = ASCIIEncoding.ASCII.GetBytes(password);
                                passKey = Enumerable.Range(0, 32).Select(o => passKey.Length > o ? passKey[o] : (byte)0).ToArray();
                                desCrypto.IV = iv;
                                desCrypto.Key = passKey;
                                inStream = new CryptoStream(fs, desCrypto.CreateDecryptor(), CryptoStreamMode.Read);
                            }
                        }

                        using (var gzs = new GZipStream(inStream, CompressionMode.Decompress))
                        using (var tr = TarReader.Open(gzs))
                        {

                            // Move to next entry & copy 
                            while (tr.MoveToNextEntry())
                            {

                                this.m_tracer.TraceVerbose("Extracting : {0}", tr.Entry.Key);
                                if (tr.Entry.Key == "DISCLAIMER.TXT" || tr.Entry.Key == ".appinfo.xml") continue;
                                var destDir = Path.Combine(sourceDirectory, tr.Entry.Key.Replace('/', Path.DirectorySeparatorChar));
                                if (!Directory.Exists(Path.GetDirectoryName(destDir)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(destDir));

                                if (!tr.Entry.IsDirectory)
                                    using (var s = tr.OpenEntryStream())
                                    using (var ofs = File.Create(Path.Combine(sourceDirectory, tr.Entry.Key)))
                                        s.CopyTo(ofs);

                            }
                        }

                        // Restore passkeys
                        var dcc = ApplicationServiceContext.Current.GetService<IDataConnectionManager>();
                        dcc?.RekeyDatabases();
                    }
                    finally
                    {
                        if (inStream != fs)
                            inStream.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error restoring backup {0}: {1}", backupFile, ex);
                throw;
            }
        }

        /// <summary>
        /// Determine if backup is available on the specified media
        /// </summary>
        public bool HasBackup(BackupMedia media)
        {
            return this.GetLastBackup(media) != null;
        }

        /// <summary>
        /// Start the backup service
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

           
            ApplicationServiceContext.Current.Started += (o, e) => ApplicationServiceContext.Current.GetService<IJobManagerService>().AddJob(new DefaultBackupJob(), new TimeSpan(2,0,0));
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);
            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Get the list of backup descriptors
        /// </summary>
        public IEnumerable<string> GetBackups(BackupMedia media)
        {
            var directoryName = this.GetBackupDirectory(media);
            this.m_tracer.TraceInfo("Getting {0} for backups...", directoryName);
            return Directory.GetFiles(directoryName, "*.sdbk").OrderByDescending(o => new FileInfo(o).CreationTime).Select(o => Path.GetFileNameWithoutExtension(o));
        }

        /// <summary>
        /// Rremove t
        /// </summary>
        /// <param name="media"></param>
        /// <param name="backupDescriptor"></param>
        public void RemoveBackup(BackupMedia media, string backupDescriptor)
        {
            // Make a determination that the user is allowed to perform this action
            if (AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal)
                new PolicyPermission(System.Security.Permissions.PermissionState.Unrestricted, PermissionPolicyIdentifiers.ExportClinicalData).Demand();

            var directoryName = this.GetBackupDirectory(media);
            this.m_tracer.TraceInfo("Removing backup {0}...", backupDescriptor);

            var expectedFile = Path.ChangeExtension(Path.Combine(directoryName, backupDescriptor), ".tar.gz");
            if (!File.Exists(expectedFile))
                throw new FileNotFoundException($"Cannot find backup with descriptor {backupDescriptor}");
            else
                try
                {
                    File.Delete(expectedFile);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error removing backup descriptor {0}", backupDescriptor);
                    throw new Exception($"Error removing backup descriptor {backupDescriptor}");
                }
        }

        /// <summary>
        /// Automatic restoration
        /// </summary>
        public void AutoRestore()
        {
            // Is there a restore directory for system file?
            var autoRestore = Path.Combine(ApplicationServiceContext.Current.GetService<IConfigurationPersister>().ApplicationDataDirectory, "restore");
            if (Directory.Exists(autoRestore) && Directory.GetFiles(autoRestore, "*.sdbk").Length == 1)
            {
                try
                {
                    var bkFile = Directory.GetFiles(autoRestore, "*.sdbk")[0];
                    File.Copy(bkFile, Path.Combine(this.GetBackupDirectory(BackupMedia.Private), Path.GetFileName(bkFile)), true);
                    this.Restore(BackupMedia.Private, Path.GetFileNameWithoutExtension(bkFile), ApplicationContext.Current.Configuration.GetSection<SecurityConfigurationSection>().DeviceName);
                }
                finally
                {
                    Directory.Delete(autoRestore, true);
                }
            }

        }
    }
}
