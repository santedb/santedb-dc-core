using SanteDB.Client.Exceptions;
using SanteDB.Core.Http;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Query;
using SanteDB.Rest.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Client.Upstream.Repositories
{
    /// <summary>
    /// Represents a <see cref="IQueryResultSet{TData}"/> which interacts with a REST service 
    /// </summary>
    internal class UpstreamQueryResultSet<TModel, TCollectionType> : IQueryResultSet<TModel>, IDisposable
        where TModel : IdentifiedData, new()
        where TCollectionType : IResourceCollection
    {
        private readonly IRestClient m_restClient;
        private readonly NameValueCollection m_queryFilter;

        /// <summary>
        /// Create a new upstream query result set with <paramref name="restClient"/> and <paramref name="query"/>
        /// </summary>
        /// <param name="restClient">The rest client to use to fetch results</param>
        /// <param name="query">The query to use to filter</param>
        public UpstreamQueryResultSet(IRestClient restClient, Expression<Func<TModel, bool>> query) : this(restClient, QueryExpressionBuilder.BuildQuery(query)) { }

        /// <summary>
        /// Create a new upstream query result
        /// </summary>
        private UpstreamQueryResultSet(IRestClient restClient, NameValueCollection query)
        {
            this.m_restClient = restClient;
            this.m_queryFilter = query;
        }

        /// <inheritdoc/>
        public bool Any() => this.Count() > 0;

        /// <inheritdoc/>
        public IQueryResultSet<TModel> AsStateful(Guid stateId)
        {
            var queryFilter = new NameValueCollection(this.m_queryFilter);
            queryFilter[QueryControlParameterNames.HttpQueryStateParameterName] = stateId.ToString();
            return new UpstreamQueryResultSet<TModel, TCollectionType>(this.m_restClient, queryFilter);
        }

        /// <inheritdoc/>
        public int Count()
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            parameters[QueryControlParameterNames.HttpCountParameterName] = "0";
            parameters[QueryControlParameterNames.HttpIncludeTotalParameterName] = "true";
            try
            {
                return this.m_restClient.Get<TCollectionType>(typeof(TModel).GetSerializationName(), parameters).TotalResults.GetValueOrDefault();
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(ErrorMessages.GENERAL_QUERY_ERROR,  e);
            }
        }

        /// <summary>
        /// Dispose this object
        /// </summary>
        public void Dispose()
        {
            this.m_restClient.Dispose();
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Distinct() => this; // Does nothing

        /// <inheritdoc/>
        public TModel First()
        {
            var retVal = this.FirstOrDefault();
            if (retVal == null)
            {
                throw new InvalidOperationException(ErrorMessages.SEQUENCE_NO_ELEMENTS);
            }
            return retVal;
        }

        /// <inheritdoc/>
        public TModel FirstOrDefault()
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            parameters[QueryControlParameterNames.HttpCountParameterName] = "1";
            parameters[QueryControlParameterNames.HttpIncludeTotalParameterName] = "false";
            try
            {
                return (TModel)this.m_restClient.Get<TCollectionType>(typeof(TModel).GetSerializationName(), parameters).Item.FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }

        }

        /// <inheritdoc/>
        public IEnumerator<TModel> GetEnumerator()
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            _ = Int32.TryParse(parameters[QueryControlParameterNames.HttpOffsetParameterName], out var offset);
            var hasExplicitLimit = !String.IsNullOrEmpty(parameters[QueryControlParameterNames.HttpCountParameterName]);
            parameters[QueryControlParameterNames.HttpIncludeTotalParameterName] = "false";

            bool fetchNextPage = true;
            while (fetchNextPage)
            {
                var results = this.m_restClient.Get<TCollectionType>(typeof(TModel).GetSerializationName(), parameters).Item;
                foreach (var r in results.OfType<TModel>())
                {
                    fetchNextPage = true;
                    yield return r;
                }
                fetchNextPage = results.Any();
                offset += results.Count();
                parameters[QueryControlParameterNames.HttpOffsetParameterName] = offset.ToString(); // fetch next page
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Intersect(IQueryResultSet<TModel> other)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Intersect(Expression<Func<TModel, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet Intersect(IQueryResultSet other)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IEnumerable<TType> OfType<TType>()
        {
            foreach (var itm in this)
            {
                if (this is TType t)
                {
                    yield return t;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TReturn> Select<TReturn>(Expression<Func<TModel, TReturn>> selector) => this.Select<TReturn>((Expression)selector);

        /// <inheritdoc/>
        public IEnumerable<TReturn> Select<TReturn>(Expression selector)
        {
            Func<TModel, TReturn> compiledSelector = selector as Func<TModel, TReturn>;
            switch (selector)
            {
                case Expression<Func<TModel, TReturn>> se:
                    compiledSelector = se.Compile();
                    break;
                case Func<TModel, TReturn> fse:
                    compiledSelector = fse;
                    break;
                default:
                    throw new InvalidOperationException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, selector.GetType(), typeof(Func<TModel, TReturn>)));
            }

            foreach (var itm in this)
            {
                yield return compiledSelector(itm);
            }
        }

        /// <inheritdoc/>
        public TModel Single()
        {
            var retVal = this.SingleOrDefault();
            if (retVal == default(TModel))
            {
                throw new InvalidOperationException(ErrorMessages.SEQUENCE_MORE_THAN_ONE);
            }
            return retVal;
        }

        /// <inheritdoc/>
        public TModel SingleOrDefault()
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            parameters[QueryControlParameterNames.HttpCountParameterName] = "2";
            parameters[QueryControlParameterNames.HttpIncludeTotalParameterName] = "false";
            try
            {
                var results = this.m_restClient.Get<TCollectionType>(typeof(TModel).GetSerializationName(), parameters);
                return results.Item.OfType<TModel>().SingleOrDefault();
            }
            catch (Exception e)
            {
                throw new UpstreamIntegrationException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Skip(int count)
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            _ = Int32.TryParse(parameters[QueryControlParameterNames.HttpOffsetParameterName], out var offset);
            parameters[QueryControlParameterNames.HttpOffsetParameterName] = (offset + count).ToString();
            return new UpstreamQueryResultSet<TModel, TCollectionType>(this.m_restClient, parameters);
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Take(int count)
        {
            var parameters = new NameValueCollection(this.m_queryFilter);
            parameters[QueryControlParameterNames.HttpCountParameterName] = count.ToString();
            return new UpstreamQueryResultSet<TModel, TCollectionType>(this.m_restClient, parameters);
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Union(IQueryResultSet<TModel> other)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Union(Expression<Func<TModel, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet Union(IQueryResultSet other)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet<TModel> Where(Expression<Func<TModel, bool>> query)
        {
            var queryCollection = QueryExpressionBuilder.BuildQuery(query);
            queryCollection.Add(this.m_queryFilter);
            return new UpstreamQueryResultSet<TModel, TCollectionType>(this.m_restClient, queryCollection);
        }

        /// <inheritdoc/>
        public IQueryResultSet Where(Expression query)
        {
            if(query is Expression<Func<TModel, bool>> other)
            {
                return this.Where(other);
            }
            else
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, query.GetType(), typeof(Expression<Func<TModel, bool>>)));
            }
        }

        /// <inheritdoc/>
        IQueryResultSet IQueryResultSet.AsStateful(Guid stateId) => this.AsStateful(stateId);

        /// <inheritdoc/>
        object IQueryResultSet.First() => this.First();

        /// <inheritdoc/>
        object IQueryResultSet.FirstOrDefault() => this.FirstOrDefault();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <inheritdoc/>
        object IQueryResultSet.Single() => this.Single();

        /// <inheritdoc/>
        object IQueryResultSet.SingleOrDefault() => this.SingleOrDefault();

        /// <inheritdoc/>
        IQueryResultSet IQueryResultSet.Skip(int count) => this.Skip(count);

        /// <inheritdoc/>
        IQueryResultSet IQueryResultSet.Take(int count) => this.Take(count);
    }
}