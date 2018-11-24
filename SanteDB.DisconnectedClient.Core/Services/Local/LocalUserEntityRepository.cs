using SanteDB.Core;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Localuser entity repository
    /// </summary>
    public class LocalUserEntityRepository : GenericLocalMetadataRepository<UserEntity>
    {

        /// <summary>
        /// Validate that the user has write permission
        /// </summary>
        private void ValidateWritePermission(UserEntity entity)
        {
            var user = ApplicationServiceContext.Current.GetService<ISecurityRepositoryService>()?.GetUser(AuthenticationContext.Current.Principal.Identity);
            if (user.Key != entity.SecurityUserKey)
                this.Demand(PermissionPolicyIdentifiers.AlterIdentity);
        }

        /// <summary>
        /// Insert the user entity
        /// </summary>
        public override UserEntity Insert(UserEntity entity)
        {
            this.ValidateWritePermission(entity);
            return base.Insert(entity);
        }
        
        /// <summary>
        /// Obsolete the user entity
        /// </summary>
        public override UserEntity Obsolete(Guid key)
        {
            this.ValidateWritePermission(this.Get(key));
            return base.Obsolete(key);
        }

        /// <summary>
        /// Update the user entity
        /// </summary>
        public override UserEntity Save(UserEntity data)
        {
            this.ValidateWritePermission(data);
            return base.Save(data);
        }
    }
}
