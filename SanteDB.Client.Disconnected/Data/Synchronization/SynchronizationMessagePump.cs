using SanteDB.Core.Model;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SanteDB.Client.Disconnected.Data.Synchronization
{
    internal class SynchronizationMessagePump
    {
        readonly ISynchronizationQueueManager _QueueManager;
        readonly IThreadPoolService _ThreadPool;

        readonly ManualResetEventSlim _Lock;
        readonly ConcurrentQueue<Action> _Work;

        internal const bool Continue = true;
        internal const bool Abort = false;
        internal const bool Handled = true;
        internal const bool Unhandled = false;

        public SynchronizationMessagePump(ISynchronizationQueueManager queueManager, IThreadPoolService threadPool)
        {
            _QueueManager = queueManager;
            _ThreadPool = threadPool;

            _Lock = new ManualResetEventSlim(false);
            _Work = new ConcurrentQueue<Action>();
        }

        internal SynchronizationQueue<SynchronizationDeadLetterQueueEntry> GetDeadLetterQueue() => _QueueManager.GetAll(SynchronizationPattern.DeadLetter)?.OfType<SynchronizationQueue<SynchronizationDeadLetterQueueEntry>>()?.FirstOrDefault();

        /// <summary>
        /// Generic message loop for a queue. This method is ignorant of any threading concerns.
        /// </summary>
        /// <param name="queue">The queue to run the pump on. The queue's <see cref="ISynchronizationQueue.Dequeue"/> method is called until <c>default</c> is returned.</param>
        /// <param name="callback">The callback to execute when data is received from the <paramref name="queue"/>. Return <c>true</c> to continue, <c>false</c> to break out of the loop.</param>
        /// <param name="error">Optional error handler when an exception is thrown in <paramref name="callback"/>. Return <c>true</c> to continue, <c>false</c> to throw the exception that was generated.</param>
        /// <param name="before">Optional pre-execution handler to invoke before the loop begins. Return <c>true</c> to proceed, <c>false</c> to return before beginning the loop.</param>
        /// <param name="after">Optional post-execution callback to cleanup any managed state before returning.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="queue"/> or <paramref name="callback"/> parameters are null.</exception>
        public void Run(ISynchronizationQueue queue, Func<ISynchronizationQueueEntry, bool> callback, Func<ISynchronizationQueueEntry, Exception, bool> error = null, Func<bool> before = null, Action after = null)
        {
            if (null == queue)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            if (null == callback)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            bool cont = before?.Invoke() ?? Continue;

            if (cont == Abort)
            {
                return;
            }

            var entry = queue.Dequeue();
            while (null != entry)
            {
                try
                {
                    cont = callback(entry);
                }
                catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
                {
                    //TODO: Log exception
                    if (!(error?.Invoke(entry, ex) ?? Unhandled))
                    {
                        throw;
                    }
                }

                if (cont == Abort)
                {
                    break;
                }

                entry = queue.Dequeue();
            }

            after?.Invoke();
        }
        /// <summary>
        /// Generic message loop for a queue. This method is ignorant of any threading concerns.
        /// </summary>
        /// <param name="queue">The queue to run the pump on. The queue's <see cref="ISynchronizationQueue.Dequeue"/> method is called until <c>default</c> is returned.</param>
        /// <param name="callback">The callback to execute when data is received from the <paramref name="queue"/>. Return <c>true</c> to continue, <c>false</c> to break out of the loop.</param>
        /// <param name="callbackPolicy">A policy that defines how exceptions are handled when they occur during the callback.</param>
        /// <param name="before">Optional pre-execution handler to invoke before the loop begins. Return <c>true</c> to proceed, <c>false</c> to return before beginning the loop.</param>
        /// <param name="after">Optional post-execution callback to cleanup any managed state before returning.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="queue"/> or <paramref name="callback"/> parameters are null.</exception>
        public void Run(ISynchronizationQueue queue, Func<ISynchronizationQueueEntry, bool> callback, Polly.ISyncPolicy<bool> callbackPolicy, Func<bool> before, Action after = null)
        {
            if (null == queue)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            if (null == callback)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            bool cont = before?.Invoke() ?? Continue;

            if (cont == Abort)
            {
                return;
            }

            var entry = queue.Dequeue();
            while (null != entry)
            {
                cont = callbackPolicy.Execute(() => callback(entry));

                if (cont == Abort)
                {
                    break;
                }

                entry = queue.Dequeue();
            }

            after?.Invoke();
        }
        /// <summary>
        /// Generic message loop for a queue. This method is ignorant of any threading concerns.
        /// </summary>
        /// <param name="queue">The queue to run the pump on. The queue's <see cref="ISynchronizationQueue.Dequeue"/> method is called until <c>default</c> is returned.</param>
        /// <param name="callback">The callback to execute when data is received from the <paramref name="queue"/>. Return <c>true</c> to continue, <c>false</c> to break out of the loop.</param>
        /// <param name="callbackPolicy">A policy that defines how exceptions are handled when they occur during the callback.</param>
        /// <param name="loopPolicy">A policy that defines how exceptions are handled outside of the loop.</param>
        /// <param name="before">Optional pre-execution handler to invoke before the loop begins. Return <c>true</c> to proceed, <c>false</c> to return before beginning the loop.</param>
        /// <param name="after">Optional post-execution callback to cleanup any managed state before returning.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="queue"/> or <paramref name="callback"/> parameters are null.</exception>
        public void Run(ISynchronizationQueue queue, Func<ISynchronizationQueueEntry, bool> callback, Polly.ISyncPolicy<bool> callbackPolicy, Polly.ISyncPolicy loopPolicy, Func<bool> before, Action after = null)
        {
            if (null == queue)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            if (null == callback)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            loopPolicy.Execute(() =>
            {
                bool cont = before?.Invoke() ?? Continue;

                if (cont == Abort)
                {
                    return;
                }

                var entry = queue.Dequeue();
                while (null != entry)
                {
                    cont = callbackPolicy.Execute(() => callback(entry));

                    if (cont == Abort)
                    {
                        break;
                    }

                    entry = queue.Dequeue();
                }

                after?.Invoke();
            });
        }
        /// <summary>
        /// Generic message loop for a queue. This method is ignorant of any threading concerns.
        /// </summary>
        /// <param name="queue">The queue to run the pump on. The queue's <see cref="ISynchronizationQueue.Dequeue"/> method is called until <c>default</c> is returned.</param>
        /// <param name="callback">The callback to execute when data is received from the <paramref name="queue"/>. Return <c>true</c> to continue, <c>false</c> to break out of the loop.</param>
        /// <param name="callbackPolicy">A policy that defines how exceptions are handled when they occur during the callback.</param>
        /// <param name="dequeuePolicy">A policy that defines how exceptions are handled when they occur during the queue's dequeue operation.</param>
        /// <param name="loopPolicy">A policy that defines how exceptions are handled outside of the loop.</param>
        /// <param name="before">Optional pre-execution handler to invoke before the loop begins. Return <c>true</c> to proceed, <c>false</c> to return before beginning the loop.</param>
        /// <param name="after">Optional post-execution callback to cleanup any managed state before returning.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="queue"/> or <paramref name="callback"/> parameters are null.</exception>
        public void Run(ISynchronizationQueue queue, Func<ISynchronizationQueueEntry, bool> callback, Polly.ISyncPolicy<bool> callbackPolicy, Polly.ISyncPolicy<ISynchronizationQueueEntry> dequeuePolicy, Polly.ISyncPolicy loopPolicy, Func<bool> before, Action after = null)
        {
            if (null == queue)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            if (null == callback)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            loopPolicy.Execute(() =>
            {
                bool cont = before?.Invoke() ?? Continue;

                if (cont == Abort)
                {
                    return;
                }

                var entry = dequeuePolicy.Execute(() => queue.Dequeue());
                while (null != entry)
                {
                    cont = callbackPolicy.Execute(() => callback(entry));

                    if (cont == Abort)
                    {
                        break;
                    }

                    entry = dequeuePolicy.Execute(() => queue.Dequeue());
                }

                after?.Invoke();
            });
        }
    }
}
