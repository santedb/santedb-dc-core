using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Security.Session
{
    /// <summary>
    /// Represents an in-memory session
    /// </summary>
    public class MemorySession : ISession
    {

        // Claims for this object
        private List<IClaim> m_claims = new List<IClaim>();

        /// <summary>
        /// Create a new memory session
        /// </summary>
        internal MemorySession(Guid id, DateTime notBefore, DateTime notAfter, byte[] refreshToken, IClaim[] claims, IPrincipal principal)
        {
            this.m_claims = new List<IClaim>(claims);
            this.Id = id.ToByteArray();
            this.NotBefore = notBefore;
            this.NotAfter = notAfter;
            this.RefreshToken = refreshToken;
            this.Principal = principal;
        }

        /// <summary>
        /// Get the refresh token
        /// </summary>
        internal String RefreshTokenString => BitConverter.ToString(this.RefreshToken).Replace("-", ""); 

        /// <summary>
        /// Gets the identifier of the session
        /// </summary>
        public byte[] Id { get; private set; }

        /// <summary>
        /// The session is not valid before this time
        /// </summary>
        public DateTimeOffset NotBefore { get; private set; }

        /// <summary>
        /// The session is not valid after this time
        /// </summary>
        public DateTimeOffset NotAfter { get; private set; }

        /// <summary>
        /// The session refresh token
        /// </summary>
        public byte[] RefreshToken { get; private set; }

        /// <summary>
        /// Claims for this session
        /// </summary>
        public IClaim[] Claims => this.m_claims.ToArray();

        /// <summary>
        /// The princpal which this session wraps
        /// </summary>
        internal IPrincipal Principal { get; private set; }
    }

}
