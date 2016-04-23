// <copyright file="MemoryEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Script.Serialization;
    using dddlib.Persistence.Sdk;
    using dddlib.Sdk;

    /// <summary>
    /// Represents the memory event store.
    /// </summary>
    public sealed class MemoryEventStore : IEventStore, IDisposable
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly MemoryMappedDictionary<int, MemoryMappedEvent> events = new MemoryMappedDictionary<int, MemoryMappedEvent>("Events");
        private readonly Dictionary<Guid, List<Event>> streams = new Dictionary<Guid, List<Event>>();

        private int currentSequenceNumber;
        private bool isDisposed;

        /// <summary>
        /// Gets the events for a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="streamRevision">The stream revision to get the events from.</param>
        /// <param name="state">The state of the steam.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetStream(Guid streamId, int streamRevision, out string state)
        {
            Guard.Against.Negative(() => streamRevision);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();

            var stream = default(List<Event>);
            if (!this.streams.TryGetValue(streamId, out stream))
            {
                state = null;
                return new object[0];
            }

            state = stream.Last().State;

            return stream.Skip(streamRevision).Select(@event => @event.Payload).ToList();
        }

        /// <summary>
        /// Commits the events to a stream.
        /// </summary>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="events">The events to commit.</param>
        /// <param name="correlationId">The correlation identifier.</param>
        /// <param name="preCommitState">The pre-commit state of the stream.</param>
        /// <param name="postCommitState">The post-commit state of stream.</param>
        public void CommitStream(Guid streamId, IEnumerable<object> events, Guid correlationId, string preCommitState, out string postCommitState)
        {
            Guard.Against.Null(() => events);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();

            postCommitState = null;

            var stream = default(List<Event>);
            if (this.streams.TryGetValue(streamId, out stream))
            {
                if (stream.Last().State != preCommitState)
                {
                    throw preCommitState == null
                        ? new ConcurrencyException("Aggregate root already exists.")
                        : new ConcurrencyException();
                }

                // NOTE (Cameron): Only if there are no events to commit.
                postCommitState = stream.Last().State;
            }

            if (!events.Any())
            {
                return;
            }

            foreach (var @event in events)
            {
                // NOTE (Cameron): Try-catch block retry required for concurrency issues.
                ////try
                ////{
                    this.events.Add(
                        this.events.Count + 1,
                        new MemoryMappedEvent
                        {
                            StreamId = streamId,
                            Type = @event.GetType().GetSerializedName(),
                            Payload = Serializer.Serialize(@event),
                            State = postCommitState = Guid.NewGuid().ToString("N").Substring(0, 8),
                        });
                ////}
                ////catch (ArgumentException)
                ////{
                ////    // An item with the same key has already been added.
                ////}
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            this.events.Dispose();
        }

        private void Synchronize()
        {
            var sequenceNumbers = this.events.Keys.Skip(this.currentSequenceNumber);
            foreach (var sequenceNumber in sequenceNumbers)
            {
                var memoryMappedEvent = this.events[sequenceNumber];

                var stream = default(List<Event>);
                if (!this.streams.TryGetValue(memoryMappedEvent.StreamId, out stream))
                {
                    this.streams.Add(memoryMappedEvent.StreamId, stream = new List<Event>());
                }

                stream.Add(
                    new Event
                    {
                        SequenceNumber = sequenceNumber,
                        Payload = Serializer.Deserialize(memoryMappedEvent.Payload, Type.GetType(memoryMappedEvent.Type)),
                        State = memoryMappedEvent.State,
                    });
            }

            if (sequenceNumbers.Any())
            {
                this.currentSequenceNumber = sequenceNumbers.Max();
            }
        }

        private class Event
        {
            public long SequenceNumber { get; set; }

            public object Payload { get; set; }

            public string State { get; set; }
        }

        private class MemoryMappedEvent
        {
            public Guid StreamId { get; set; }

            public string Type { get; set; }

            public string Payload { get; set; }

            public string State { get; set; }
        }
    }
}
