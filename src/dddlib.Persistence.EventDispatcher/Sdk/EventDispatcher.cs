// <copyright file="EventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an event dispatcher.
    /// </summary>
    public class EventDispatcher : IDisposable
    {
        private readonly object @lock = new object();
        private readonly Timer timer;

        private readonly IEventDispatcher eventDispatcher;
        private readonly IEventStore eventStore;
        private readonly INotificationService notificationService;
        private readonly string dispatcherId;
        private readonly int batchSize;

        private Batch bufferedBatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventDispatcher" /> class.
        /// </summary>
        /// <param name="eventDispatcher">The event dispatcher.</param>
        /// <param name="eventStore">The event store.</param>
        /// <param name="notificationService">The notification service.</param>
        /// <param name="dispatcherId">The dispatcher identifier.</param>
        /// <param name="batchSize">Size of the batch.</param>
        public EventDispatcher(
            IEventDispatcher eventDispatcher,
            IEventStore eventStore, 
            INotificationService notificationService, 
            string dispatcherId,
            int batchSize)
        {
            Guard.Against.Null(() => eventDispatcher);
            Guard.Against.Null(() => eventStore);
            Guard.Against.Null(() => notificationService);

            if (dispatcherId != null && dispatcherId.Length > 10)
            {
                throw new ArgumentException("Dispatcher identity cannot be more than 10 character long.", Guard.Expression.Parse(() => dispatcherId));
            }

            this.eventDispatcher = eventDispatcher;
            this.eventStore = eventStore;
            this.notificationService = notificationService;
            this.dispatcherId = dispatcherId;
            this.batchSize = batchSize;

            this.timer = new Timer(this.OnTimeout, null, Timeout.Infinite, Timeout.Infinite);

            this.Start();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="EventDispatcher"/> class.
        /// </summary>
        ~EventDispatcher()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start()
        {
            this.notificationService.OnBatchPrepared += this.OnBatchPrepared;
            this.notificationService.OnEventCommitted += this.OnEventComitted;

            this.BufferNextBatch();
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop()
        {
            this.notificationService.OnEventCommitted -= this.OnEventComitted;
            this.notificationService.OnBatchPrepared -= this.OnBatchPrepared;

            if (this.timer != null)
            {
                this.timer.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">Set to <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                this.Stop();
            }
        }

        private void OnEventComitted(object sender, EventCommittedEventArgs e)
        {
            this.BufferNextBatch();
        }

        private void OnBatchPrepared(object sender, BatchPreparedEventArgs e)
        {
            this.ProcessBatch(e.BatchId);
        }

        private void OnTimeout(object state)
        {
            this.RemoveFromBuffer();
        }

        private void BufferNextBatch()
        {
            lock (@lock)
            {
                if (this.bufferedBatch != null)
                {
                    return;
                }

                var batch = this.eventStore.GetNextUndispatchedEventsBatch(this.dispatcherId, this.batchSize);
                if (batch == null)
                {
                    return;
                }

                this.bufferedBatch = batch;

                this.timer.Change(30 * 1000, Timeout.Infinite);
            }
        }

        private bool TryRemoveFromBuffer(long batchId, out Batch batch)
        {
            lock (@lock)
            {
                if (this.bufferedBatch == null || this.bufferedBatch.Id != batchId)
                {
                    batch = null;
                    return false;
                }

                batch = this.bufferedBatch;

                this.RemoveFromBuffer();
            }

            return true;
        }

        private void RemoveFromBuffer()
        {
            this.bufferedBatch = null;

            this.timer.Change(Timeout.Infinite, Timeout.Infinite);

            // TODO (Cameron): This should be an async operation.
            Task.Factory.StartNew(() => this.BufferNextBatch());
        }

        // invoked while there is data in batches
        private void ProcessBatch(long batchId)
        {
            var batch = default(Batch);
            if (!this.TryRemoveFromBuffer(batchId, out batch))
            {
                ////lock (@lock)
                ////{
                ////    if (this.bufferedBatch != null && this.bufferedBatch.Id < batchId)
                ////    {
                ////        // NOTE (Cameron): This batch is stale.
                ////        this.RemoveFromBuffer();
                ////    }
                ////}

                return;
            }

            foreach (var @event in batch.Events)
            {
                // TODO: Deserialize?
                try
                {
                    this.eventDispatcher.Dispatch(@event.SequenceNumber, @event.Payload);
                }
                catch (Exception)
                {
                    // NOTE (Cameron): Something went wrong in the dispatcher implementation.
                    // TODO (Cameron): Log?
                    // TODO (Cameron): Consider retry?
                    break;
                }

                // NOTE (Cameron): If this fails we will likely double dispatch.
                // TODO (Cameron): Retry? Fall over?
                this.eventStore.MarkEventAsDispatched(this.dispatcherId, @event.SequenceNumber);
            }
        }
    }
}
