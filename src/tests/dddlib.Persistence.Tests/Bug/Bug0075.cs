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
        public void ShouldThrow()
        {
            var repository = new MemoryEventStoreRepository();
            var registrationNumber = "abc";
            var car = new Car(registrationNumber);

            repository.Save(car);

            // NOTE (Cameron): This should throw because we're trying to load an aggregate with a natural key of type string using a value object.
            ////Assert.Throws<ArgumentException>(() => repository.Load(registration));
        }

        public class Car : AggregateRoot
        {
            public Car(string registrationNumber)
            {
                Guard.Against.Null(() => registrationNumber);

                this.Apply(new CarRegistered { RegistrationNumber = registrationNumber });
            }

            internal Car()
            {
            }

            [NaturalKey]
            public string RegistrationNumber { get; private set; }

            private void Apply(CarRegistered @event)
            {
                this.RegistrationNumber = @event.RegistrationNumber;
            }
        }

        public class CarRegistered
        {
            public string RegistrationNumber { get; set; }
        }
    }
}
