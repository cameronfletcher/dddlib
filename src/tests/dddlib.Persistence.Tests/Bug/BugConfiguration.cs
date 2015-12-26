// <copyright file = "BugConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Bug
{
    using dddlib.Configuration;

    internal class BugConfiguration : IBootstrapper
    {
        public void Bootstrap(IConfiguration configure)
        {
            configure.AggregateRoot<Bug0043.Car>().ToReconstituteUsing(() => new Bug0043.Car());
            configure.AggregateRoot<Bug0064.Thing>().ToReconstituteUsing(() => new Bug0064.Thing());
            configure.AggregateRoot<Bug0075.Car>().ToReconstituteUsing(() => new Bug0075.Car());
        }
    }
}
