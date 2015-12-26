// <copyright file="AggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib
{
    /*  TODO (Cameron): 
        Enable ES without persistence mode.
        Fix exception types (as before).
        Remove IAggregateRoot.
        More will be required once persistence layer is fleshed out.
        Consider enforcing logic around change of state field.  */

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using dddlib.Runtime;
    using dddlib.Sdk;
    using dddlib.Sdk.Configuration.Model;

    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public abstract partial class AggregateRoot : Entity
    {
        // PERF (Cameron): Introduced to reduce allocations of the function delegates for type information.
        private static readonly Func<AggregateRoot, TypeInformation> GetTypeInformation =
            @this => new TypeInformation(Application.Current.GetAggregateRootType(@this.GetType()));

        private readonly List<object> events = new List<object>();

        // TODO (Cameron): Add to configuration.
        private readonly Lazy<IMapperProvider> mapProvider = new Lazy<IMapperProvider>(() => new DefaultMapperProvider(), true);

        private readonly TypeInformation typeInformation;

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        protected AggregateRoot()
            : this(GetTypeInformation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        /// <param name="aggregateRootType">The aggregate root type.</param>
        protected AggregateRoot(AggregateRootType aggregateRootType)
            : this(@this => new TypeInformation(aggregateRootType))
        {
        }

        // LINK (Cameron): http://stackoverflow.com/questions/2287636/pass-current-object-type-into-base-constructor-call
        private AggregateRoot(Func<AggregateRoot, TypeInformation> getTypeInformation)
        {
            this.typeInformation = getTypeInformation(this);
        }

        internal string State { get; private set; }

        internal int Revision { get; private set; }

        /// <summary>
        /// Specifies that a mapping should take place.
        /// </summary>
        /// <value>The mapping options.</value>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Not here.")]
        protected IMapperProvider Map
        {
            get { return this.mapProvider.Value; }
        }

        internal void Initialize(object memento, int revision, IEnumerable<object> events, string state)
        {
            Guard.Against.Null(() => events);
            Guard.Against.Negative(() => revision);

            if (memento != null)
            {
                this.SetState(memento);
                this.Revision = revision;
            }

            foreach (var @event in events)
            {
                this.Apply(@event, isNew: false);
            }

            this.State = state;
        }

        internal object GetMemento()
        {
            return this.GetState();
        }

        internal IEnumerable<object> GetUncommittedEvents()
        {
            return this.events;
        }

        internal void CommitEvents(string state)
        {
            Guard.Against.NullOrEmpty(() => state);

            this.events.Clear();
            this.State = state;
        }

        /// <summary>
        /// Gets a memento representing the state of the aggregate root.
        /// </summary>
        /// <returns>A memento representing the state of the aggregate root.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Inappropriate.")]
        protected virtual object GetState()
        {
            return null;
        }

        /// <summary>
        /// Sets the state of the aggregate root from the specified memento.
        /// </summary>
        /// <param name="memento">A memento representing the state of the aggregate root.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "It's fine here.")]
        protected virtual void SetState(object memento)
        {
            throw new RuntimeException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"The aggregate root of type '{0}' has not been configured to reconstitute from a memento representing its state.
To fix this issue:
- override the 'SetState' method of the aggregate root to update the it's state from the specified memento.",
                    this.GetType()))
            {
                HelpLink = "https://github.com/dddlib/dddlib/wiki/Aggregate-Root-Mementos",
            };
        }

        /// <summary>
        /// Applies the specified event to the aggregate root.
        /// </summary>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <param name="event">The event to apply.</param>
        //// NOTE (Cameron): The type constraints on the event stem from the requirements of the mapper functionality.
        protected void Apply<T>(T @event) where T : class, new()
        {
            Guard.Against.Null(() => @event);

            this.ThrowIfLifecycleEnded(@event.GetType().Name);

            if (this.Revision == 0 &&
                !this.typeInformation.EventDispatcher.CanDispatch(@event.GetType()))
            {
                throw new RuntimeException(
                    string.Format(
                        "Unable to dispatch the initial event of type '{0}' to the aggregate root of type '{1}' as there is no corresponding event handler method.",
                        @event.GetType(),
                        this.GetType()));
            }

            this.Apply(@event, isNew: true);
        }

        private void Apply(object @event, bool isNew)
        {
            Guard.Against.Null(() => @event);

            if (!@event.GetType().IsClass)
            {
                // NOTE (Cameron): This is to enforce the class constraint on the protected method for boxed value type events.
                throw new RuntimeException(
                    string.Format(
                        "Unable to apply the specified change of type '{0}' to the aggregate root of type '{1}'.",
                        @event.GetType(),
                        this.GetType()));
            }

            try
            {
                this.typeInformation.EventDispatcher.Dispatch(this, @event);
            }
            catch (RuntimeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RuntimeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The event dispatcher of type '{0}' threw an exception whilst attempting to dispatch an event of type '{1}'.\r\nSee inner exception for details.",
                        this.typeInformation.EventDispatcher.GetType(),
                        @event.GetType()),
                    ex);
            }

            this.Revision++;

            if (this.typeInformation.PersistEvents && isNew)
            {
                this.events.Add(@event);
            }
        }
    }
}
