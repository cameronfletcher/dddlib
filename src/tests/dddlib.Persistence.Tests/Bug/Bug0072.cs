// <copyright file="Bug0072.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using Xunit;

    public class Bug0072
    {
        [Fact]
        public void ShouldBeEqual()
        {
            var naturalKey = "abc";

            var subject1 = new Subject { Nested = new Nested { NaturalKey = naturalKey } };
            var subject2 = new Subject { Nested = new Nested { NaturalKey = naturalKey } };

            Assert.Equal(subject1, subject2);
        }

        public class Subject : AggregateRoot
        {
            [NaturalKey]
            public Nested Nested { get; set; }
        }

        public class Nested : Entity
        {
            public string NaturalKey { get; set; }
        }
    }
}
