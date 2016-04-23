// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.EventDispatcher.Sdk;

    internal sealed class MemoryEventStore : IEventStore, IDisposable
    {
        internal const string NullDispatcherId = "{246192AB-64D9-4A55-AE49-AF75EDF6FCB2}";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly MemoryMappedDictionary<int, MemoryMappedEvent> events = new MemoryMappedDictionary<int, MemoryMappedEvent>("Events");
        private readonly MemoryMappedDictionary<int, MemoryMappedBatch> batches = new MemoryMappedDictionary<int, MemoryMappedBatch>("Batches");
        private readonly MemoryMappedDictionary<string, long> dispatchedEvents = new MemoryMappedDictionary<string, long>("DispatchedEvents");

        private bool isDisposed;

        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            throw new NotImplementedException();
        }

        public Batch GetNextUndispatchedEventsBatch(string dispatcherId, int batchSize)
        {
            dispatcherId = dispatcherId ?? NullDispatcherId;

            // #1. Cancel all old batches (that have timed out)
            this.batches.Where(batch => batch.Value.DispatcherId == dispatcherId && batch.Value.Timestamp <= DateTime.UtcNow.AddSeconds(-30))
                .Select(batch => batch.Key)
                .ToList()
                .ForEach(batch => this.batches.Remove(batch));

            // #2. Identify the remaining incomplete batches
            var batchedSequenceNumber = 0L;
            var dispatcherBatches = this.batches.Where(batch => batch.Value.DispatcherId == dispatcherId);
            if (dispatcherBatches.Any())
            {
                batchedSequenceNumber = dispatcherBatches.Max(batch => batch.Value.SequenceNumber + batch.Value.Size);
            }

            var dispatchedSequenceNumber = 0L;
            this.dispatchedEvents.TryGetValue(dispatcherId, out dispatchedSequenceNumber);

            var sequenceNumber = Math.Max(dispatchedSequenceNumber, batchedSequenceNumber);

            // #3. Create a new batch (if necessary)
            var batchEvents = this.events.Where(@event => @event.Key >= sequenceNumber)
                .Take(batchSize)
                .Select(
                    @event => 
                    new Event
                    {
                        Id = @event.Key,
                        Payload = Serializer.Deserialize(@event.Value.Payload, Type.GetType(@event.Value.Type))
                    })
                .ToArray();

            if (!batchEvents.Any())
            {
                return null;
            }

            var batchId = this.batches.Any() ? this.batches.Max(batch => batch.Key) + 1 : 1;
            this.batches.Add(
                batchId,
                new MemoryMappedBatch
                {
                    DispatcherId = dispatcherId,
                    SequenceNumber = sequenceNumber,
                    Size = batchEvents.Length,
                    Timestamp = DateTime.UtcNow,
                });

            return new Batch
            {
                Id = batchId,
                Events = batchEvents,
            };
        }

        public void MarkEventAsDispatched(string dispatcherId, long sequenceNumber)
        {
            dispatcherId = dispatcherId ?? NullDispatcherId;

            this.dispatchedEvents[dispatcherId] = sequenceNumber;

            this.batches.Where(batch => batch.Value.DispatcherId == dispatcherId && batch.Value.SequenceNumber + batch.Value.Size - 1 == sequenceNumber)
                .Select(batch => batch.Key)
                .ToList()
                .ForEach(batch => this.batches.Remove(batch));
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            this.events.Dispose();
            this.batches.Dispose();
            this.dispatchedEvents.Dispose();
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

            public long Size { get; set; }

            public DateTime Timestamp { get; set; }
        }
    }
}
