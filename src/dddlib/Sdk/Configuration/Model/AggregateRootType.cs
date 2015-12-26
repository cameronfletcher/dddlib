// <copyright file="AggregateRootType.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk.Configuration.Model
{
    using System;
    using System.Globalization;
    using dddlib.Runtime;

    /// <summary>
    /// Represents an aggregate root type.
    /// </summary>
    public class AggregateRootType : EntityType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRootType"/> class.
        /// </summary>
        /// <param name="runtimeType">The runtime type.</param>
        /// <param name="typeAnalyzerService">The type analyzer service.</param>
        /// <param name="baseEntity">The base entity.</param>
        public AggregateRootType(Type runtimeType, ITypeAnalyzerService typeAnalyzerService, EntityType baseEntity)
            : base(runtimeType, typeAnalyzerService, baseEntity)
        {
            Guard.Against.Null(() => runtimeType);
            Guard.Against.Null(() => typeAnalyzerService);

            if (!typeAnalyzerService.IsValidAggregateRoot(runtimeType))
            {
                throw new BusinessException(
                    string.Format(CultureInfo.InvariantCulture, "The specified runtime type '{0}' is not an aggregate root.", runtimeType));
            }

            // NOTE (Cameron): Defaults.
            this.UninitializedFactory = typeAnalyzerService.GetUninitializedFactory(runtimeType);
            this.EventDispatcher = new DefaultEventDispatcher(runtimeType);
        }

        /// <summary>
        /// Gets the uninitialized factory.
        /// </summary>
        /// <value>The uninitialized factory.</value>
        public Delegate UninitializedFactory { get; private set; }

        /// <summary>
        /// Gets the event dispatcher.
        /// </summary>
        /// <value>The event dispatcher.</value>
        public IEventDispatcher EventDispatcher { get; private set; }

        /// <summary>
        /// Gets a value indicating whether events should be persisted.
        /// </summary>
        /// <value>Returns <c>true</c> if events should be persisted; otherwise, <c>false</c>.</value>
        //// NOTE (Cameron): Only persist events if there is a way to reconstitute the persisted object.
        public bool PersistEvents
        {
            get { return this.UninitializedFactory != null; }
        }

        /// <summary>
        /// Configures the uninitialized factory.
        /// </summary>
        /// <param name="uninitializedFactory">The uninitialized factory.</param>
        public void ConfigureUninitializedFactory(Delegate uninitializedFactory)
        {
            Guard.Against.Null(() => uninitializedFactory);

            // TODO (Cameron): Validate?
            this.UninitializedFactory = uninitializedFactory;
        }
    }
}
