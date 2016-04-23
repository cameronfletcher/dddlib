// <copyright file="MemoryNotificationService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using System.Linq;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.EventDispatcher.Sdk;

    internal sealed class MemoryNotificationService : INotificationService, IDisposable
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly MemoryMappedDictionary<int, MemoryMappedEvent> events = new MemoryMappedDictionary<int, MemoryMappedEvent>("Events", true);
        private readonly MemoryMappedDictionary<int, MemoryMappedBatch> batches = new MemoryMappedDictionary<int, MemoryMappedBatch>("Batches", true);

        private readonly string dispatcherId;

        private int currentSequenceNumber;
        private int currentBatchId;
        private bool isDisposed;

        public MemoryNotificationService()
            : this(MemoryEventStore.NullDispatcherId)
        {
        }

        public MemoryNotificationService(string dispatcherId)
        {
            Guard.Against.NullOrEmpty(() => dispatcherId);

            this.dispatcherId = dispatcherId;

            this.events.OnChange += this.MonitorUndispatchedEvents;
            this.batches.OnChange += this.MonitorUndispatchedBatches;
        }

        public event EventHandler<BatchPreparedEventArgs> OnBatchPrepared;

        public event EventHandler<EventCommittedEventArgs> OnEventCommitted;

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            this.events.OnChange -= this.MonitorUndispatchedEvents;
            this.batches.OnChange -= this.MonitorUndispatchedBatches;

            this.events.Dispose();
            this.batches.Dispose();
        }

        private void MonitorUndispatchedEvents(object sender, EventArgs e)
        {
            var sequenceNumber = this.events.Count;
            if (this.currentSequenceNumber == sequenceNumber)
            {
                return;
            }

            this.currentSequenceNumber = sequenceNumber;

            if (this.OnEventCommitted != null)
            {
                this.OnEventCommitted.Invoke(this, new EventCommittedEventArgs(this.currentSequenceNumber));
            }
        }

        private void MonitorUndispatchedBatches(object sender, EventArgs e)
        {
            var nextBatch = 0L;
            var batchIds = this.batches.Keys.Skip(this.currentBatchId);
            foreach (var batchId in batchIds)
            {
                var batch = this.batches[batchId];
                if (batch.DispatcherId == this.dispatcherId)
                {
                    nextBatch = batchId;
                }
            }

            if (this.OnBatchPrepared != null)
            {
                this.OnBatchPrepared.Invoke(this, new BatchPreparedEventArgs(nextBatch));
            }

            if (batchIds.Any())
            {
                this.currentBatchId = batchIds.Max();
            }
        }

        private class MemoryMappedEvent
        {
            public Guid StreamId { get; set; }

            public string Type { get; set; }

            public string Payload { get; set; }

            public string State { get; set; }
        }

        private class MemoryMappedBatch
        {
            public string DispatcherId { get; set; }

            public long SequenceNumber { get; set; }

            public string State { get; set; }

            public string Timestamp { get; set; }
        }
    }
}
