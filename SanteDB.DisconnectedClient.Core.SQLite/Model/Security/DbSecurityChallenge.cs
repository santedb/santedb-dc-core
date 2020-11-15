using SanteDB.OrmLite.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.DisconnectedClient.SQLite.Model.Security
{
    /// <summary>
    /// Defines a security challenge question for the user
    /// </summary>
    [Table("security_challenge")]
    public class DbSecurityChallenge : DbBaseData
    {
        
        /// <summary>
        /// Gets or sets the challenge text
        /// </summary>
        [Column("text")]
        public String ChallengeText { get; set; }

    }


    /// <summary>
    /// Security user challenge association
    /// </summary>
    [Table("security_user_challenge")]
    public class DbSecurityUserChallengeAssoc
    {

        /// <summary>
        /// Gets the user that is associated with this challenge
        /// </summary>
        [Column("user_uuid"), ForeignKey(typeof(DbSecurityUser), nameof(DbSecurityUser.Uuid))]
        public byte[] UserUuid { get; set; }

        /// <summary>
        /// Gets or sets the associated challenge text
        /// </summary>
        [Column("challenge_uuid"), ForeignKey(typeof(DbSecurityChallenge), nameof(DbSecurityChallenge.Uuid))]
        public byte[] ChallengeUuid { get; set; }

        /// <summary>
        /// Challenge response
        /// </summary>
        [Column("response"), Secret]
        public String ChallengeResponse { get; set; }

        /// <summary>
        /// The time that the challenge will expire
        /// </summary>
        [Column("expiration")]
        public DateTime? ExpiryTime { get; set; }

        /// <summary>
        /// Query result for authentication
        /// </summary>
        public class QueryResult : DbSecurityUser
        {

            /// <summary>
            /// Challenge response
            /// </summary>
            [Column("response"), Secret]
            public String ChallengeResponse { get; set; }
        }

    }

}
