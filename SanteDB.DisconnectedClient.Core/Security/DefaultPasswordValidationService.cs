using SanteDB.Core;
using SanteDB.Core.Services;
using SanteDB.Core.Services.Impl;
using SanteDB.DisconnectedClient.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.Security
{
    /// <summary>
    /// Represents a local regex password validator
    /// </summary>
    [ServiceProvider("Default Password Validator")]
    public class DefaultPasswordValidationService : RegexPasswordValidator
    {
        /// <summary>
        /// Local password validation service
        /// </summary>
        public DefaultPasswordValidationService() : base(ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<SecurityConfigurationSection>().PasswordRegex ?? RegexPasswordValidator.DefaultPasswordPattern)
        {

        }
    }
}
