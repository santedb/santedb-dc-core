using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Client.Tickles
{
    /// <summary>
    /// An implementation of the <see cref="ITickleService"/> which uses memory structures 
    /// to temporarily hold tickles for the user interface
    /// </summary>
    public class InMemoryTickleService : ITickleService
    {
        // Tickles
        private readonly ConcurrentDictionary<Guid, Tickle> m_tickles = new ConcurrentDictionary<Guid, Tickle>();

        /// <inheritdoc/>
        public string ServiceName => "Default Tickle Service";

        /// <inheritdoc/>
        public void DismissTickle(Guid tickleId) => this.m_tickles.TryRemove(tickleId, out _);

        /// <inheritdoc/>
        public IEnumerable<Tickle> GetTickles(Expression<Func<Tickle, bool>> filter) => this.m_tickles.Values.Where(filter.Compile());

        /// <inheritdoc/>
        public void SendTickle(Tickle tickle)
        {
            if(tickle.Id == Guid.Empty)
            {
                tickle.Id = Guid.NewGuid();
            }
            this.m_tickles.TryAdd(tickle.Id, tickle);
        }
    }
}
