﻿// <copyright file="BootstrapperConfiguration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime.Configuration
{
    internal class BootstrapperConfiguration : IConfiguration
    {
        private readonly RuntimeConfiguration assemblyConfiguration;

        public BootstrapperConfiguration(RuntimeConfiguration assemblyConfiguration)
        {
            Guard.Against.Null(() => assemblyConfiguration);

            this.assemblyConfiguration = assemblyConfiguration;
        }

        public RuntimeConfiguration AssemblyConfiguration
        {
            get { return this.assemblyConfiguration; }
        }

        public IConfigureAggregateRoots AggregateRoots
        {
            get { return new ConfigureAggregateRoots(this.assemblyConfiguration); }
        }

        public IConfigureAggregateRoot<T> AggregateRoot<T>() where T : AggregateRoot
        {
            return new ConfigureAggregateRoot<T>(this.assemblyConfiguration);
        }

        public IConfigureEntity<T> Entity<T>() where T : Entity
        {
            return new ConfigureEntity<T>(this.assemblyConfiguration);
        }
    }
}
