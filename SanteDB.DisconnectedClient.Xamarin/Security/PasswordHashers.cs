/*
 * Copyright 2015-2019 Mohawk College of Applied Arts and Technology
 * Copyright 2019-2019 SanteSuite Contributors (See NOTICE)
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
 * User: Justin Fyfe
 * Date: 2019-8-8
 */
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using System;
using System.Security.Cryptography;
using System.Text;

namespace SanteDB.DisconnectedClient.Xamarin.Security
{
    /// <summary>
    /// SHA256 password hasher service
    /// </summary>
    public class SHA256PasswordHasher : IPasswordHashingService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SHA256 Password Encoding Service";

        #region IPasswordHashingService implementation
        /// <summary>
        /// Compute hash
        /// </summary>
        /// <returns>The hash.</returns>
        /// <param name="password">Password.</param>
        public string ComputeHash(string password)
        {
            SHA256 hasher = SHA256.Create();
            return BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLower();
        }
        #endregion
    }

    /// <summary>
    /// SHA1 password hasher service
    /// </summary>
    public class SHAPasswordHasher : IPasswordHashingService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "SHA1 Password Encoding Service";

        #region IPasswordHashingService implementation
        /// <summary>
        /// Compute hash
        /// </summary>
        /// <returns>The hash.</returns>
        /// <param name="password">Password.</param>
        public string ComputeHash(string password)
        {
            SHA1 hasher = SHA1.Create();
            return BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLower();
        }
        #endregion
    }

    /// <summary>
    /// Plain text password hasher service
    /// </summary>
    public class PlainTextPasswordHasher : IPasswordHashingService
    {
        /// <summary>
        /// Get the service name
        /// </summary>
        public String ServiceName => "Plain Text Password Encoding Service";

        #region IPasswordHashingService implementation
        /// <summary>
        /// Compute hash
        /// </summary>
        /// <returns>The hash.</returns>
        /// <param name="password">Password.</param>
        public string ComputeHash(string password)
        {
            return password;
        }
        #endregion
    }
}

