// <copyright file="Event.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.EventDispatcher.Sdk
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Gets or sets the event sequence number.
        /// </summary>
        /// <value>The event sequence number.</value>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the event payload.
        /// </summary>
        /// <value>The event payload.</value>
        public object Payload { get; set; }
    }
}
