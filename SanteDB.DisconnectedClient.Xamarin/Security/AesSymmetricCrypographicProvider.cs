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
using SanteDB.DisconnectedClient.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Xamarin.Security
{
    /// <summary>
    /// Represents a symmetric cryptographic provider based on AES
    /// </summary>
    public class AesSymmetricCrypographicProvider : ISymmetricCryptographicProvider
    {

        /// <summary>
        /// Decrypt the specified data using the specified key and iv
        /// </summary>
        public byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using(var aes = new AesCryptoServiceProvider())
            {
                var decryptor = aes.CreateDecryptor(key, iv);
                byte[] outputBuffer = new byte[1024];
                return decryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Encrypt the specified data using the specified key and iv
        /// </summary>
        public byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider())
            {
                var encryptor = aes.CreateEncryptor(key, iv);
                byte[] outputBuffer = new byte[1024];
                return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Generate a random IV
        /// </summary>
        public byte[] GenerateIV()
        {
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.GenerateIV();
                return aes.IV;
            }
        }

        /// <summary>
        /// Generate key
        /// </summary>
        /// <returns></returns>
        public byte[] GenerateKey()
        {
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.GenerateKey();
                return aes.Key;
            }
        }
    }
}
