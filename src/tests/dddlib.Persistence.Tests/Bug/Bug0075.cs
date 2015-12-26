// <copyright file = "Bug0075.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using dddlib.Persistence.Memory;
    using Xunit;

    // LINK (Cameron): https://github.com/dddlib/dddlib/issues/75
    public class Bug0075
    {
        [Fact]
        public void ShouldThrowOnObviousHandlerNameMismatch()
        {
            var repository = new MemoryEventStoreRepository();
            var handler = new ObviousMisnamedHandler("abc");

            repository.Save(handler);

            // NOTE (Cameron): This should throw because we're trying to load an aggregate with a natural key of type string using a value object.
            ////Assert.Throws<ArgumentException>(() => repository.Load(registration));
        }

        [Fact]
        public void ShouldThrowOnSubtleHandlerNameMismatch()
        {
            var repository = new MemoryEventStoreRepository();
            var handler = new SubtleMisnamedHandler("abc");

            repository.Save(handler);

            // NOTE (Cameron): This should throw because we're trying to load an aggregate with a natural key of type string using a value object.
            ////Assert.Throws<ArgumentException>(() => repository.Load(registration));
        }

        public class ObviousMisnamedHandler : AggregateRoot
        {
            public ObviousMisnamedHandler(string naturalKey)
            {
                Guard.Against.Null(() => naturalKey);

                this.Apply(new HandlerCreated { NaturalKey = naturalKey });
            }

            internal ObviousMisnamedHandler()
            {
            }

            [NaturalKey]
            public string NaturalKey { get; private set; }

            private void Consume(HandlerCreated @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }
        }

        public class SubtleMisnamedHandler : AggregateRoot
        {
            public SubtleMisnamedHandler(string naturalKey)
            {
                Guard.Against.Null(() => naturalKey);

                this.Apply(new HandlerCreated { NaturalKey = naturalKey });
            }

            internal SubtleMisnamedHandler()
            {
            }

            [NaturalKey]
            public string NaturalKey { get; private set; }

            private void Apply(HandlerCreated @event)
            {
                this.NaturalKey = @event.NaturalKey;
            }
        }

        public class HandlerCreated
        {
            public string NaturalKey { get; set; }
        }
    }
}
