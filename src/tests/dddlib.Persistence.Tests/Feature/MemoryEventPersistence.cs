// <copyright file="MemoryEventPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using dddlib.Configuration;
    using dddlib.Persistence.Memory;
    using dddlib.Persistence.Sdk;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order save state
    // I need to be able to persist an aggregate root (in memory)
    public abstract class MemoryEventPersistence : Feature
    {
        private IIdentityMap identityMap;
        private IEventStore eventStore;
        private ISnapshotStore snapshotStore;
        private IEventStoreRepository repository;

        [Background]
        public override void Background()
        {
            base.Background();

            "Given an identity map"
                .f(ctx => this.identityMap = new MemoryIdentityMap().Using(ctx));

            "And an event store"
                .f(ctx => this.eventStore = new MemoryEventStore().Using(ctx));

            "And a snapshot store"
                .f(() => this.snapshotStore = new MemorySnapshotStore());

            "And an event store repository"
                .f(() => this.repository = new EventStoreRepository(this.identityMap, this.eventStore, this.snapshotStore));
        }

        public class UndefinedNaturalKey : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a persistence exception is thrown"
                    .f(() => action.ShouldThrow<PersistenceException>());
            }

            public class Subject : AggregateRoot
            {
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class UndefinedUnititializedFactory : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with no defined uninitialized factory"
                    .f(() => instance = new Subject("nonsense"));

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a persistence exception is thrown"
                    .f(() => action.ShouldThrow<PersistenceException>());
            }

            public class Subject : AggregateRoot
            {
                public Subject(string nonsense)
                {
                }

                [NaturalKey]
                public string Id { get; set; }
            }
        }

        public class NullNaturalKey : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject instance, Action action)
            {
                "Given an instance of an aggregate root with a null natural key"
                    .f(() => instance = new Subject());

                "When that instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(instance));

                "Then a persistence exception is thrown"
                    .f(() => action.ShouldThrow<ArgumentException>());
            }

            public class Subject : AggregateRoot
            {
                [NaturalKey]
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.GetRevision().Should().Be(saved.GetRevision()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                protected override object GetState()
                {
                    return this.Id;
                }

                protected override void SetState(object memento)
                {
                    this.Id = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test2"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And something happened to that instance"
                    .f(() => saved.DoSomething());

                "And that instance is saved again to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.GetRevision().Should().Be(saved.GetRevision()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndLoadAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded, Subject anotherLoaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test3"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "And something happened to that loaded instance"
                    .f(() => loaded.DoSomething());

                "And that loaded instance is saved to the repository"
                    .f(() => this.repository.Save(loaded));

                "When another instance is loaded from the repository"
                    .f(() => anotherLoaded = this.repository.Load<Subject>(saved.Id));

                "Then the other loaded instance should be the loaded instance"
                    .f(() => anotherLoaded.Should().Be(loaded));

                "And their revisions should be equal"
                    .f(() => anotherLoaded.GetRevision().Should().Be(loaded.GetRevision()));

                "And their mementos should match"
                    .f(() => anotherLoaded.GetMemento().ShouldMatch(loaded.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SnapshotAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test4"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is snapshot to the repository"
                    .f(() =>
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);
                        this.snapshotStore.PutSnapshot(
                            streamId,
                            new Snapshot
                            {
                                StreamRevision = saved.GetRevision(),
                                Memento = saved.GetMemento(),
                            });
                    });

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.GetRevision().Should().Be(saved.GetRevision()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                protected override object GetState()
                {
                    return this.Id;
                }

                protected override void SetState(object memento)
                {
                    this.Id = memento.ToString();
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SnapshotAndSaveAndLoad : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(Subject saved, Subject loaded, IEnumerable<object> events)
            {
                "Given an instance of an aggregate root"
                    .f(() => saved = new Subject("test5"));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is snapshot to the repository"
                    .f(() =>
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);
                        this.snapshotStore.PutSnapshot(
                            streamId,
                            new Snapshot
                            {
                                StreamRevision = saved.GetRevision(),
                                Memento = saved.GetMemento(),
                            });
                    });

                "And something happened to that instance"
                    .f(() => saved.DoSomething());

                "And that instance is saved again to the repository"
                    .f(() => this.repository.Save(saved));

                "When that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(saved.Id));

                "And the events for that instance are loaded from the event store"
                    .f(() => 
                    {
                        Guid streamId;
                        this.identityMap.TryGet(typeof(Subject), typeof(string), saved.Id, out streamId);

                        string state;
                        events = this.eventStore.GetStream(streamId, 0, out state);
                    });

                "Then the loaded instance should be the saved instance"
                    .f(() => loaded.Should().Be(saved));

                "And their revisions should be equal"
                    .f(() => loaded.GetRevision().Should().Be(saved.GetRevision()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));

                "And their mementos should match"
                    .f(() => loaded.GetMemento().ShouldMatch(saved.GetMemento()));

                "And the loaded events should contain two matching events"
                    .f(() =>
                    {
                        events.Should().HaveCount(2);
                        events.First().Should().BeOfType<NewSubject>();
                        events.First().As<NewSubject>().Should().Match<NewSubject>(@event => @event.Id == saved.Id);
                        events.Last().Should().BeOfType<SubjectDidSomething>();
                        events.Last().As<SubjectDidSomething>().Should().Match<SubjectDidSomething>(@event => @event.Id == saved.Id);
                    });
            }

            public class Subject : AggregateRoot
            {
                private bool hasDoneSomething;

                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void DoSomething()
                {
                    this.Apply(new SubjectDidSomething { Id = this.Id });
                }

                protected override object GetState()
                {
                    return new Memento
                    {
                        Id = this.Id,
                        HasDoneSomething = this.hasDoneSomething,
                    };
                }

                protected override void SetState(object memento)
                {
                    var subject = memento as Memento;

                    this.Id = subject.Id;
                    this.hasDoneSomething = subject.HasDoneSomething;
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDidSomething @event)
                {
                    this.hasDoneSomething = true;
                }

                private class Memento
                {
                    public string Id { get; set; }

                    public bool HasDoneSomething { get; set; }
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDidSomething
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }

        public class SaveAndEndLifecycleAndSaveAndCreate : MemoryEventPersistence
        {
            [Scenario]
            public void Scenario(string naturalKey, Subject saved, Subject loaded, Subject temporallyNew, Subject actual, Action action)
            {
                "Given a natural key value"
                    .f(() => naturalKey = "naturalKey");

                "And an instance of an aggregate root with that natural key"
                    .f(() => saved = new Subject(naturalKey));

                "And that instance is saved to the repository"
                    .f(() => this.repository.Save(saved));

                "And that instance is loaded from the repository"
                    .f(() => loaded = this.repository.Load<Subject>(naturalKey));

                "And that instance is destroyed"
                    .f(() => loaded.Destroy());

                "And no further operations can occur against that instance"
                    .f(() => ((Action)(() => loaded.Destroy())).ShouldThrow<BusinessException>());

                "And that destroyed instance is saved to the repository"
                    .f(() => this.repository.Save(loaded));

                "When a temporally new instance of an aggregate root with that same natural key is created"
                    .f(() => temporallyNew = new Subject(naturalKey));

                "And that temporally new instance is saved to the repository"
                    .f(() => action = () => this.repository.Save(temporallyNew));

                "Then the operation completes without an exception being thrown"
                    .f(() => action.ShouldNotThrow());

                "And further operations can occur against that instance"
                    .f(() =>
                    {
                        actual = this.repository.Load<Subject>(naturalKey);
                        action = () => actual.Destroy();
                        action.ShouldNotThrow();
                    });
            }

            public class Subject : AggregateRoot
            {
                public Subject(string id)
                {
                    this.Apply(new NewSubject { Id = id });
                }

                internal Subject()
                {
                }

                [NaturalKey]
                public string Id { get; private set; }

                public void Destroy()
                {
                    this.Apply(new SubjectDestroyed { Id = this.Id });
                }

                private void Handle(NewSubject @event)
                {
                    this.Id = @event.Id;
                }

                private void Handle(SubjectDestroyed @event)
                {
                    this.EndLifecycle();
                }
            }

            public class NewSubject
            {
                public string Id { get; set; }
            }

            public class SubjectDestroyed
            {
                public string Id { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.AggregateRoot<Subject>().ToReconstituteUsing(() => new Subject());
                }
            }
        }
    }
}
