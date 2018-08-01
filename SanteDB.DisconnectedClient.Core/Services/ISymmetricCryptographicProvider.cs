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
