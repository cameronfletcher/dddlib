// <copyright file="IEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Exposes the public members of the event dispatcher.
    /// </summary>
    public interface IEventDispatcher
    {
        /// <summary>
        /// Dispatches an event to the specified target object.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="event">The event.</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "event", Justification = "It is an event.")]
        void Dispatch(object target, object @event);

        /// <summary>
        /// Determines whether this event dispatcher can dispatch the specified event type.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <returns>Returns <c>true</c> if the event can be dispatched; otherwise <c>false</c>.</returns>
        bool CanDispatch(Type eventType);
    }
}
