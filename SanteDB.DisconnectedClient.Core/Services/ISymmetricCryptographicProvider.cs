/*
 * Copyright 2015-2018 Mohawk College of Applied Arts and Technology
 *
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
 * User: justin
 * Date: 2018-7-31
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services
{
    /// <summary>
    /// Represents a crypto service provider that encrypts things using symmetric encryption
    /// </summary>
    public interface ISymmetricCryptographicProvider
    {

        /// <summary>
        /// Generates an initialization vector
        /// </summary>
        byte[] GenerateIV();

        /// <summary>
        /// Generates a key
        /// </summary>
        byte[] GenerateKey();

        /// <summary>
        /// Encrypts the sepcified data
        /// </summary>
        byte[] Encrypt(byte[] data, byte[] key, byte[] iv);

        /// <summary>
        /// Decrypts the specified data
        /// </summary>
        byte[] Decrypt(byte[] data, byte[] key, byte[] iv);

    }
}
