using Newtonsoft.Json.Linq;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace SanteDB.Client.OAuth
{
    /// <summary>
    /// Token claims principal.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OAuthClaimsPrincipal : SanteDBClaimsPrincipal
    {
        public DateTimeOffset ExpiresAt { get; }
        public DateTimeOffset RenewAfter { get; }


        // Claim map
        private readonly Dictionary<String, String> claimMap = new Dictionary<string, string>() {
            { "unique_name", SanteDBClaimTypes.DefaultNameClaimType },
            { "role", SanteDBClaimTypes.DefaultRoleClaimType },
            { "sub", SanteDBClaimTypes.Sid },
            { "authmethod", SanteDBClaimTypes.AuthenticationMethod },
            { "exp", SanteDBClaimTypes.Expiration },
            { "nbf", SanteDBClaimTypes.AuthenticationInstant },
            { "email", SanteDBClaimTypes.Email },
            { "tel", SanteDBClaimTypes.Telephone }
        };

        // The token
        private String m_idToken;

        // Access token
        private String m_accessToken;

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        public String RefreshToken { get; private set; }

        public bool NeedsRenewal => DateTimeOffset.Now >= RenewAfter;
        public bool IsExpired => DateTimeOffset.Now >= ExpiresAt;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenClaimsPrincipal"/> class.
        /// </summary>
        /// <param name="idToken">Token.</param>
        /// <param name="tokenType">Token type.</param>
        public OAuthClaimsPrincipal(String accessToken, String idToken, String tokenType, String refreshToken, int expiresIn) : base()
        {
            if (String.IsNullOrEmpty(idToken))
            {
                throw new ArgumentNullException(nameof(idToken));
            }
            else if (String.IsNullOrEmpty(tokenType))
            {
                throw new ArgumentNullException(nameof(tokenType));
            }
            else if (tokenType != "urn:ietf:params:oauth:token-type:jwt" &&
                tokenType != "bearer")
            {
                throw new ArgumentOutOfRangeException(nameof(tokenType), "expected urn:ietf:params:oauth:token-type:jwt");
            }

            // Token
            this.m_idToken = idToken;
            this.m_accessToken = accessToken;

            String[] tokenObjects = idToken.Split('.');
            // Correct each token to be proper B64 encoding
            for (int i = 0; i < tokenObjects.Length; i++)
            {
                tokenObjects[i] = tokenObjects[i].PadRight(tokenObjects[i].Length + (tokenObjects[i].Length % 4), '=').Replace("===", "=");
            }

            JObject headers = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(tokenObjects[0]))),
                body = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(tokenObjects[1])));


            // Attempt to get the certificate
            if (((String)headers["alg"]).StartsWith("RS"))
            {
                var cert = X509CertificateUtils.FindCertificate(X509FindType.FindByThumbprint, StoreLocation.CurrentUser, StoreName.My, headers["x5t"].ToString());
                //if (cert == null)
                //	throw new SecurityTokenException(SecurityTokenExceptionType.KeyNotFound, String.Format ("Cannot find certificate {0}", headers ["x5t"]));
                // TODO: Verify signature
            }
            else if (((String)headers["alg"]).StartsWith("HS"))
            {
                // TODO: Verify key

            }

            // Parse the jwt
            List<IClaim> claims = new List<IClaim>();

            foreach (var kf in body)
            {
                String claimName = kf.Key;
                if (!claimMap.TryGetValue(kf.Key, out claimName))
                {
                    claims.AddRange(this.ProcessClaim(kf, kf.Key));
                }
                else
                {
                    claims.AddRange(this.ProcessClaim(kf, claimName));
                }
            }

            IClaim expiryClaim = claims.Find(o => o.Type == SanteDBClaimTypes.Expiration),
                notBeforeClaim = claims.Find(o => o.Type == SanteDBClaimTypes.AuthenticationInstant);

            if (expiryClaim == null || notBeforeClaim == null)
            {
                throw new SecurityException("Missing NBF or EXP claim");
            }
            else
            {
                DateTime expiry = expiryClaim.AsDateTime().ToLocalTime(),
                    notBefore = notBeforeClaim.AsDateTime().ToLocalTime();

                if (expiry == null || expiry < DateTime.Now)
                {
                    throw new SecurityException("Token expired");
                }
                else if (notBefore == null || Math.Abs(notBefore.Subtract(DateTime.Now).TotalMinutes) > 3) //TODO: Sort out skew
                {
                    throw new SecurityException("Token cannot yet be used (issued in the future)");
                }
            }
            this.RefreshToken = refreshToken;
            this.AddIdentity(new SanteDBClaimsIdentity(body["unique_name"]?.Value<String>().ToLower() ?? body["sub"]?.Value<String>().ToLower(), true, "OAUTH", claims));

            this.ExpiresAt = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(expiresIn));
            this.RenewAfter = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(expiresIn / 2));
            
        }

        /// <summary>
        /// Processes the claim.
        /// </summary>
        /// <returns>The claim.</returns>
        /// <param name="jwtClaim">Jwt claim.</param>
        public IEnumerable<IClaim> ProcessClaim(KeyValuePair<String, JToken> jwtClaim, String claimType)
        {
            List<IClaim> retVal = new List<IClaim>();
            if (jwtClaim.Value is JArray)
            {
                foreach (var val in jwtClaim.Value as JArray)
                {
                    retVal.Add(new SanteDBClaim(claimType, (String)val));
                }
            }
            else
            {
                retVal.Add(new SanteDBClaim(claimType, jwtClaim.Value.ToString()));
            }

            return retVal;
        }

        /// <summary>
        /// Represent the token claims principal as a string (the access token itself)
        /// </summary>
        /// <returns>To be added.</returns>
        /// <remarks>To be added.</remarks>
        public override string ToString()
        {
            return this.m_accessToken;
        }

    }
}
