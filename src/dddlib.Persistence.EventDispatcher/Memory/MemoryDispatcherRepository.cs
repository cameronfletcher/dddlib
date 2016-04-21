// <copyright file="MemoryDispatcherRepository.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using dddlib.Persistence.EventDispatcher.Sdk;

    internal sealed class MemoryDispatcherRepository : IEventStore, IDisposable
    {
        private static readonly string NullDispatcherId = "{246192AB-64D9-4A55-AE49-AF75EDF6FCB2}";

        private readonly Dictionary<string, long> dispatchedEvents = new Dictionary<string, long>();
        private readonly Dictionary<string, List<MemoryMappedBatch>> batches = new Dictionary<string, List<MemoryMappedBatch>>();

        private readonly IEventStore eventStore;

        public MemoryDispatcherRepository(IEventStore eventStore)
        {
            Guard.Against.Null(() => eventStore);

            this.eventStore = eventStore;
        }

        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            throw new NotImplementedException();
        }

        public Batch GetNextUndispatchedEventsBatch(string dispatcherId, int batchSize)
        {
            dispatcherId = dispatcherId ?? NullDispatcherId;

            var dispatchedSequenceNumber = 0L;
            this.dispatchedEvents.TryGetValue(dispatcherId, out dispatchedSequenceNumber);

            var incompleteBatches = default(List<MemoryMappedBatch>);
            if (!this.batches.TryGetValue(dispatcherId, out incompleteBatches))
            {
                this.batches.Add(dispatcherId, incompleteBatches = new List<MemoryMappedBatch>());
            }

            // cancel old batches
            incompleteBatches.Where(batch => batch.Timestamp <= DateTime.UtcNow.AddSeconds(-30))
                .ToList()
                .ForEach(batch => incompleteBatches.Remove(batch));

            // take max sequence number from an existing incomplete batch
            var batchedSequenceNumber = incompleteBatches.Any()
                ? incompleteBatches.Max(batch => batch.SequenceNumber + batch.Size - 1)
                : 0L;

            var sequenceNumber = Math.Max(dispatchedSequenceNumber, batchedSequenceNumber);

            // TODO (Cameron): Might need to introduce a 'to' argument.
            var @events = this.eventStore.GetEventsFrom(sequenceNumber)
                .Take(50)
                .Select(payload => new Event { Id = ++sequenceNumber, Payload = payload })
                .ToArray();

            if (!events.Any())
            {
                return null;
            }

            return new Batch
            {
                Id = 1,
                Events = events,
            };
        }

        public void MarkEventAsDispatched(string dispatcherId, long sequenceNumber)
        {
            dispatcherId = dispatcherId ?? NullDispatcherId;

            this.dispatchedEvents[dispatcherId] = sequenceNumber;

            var incompleteBatches = default(List<MemoryMappedBatch>);
            if (!this.batches.TryGetValue(dispatcherId, out incompleteBatches))
            {
                this.batches.Add(dispatcherId, incompleteBatches = new List<MemoryMappedBatch>());
            }

            // cancel complete batches
            incompleteBatches.Where(batch => batch.SequenceNumber + batch.Size - 1 == sequenceNumber)
                .ToList()
                .ForEach(batch => incompleteBatches.Remove(batch));
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private class MemoryMappedBatch
        {
            public long Id { get; set; }

            public long SequenceNumber { get; set; }

            public long Size { get; set; }

            public DateTime Timestamp { get; set; }

            public bool Complete { get; set; }
        }
    }
}
