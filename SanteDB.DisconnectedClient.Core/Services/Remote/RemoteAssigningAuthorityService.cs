using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.DisconnectedClient.Core.Services.Remote
{
    /// <summary>
    /// An assigning authority service for remotely fetching AA
    /// </summary>
    public class RemoteAssigningAuthorityService : AmiRepositoryBaseService, IRepositoryService<AssigningAuthority>

    {
        public string ServiceName => throw new NotImplementedException();


        /// <summary>
        /// Get AA
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Get(Guid key)
        {
            return ((IRepositoryService<AssigningAuthority>)this).Get(key, Guid.Empty);
        }

        /// <summary>
        /// Get assigning authority
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Get(Guid key, Guid versionKey)
        {
            try
            {
                this.m_client.Client.Credentials = this.GetCredentials();
                var retVal = this.m_client.Client.Get<AssigningAuthority>($"AssigningAuthority/{key}");
                return retVal;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not retrieve authority", e);
            }
        }

        /// <summary>
        /// Finds the specified assigning authority
        /// </summary>
        IEnumerable<AssigningAuthority> IRepositoryService<AssigningAuthority>.Find(Expression<Func<AssigningAuthority, bool>> query)
        {
            int tr = 0;
            return ((IRepositoryService<AssigningAuthority>)this).Find(query, 0, null, out tr);
        }

        IEnumerable<AssigningAuthority> IRepositoryService<AssigningAuthority>.Find(Expression<Func<AssigningAuthority, bool>> query, int offset, int? count, out int totalResults, params ModelSort<AssigningAuthority>[] orderBy)
        {
            try
            {
                this.m_client.Client.Credentials = this.GetCredentials();
                return this.m_client.Query(query, offset, count, out totalResults, orderBy: orderBy).CollectionItem.OfType<AssigningAuthority>();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not query assigning authorities", e);
            }
        }

        /// <summary>
        /// Insert the specified data
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Insert(AssigningAuthority data)
        {
            try
            {
                this.m_client.Client.Credentials = this.GetCredentials();
                var retVal = this.m_client.CreateAssigningAuthority(data);
                return retVal;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create application", e);
            }
        }

        /// <summary>
        /// Update assigning authority
        /// </summary>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Save(AssigningAuthority data)
        {
            try
            {
                this.m_client.Client.Credentials = this.GetCredentials();
                var retVal = this.m_client.UpdateAssigningAuthority(data.Key.Value, data);
                return retVal;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not create authority", e);
            }
        }

        /// <summary>
        /// Obsolete the authority
        /// </summary>
        /// <returns></returns>
        AssigningAuthority IRepositoryService<AssigningAuthority>.Obsolete(Guid key)
        {
            try
            {
                this.m_client.Client.Credentials = this.GetCredentials();
                var retVal = this.m_client.DeleteAssigningAuthority(key);
                return retVal;
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Could not delete authority", e);
            }
        }
    }
}
