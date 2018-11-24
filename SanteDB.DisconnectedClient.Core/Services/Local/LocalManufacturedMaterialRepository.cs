﻿using SanteDB.Core.Model.Entities;
using SanteDB.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Local
{
    /// <summary>
    /// Local material persistence service
    /// </summary>
    public class LocalManufacturedMaterialRepository : GenericLocalNullifiedRepository<ManufacturedMaterial>
    {
        protected override string QueryPolicy => PermissionPolicyIdentifiers.QueryMaterials;
        protected override string ReadPolicy => PermissionPolicyIdentifiers.ReadMaterials;
        protected override string WritePolicy => PermissionPolicyIdentifiers.WriteMaterials;
        protected override string DeletePolicy => PermissionPolicyIdentifiers.DeleteMaterials;
        protected override string AlterPolicy => PermissionPolicyIdentifiers.WriteMaterials;
    }
}
