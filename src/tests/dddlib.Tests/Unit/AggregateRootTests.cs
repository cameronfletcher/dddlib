// <copyright file="AggregateRootTests.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using dddlib.Runtime;
    using FluentAssertions;
    using Sdk;
    using Xunit;

    public class AggregateRootTests
    {
        [Fact]
        public void CanInstantiateEmptyAggregate()
        {
            // act
            new EmptyAggregate();
        }

        [Fact]
        public void CanApplyHandledChangeToAggregate()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().Be(@event);
        }

        [Fact]
        public void CanApplyMishandledChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingElseHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }
        
        [Fact]
        public void CanApplyHandledInheritedChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingWierdHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CanApplyUnhandledChangeToAggregateWithoutEffectOrException()
        {
            // arrange
            var aggregate = new ChangeableAggregate();
            var @event = new SomethingNeverHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CannotApplyInvalidChangeToAggregate()
        {
            // arrange
            var aggregate = new BadAggregate();
            var @event = 1;

            // act
            Action action = () => aggregate.ApplyEvent(@event);

            // assert
            action.ShouldThrow<RuntimeException>();
        }

        [Fact]
        public void CannotApplyOtherInvalidChangeToAggregate()
        {
            // arrange
            var aggregate = new BadAggregate();
            int? @event = 1;

            // act
            Action action = () => aggregate.ApplyEvent(@event);

            // assert
            action.ShouldThrow<RuntimeException>();
        }

        [Fact]
        public void CanApplyMultipleHandledChangeToAggregate()
        {
            // arrange
            var aggregate = new MoreChangeableAggregate();
            var @event = new SomethingHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().Be(@event);
            aggregate.OtherChange.Should().Be(@event);
        }

        [Fact]
        public void CannotHandleChangeFromBaseAggregate()
        {
            // arrange
            var aggregate = new OverridingAggregate();
            var @event = new SomethingWierdHappened();

            // act
            aggregate.ApplyEvent(@event);

            // assert
            aggregate.Change.Should().BeNull();
        }

        [Fact]
        public void CanEndLifecycle()
        {
            // arrange
            var aggregate = new LifecycleAggregate();

            // act
            aggregate.DoSomething(); // NOTE (Cameron): Proves we can do something before we destroy.
            aggregate.Destroy();
            Action action = () => aggregate.DoSomething();

            // assert
            action.ShouldThrow<BusinessException>();
        }

        [Fact]
        public void CanReconstitute()
        {
            // arrange
            var memento = (object)null;
            var events = new[] { new SomethingHappened() };
            var factory = new AggregateRootFactory();
            
            // act
            var aggregateRoot = factory.Create<PersistedAggregate>(memento, 0, events, "state");
            aggregateRoot.MakeSomethingHappen();

            // assert
            aggregateRoot.ThingsThatHappened.Should().HaveCount(2);
        }

        public class AggregateCreated
        {
        }

        public class SomethingHappened
        {
        }

        public class SomethingElseHappened
        {
        }

        public class SomethingWierdHappened : SomethingHappened
        {
        }

        public class SomethingNeverHappened
        {
        }

        public class LifecycleEnded
        {
        }

        public class ChangeableAggregate : AggregateRoot
        {
            public ChangeableAggregate()
            {
                this.Apply(new AggregateCreated());
            }

            [NaturalKey]
            public string NaturalKey
            {
                get { return string.Empty; }
            }

            public object Change { get; private set; }

            public void ApplyEvent(object change)
            {
                this.Apply(change);
            }

            private void Handle(AggregateCreated @event)
            {
                // NOTE (Cameron): Every aggregate should have a handle on it's initial event or an exception will be thrown.
            }

            private void Handle(SomethingHappened @event)
            {
                this.Change = @event;
            }

            private void Handle(SomethingElseHappened @event, int count)
            {
                this.Change = @event;
            }
        }

        public class BadAggregate : ChangeableAggregate
        {
            public object BadChange { get; private set; }

            private void Handle(int @event)
            {
                this.BadChange = @event;
            }

            private void Handle(int? @event)
            {
                this.BadChange = @event;
            }
        }

        public class EmptyAggregate : AggregateRoot
        {
            [NaturalKey]
            public string NaturalKey
            {
                get { return string.Empty; }
            }
        }

        public class LifecycleAggregate : AggregateRoot
        {
            public LifecycleAggregate()
            {
                this.Apply(new AggregateCreated());
            }

            [NaturalKey]
            public string NaturalKey
            {
                get { return string.Empty; }
            }

            public void Destroy()
            {
                this.Apply(new LifecycleEnded());
            }

            public void DoSomething()
            {
                this.Apply(new SomethingHappened());
            }

            private void Handle(AggregateCreated @event)
            {
                // NOTE (Cameron): Every aggregate should have a handle on it's initial event or an exception will be thrown.
            }

            private void Handle(LifecycleEnded @event)
            {
                this.EndLifecycle();
            }
        }

        public class MoreChangeableAggregate : ChangeableAggregate
        {
            public object OtherChange { get; private set; }

            private void Handle(SomethingHappened @event)
            {
                this.OtherChange = @event;
            }
        }

        public class OverridingAggregate : ChangeableAggregate
        {
            private void Apply(SomethingWierdHappened @event)
            {
            }
        }

        public class PersistedAggregate : AggregateRoot
        {
            private readonly List<object> thingsThatHappened = new List<object>();

            [NaturalKey]
            public string NaturalKey
            {
                get { return string.Empty; }
            }

            public object[] ThingsThatHappened
            {
                get { return this.thingsThatHappened.ToArray(); }
            }

            public void MakeSomethingHappen()
            {
                this.Apply(new SomethingHappened());
            }

            private void Handle(SomethingHappened @event)
            {
                this.thingsThatHappened.Add(@event);
            }
        }
    }
}
