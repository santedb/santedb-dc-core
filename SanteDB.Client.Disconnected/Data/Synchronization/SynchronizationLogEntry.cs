using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    public class SynchronizationLogEntry : IdentifiedData, ISynchronizationLogEntry
    {
        public string ResourceType { get; set; }

        public DateTime LastSync { get; set; }

        public string LastETag { get; set; }

        public string Filter { get; set; }

        public ServiceEndpointType Endpoint { get; set; }

        public override DateTimeOffset ModifiedOn => LastSync;
    }
}
