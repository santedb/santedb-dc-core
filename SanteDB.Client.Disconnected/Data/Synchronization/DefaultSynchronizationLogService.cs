using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class DefaultSynchronizationLogService : ISynchronizationLogService
    {
        public void CompleteQuery(Guid queryId)
        {
            throw new NotImplementedException();
        }

        public void Delete(ISynchronizationLogEntry itm)
        {
            throw new NotImplementedException();
        }

        public ISynchronizationLogQuery FindQueryData(Type modelType, string filter)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ISynchronizationLogEntry> GetAll()
        {
            throw new NotImplementedException();
        }

        public string GetLastEtag(Type modelType, string filter = null)
        {
            throw new NotImplementedException();
        }

        public DateTime? GetLastTime(Type modelType, string filter = null)
        {
            throw new NotImplementedException();
        }

        public void Save(Type modelType, string filter, string eTag, string name, DateTime since)
        {
            throw new NotImplementedException();
        }

        public void SaveQuery(Type modelType, string filter, Guid queryId, string name, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
