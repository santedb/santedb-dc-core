using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Interop;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Linq;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// A generic implementation that calls the upstream for fetching data 
    /// </summary>
    public abstract class UpstreamRepositoryServiceBase<TModel, TCollection> : UpstreamServiceBase, 
        IRepositoryService<TModel>,
        IRepositoryService
        where TModel : IdentifiedData, new()
        where TCollection : IResourceCollection
    {
        private readonly ServiceEndpointType m_endpoint;
        private readonly IDataCachingService m_cacheService;
        private readonly ILocalizationService m_localeService;

        /// <summary>
        /// Gets the localization service
        /// </summary>
        protected ILocalizationService LocalizationService => this.m_localeService;

        /// <summary>
        /// Gets the data caching service
        /// </summary>
        protected IDataCachingService DataCachingService => this.m_cacheService;

        /// <summary>
        /// DI constructor
        /// </summary>
        protected UpstreamRepositoryServiceBase(
            ServiceEndpointType serviceEndpoint, 
            ILocalizationService localizationService, 
            IDataCachingService cacheService, 
            IRestClientFactory restClientFactory, 
            IUpstreamManagementService upstreamManagementService, 
            IUpstreamAvailabilityProvider upstreamAvailabilityProvider,
            IUpstreamIntegrationService upstreamIntegrationService) : base(restClientFactory, upstreamManagementService, upstreamAvailabilityProvider, upstreamIntegrationService)
        {
            this.m_endpoint = serviceEndpoint;
            this.m_cacheService = cacheService;
            this.m_localeService = localizationService;
        }

        /// <inheritdoc/>
        public string ServiceName => $"Upstream Repository for {typeof(TModel)}";

        /// <summary>
        /// Get resource name on the API
        /// </summary>
        private string GetResourceName() => typeof(TModel).GetSerializationName();

        /// <inheritdoc/>
        public virtual TModel Delete(Guid key)
        {
           
                try
                {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var retVal = client.Delete<TModel>($"{this.GetResourceName()}/{key}");
                    this.m_cacheService?.Remove(key);
                    return retVal;
                }
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException( this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_GEN_ERR), e);
                }

        }

        /// <inheritdoc/>
        public virtual IQueryResultSet<TModel> Find(Expression<Func<TModel, bool>> query)
        {
            return new UpstreamQueryResultSet<TModel, TCollection>(this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal), query);
        }

        /// <inheritdoc/>
        [Obsolete("Use Find(query)", true)]
        public virtual IEnumerable<TModel> Find(Expression<Func<TModel, bool>> query, int offset, int? count, out int totalResults, params ModelSort<TModel>[] orderBy)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual TModel Get(Guid key) => this.Get(key, Guid.Empty);

        /// <inheritdoc/>
        public virtual TModel Get(Guid key, Guid versionKey)
        {
            
                try
                {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var existing = this.m_cacheService?.GetCacheItem(key);

                    if (existing is TModel tm && versionKey == Guid.Empty) // The cache item matches the type
                    {
                        if (existing is IdentifiedData idata) // For entities and acts we want to ping the server
                        {
                            client.Requesting += (o, e) => e.AdditionalHeaders.Add("If-None-Match", $"W/{idata.Tag}");
                        }
                        existing = client.Get<TModel>($"{this.GetResourceName()}/{key}") ?? existing;
                    }
                    else if (versionKey == Guid.Empty)
                    {
                        existing = client.Get<TModel>($"{this.GetResourceName()}/{key}");
                        this.m_cacheService?.Add(existing as IdentifiedData);
                    }
                    else
                    {
                        existing = client.Get<TModel>($"{this.GetResourceName()}/{key}/_history/{versionKey}");
                    }

                    return (TModel)existing;
                }
                }
                catch (WebException) // Web based exception = return nothing
                {
                    return default(TModel);
                }
                catch (Exception e)
                {
                    throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_READ_ERR), e);
                }

        }


        /// <inheritdoc/>
        public virtual TModel Insert(TModel data)
        {

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is IHasTemplate iht)
            {
                this.UpstreamIntegrationService.HarmonizeTemplateId(iht);
            }
            else if (data is IResourceCollection irc)
            {
                irc.Item.OfType<IHasTemplate>().ToList().ForEach(r => this.UpstreamIntegrationService.HarmonizeTemplateId(r));
            }


            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    var retVal = client.Post<TModel, TModel>($"{this.GetResourceName()}", data);
                    this.m_cacheService.Add(retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }
        }

        /// <inheritdoc/>
        public virtual TModel Save(TModel data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data is IHasTemplate iht)
            {
                this.UpstreamIntegrationService.HarmonizeTemplateId(iht);
            }
            else if (data is IResourceCollection irc)
            {
                irc.Item.OfType<IHasTemplate>().ToList().ForEach(r => this.UpstreamIntegrationService.HarmonizeTemplateId(r));
            }


            try
            {
                using (var client = this.CreateRestClient(this.m_endpoint, AuthenticationContext.Current.Principal))
                {
                    if (!data.Key.HasValue)
                    {
                        data.Key = Guid.NewGuid();
                    }

                    // Create or Update
                    var retVal = client.Post<TModel, TModel>($"{this.GetResourceName()}/{data.Key}", data);
                    this.m_cacheService.Add(retVal);
                    return retVal;
                }
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(this.m_localeService.GetString(ErrorMessageStrings.UPSTREAM_WRITE_ERR), e);
            }

        }

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        IQueryResultSet IRepositoryService.Find(Expression query) => this.Find(query as Expression<Func<TModel, bool>>);

        /// <inheritdoc/>
        [Obsolete("Use Find(Expression)", true)]
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Insert(object data) => this.Insert((TModel)data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Save(object data) => this.Insert((TModel)data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Delete(Guid key) => this.Delete(key);
    }
}
