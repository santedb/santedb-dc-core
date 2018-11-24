using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Local security device repository
    /// </summary>
    public class LocalSecurityDeviceRepository : GenericLocalSecurityRepository<SecurityDevice>
    {
        protected override string WritePolicy => PermissionPolicyIdentifiers.CreateDevice;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.CreateDevice;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.CreateDevice;

    }
}
