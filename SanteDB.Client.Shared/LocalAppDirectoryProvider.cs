using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Client.Shared
{
    public class LocalAppDirectoryProvider
    {
        readonly string m_AppDirectory;
        readonly string m_DataDirectory;
        readonly string m_ConfigDirectory;

        public LocalAppDirectoryProvider()
            : this("dc-win32")
        {

        }

        public LocalAppDirectoryProvider(string directoryName)
        {
            m_AppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SanteDB", directoryName);
            m_ConfigDirectory = Path.Combine(m_AppDirectory, "config");
            m_DataDirectory = Path.Combine(m_AppDirectory, "data");

            AppDomain.CurrentDomain.SetData("DataDirectory", m_DataDirectory);
            AppDomain.CurrentDomain.SetData("ConfigDirectory", m_ConfigDirectory);
        }

        private void EnsureDirectoriesAreCreated()
        {
            Directory.CreateDirectory(m_AppDirectory);
            Directory.CreateDirectory(m_ConfigDirectory);
            Directory.CreateDirectory(m_DataDirectory);
        }

        public string GetConfigFilePath()
        {
            EnsureDirectoriesAreCreated();

            return Path.Combine(m_ConfigDirectory, "santedb.config");
        }

        

        public string GetConfigDirectory()
        {
            EnsureDirectoriesAreCreated();
            return m_ConfigDirectory;
        }

        public string GetDataDirectory()
        {
            EnsureDirectoriesAreCreated();
            return m_DataDirectory;
        }
    }
}
