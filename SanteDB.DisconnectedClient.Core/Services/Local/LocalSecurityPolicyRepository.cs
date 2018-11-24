using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Alter policies
    /// </summary>
    public class LocalSecurityPolicyRepository : GenericLocalSecurityRepository<SecurityPolicy>
    {

        protected override string WritePolicy => PermissionPolicyIdentifiers.AlterPolicy;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.AlterPolicy;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.AlterPolicy;

    }
}
