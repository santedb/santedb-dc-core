using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Local security application repository
    /// </summary>
    public class LocalSecurityApplicationRepository : GenericLocalSecurityRepository<SecurityApplication>
    {
        protected override string WritePolicy => PermissionPolicyIdentifiers.CreateApplication;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.CreateApplication;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.CreateApplication;

    }
}
