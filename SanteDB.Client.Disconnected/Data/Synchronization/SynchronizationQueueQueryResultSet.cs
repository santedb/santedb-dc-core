using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    /// <summary>
    /// Implementation of the <see cref="IQueryResultSet"/> which reads metadata from the queue data file to optimize the
    /// query of data from the queue
    /// </summary>
    internal class SynchronizationQueueQueryResultSet<TEntry> : IQueryResultSet<ISynchronizationQueueEntry>
        where TEntry : ISynchronizationQueueEntry, new()
    {
        private readonly IEnumerable<ISynchronizationQueueEntry> m_queueSource;
        private readonly SynchronizationQueue<TEntry> m_synchronizationQueue;

        public SynchronizationQueueQueryResultSet(SynchronizationQueue<TEntry> synchronizationQueue)
        {
            this.m_synchronizationQueue = synchronizationQueue;
        }

        private SynchronizationQueueQueryResultSet(SynchronizationQueueQueryResultSet<TEntry> parentResultSet, IEnumerable<int> queueSourceIds)
        {
            this.m_synchronizationQueue = parentResultSet.m_synchronizationQueue;
            // If the parent result set has been expanded already via filter - just apply it
            this.m_queueSource = queueSourceIds.Select(o => this.m_synchronizationQueue.Get(o)).OfType<ISynchronizationQueueEntry>();
        }

        private SynchronizationQueueQueryResultSet(SynchronizationQueueQueryResultSet<TEntry> parentResultSet, IEnumerable<ISynchronizationQueueEntry> queueSource)
        {
            this.m_synchronizationQueue = parentResultSet.m_synchronizationQueue;
            this.m_queueSource = queueSource;
        }

        /// <summary>
        /// Expand the queue either using the provided list of previously filtered methods or using the queue as a source
        /// </summary>
        private IEnumerable<ISynchronizationQueueEntry> ExpandResults() => this.m_queueSource ?? this.m_synchronizationQueue.GetQueueEntryIdentifiers().Select(o => this.m_synchronizationQueue.Get(o)).OfType<ISynchronizationQueueEntry>();

        /// <inheritdoc/>
        public Type ElementType => typeof(TEntry);

        /// <inheritdoc/>
        public bool Any() => this.m_queueSource?.Any() ?? this.m_synchronizationQueue.GetQueueEntryIdentifiers().Any();

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> AsStateful(Guid stateId) => this; // TODO: Allow the "freezing" via get all identifiers at this call 

        /// <inheritdoc/>
        public int Count() => this.m_queueSource?.Count() ?? this.m_synchronizationQueue.GetQueueEntryIdentifiers().Count();

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Distinct() => new SynchronizationQueueQueryResultSet<TEntry>(this, this.ExpandResults().Distinct());

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Except(Expression<Func<ISynchronizationQueueEntry, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry First() => this.ExpandResults().First();

        /// <inheritdoc/>
        public ISynchronizationQueueEntry FirstOrDefault() => this.ExpandResults().FirstOrDefault();

        /// <inheritdoc/>
        public IEnumerator<ISynchronizationQueueEntry> GetEnumerator() => this.ExpandResults().GetEnumerator();

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Intersect(IQueryResultSet<ISynchronizationQueueEntry> other) => new SynchronizationQueueQueryResultSet<TEntry>(this, this.ExpandResults().Intersect(other));

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Intersect(Expression<Func<ISynchronizationQueueEntry, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet Intersect(IQueryResultSet other) => this.Intersect((IQueryResultSet<TEntry>)other);

        /// <inheritdoc/>
        public IEnumerable<TType> OfType<TType>() => this.ExpandResults().OfType<TType>();

        /// <inheritdoc/>
        public IEnumerable<TReturn> Select<TReturn>(Expression<Func<ISynchronizationQueueEntry, TReturn>> selector) => this.ExpandResults().Select(selector.Compile());

        /// <inheritdoc/>
        public IEnumerable<TReturn> Select<TReturn>(Expression selector)
        {
            if (selector is Expression<Func<ISynchronizationQueueEntry, TReturn>> strong)
            {
                return this.Select(strong);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Expression<Func<TEntry, bool>>), selector.GetType()));
            }
        }

        /// <inheritdoc/>
        public ISynchronizationQueueEntry Single() => this.ExpandResults().Single();

        /// <inheritdoc/>
        public ISynchronizationQueueEntry SingleOrDefault() => this.ExpandResults().SingleOrDefault();

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Skip(int count)
        {
            // Has this been expanded?
            if (this.m_queueSource != null)
            {
                return new SynchronizationQueueQueryResultSet<TEntry>(this, this.m_queueSource.Skip(count));
            }
            else
            {
                return new SynchronizationQueueQueryResultSet<TEntry>(this, this.m_synchronizationQueue.GetQueueEntryIdentifiers().Skip(count));
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Take(int count)
        {
            // Has this been expanded?
            if (this.m_queueSource != null)
            {
                return new SynchronizationQueueQueryResultSet<TEntry>(this, this.m_queueSource.Take(count));
            }
            else
            {
                return new SynchronizationQueueQueryResultSet<TEntry>(this, this.m_synchronizationQueue.GetQueueEntryIdentifiers().Take(count));
            }
        }

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Union(IQueryResultSet<ISynchronizationQueueEntry> other) => new SynchronizationQueueQueryResultSet<TEntry>(this, this.ExpandResults().Union(other));

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Union(Expression<Func<ISynchronizationQueueEntry, bool>> query)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public IQueryResultSet Union(IQueryResultSet other) => this.Union((IQueryResultSet<ISynchronizationQueueEntry>)other);

        /// <inheritdoc/>
        public IQueryResultSet<ISynchronizationQueueEntry> Where(Expression<Func<ISynchronizationQueueEntry, bool>> query) => new SynchronizationQueueQueryResultSet<TEntry>(this, this.ExpandResults().Where(query.Compile()));

        /// <inheritdoc/>
        public IQueryResultSet Where(Expression query)
        {
            if(query is Expression<Func<TEntry, bool>> strongExpression)
            {
                return this.Where(strongExpression);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Expression<Func<TEntry, bool>>), query.GetType()));
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
