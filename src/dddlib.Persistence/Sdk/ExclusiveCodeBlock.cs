// <copyright file="ExclusiveCodeBlock.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if PERSISTENCE
namespace dddlib.Persistence.Sdk
#else
namespace dddlib.Persistence.EventDispatcher.Sdk
#endif
{
    using System;
    using System.Threading;

    /// <summary>
    /// Represents an exclusive code block.
    /// </summary>
    public sealed class ExclusiveCodeBlock : IDisposable
    {
        private readonly Mutex mutex;

        private bool hasHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExclusiveCodeBlock"/> class.
        /// </summary>
        /// <param name="mutex">The mutex.</param>
        /// <exception cref="System.TimeoutException">Timeout waiting for exclusive access to the mutex for the memory event store.</exception>
        public ExclusiveCodeBlock(Mutex mutex)
        {
            this.mutex = mutex;

            // LINK (Cameron): http://stackoverflow.com/questions/229565/what-is-a-good-pattern-for-using-a-global-mutex-in-c
            this.hasHandle = false;
            try
            {
                this.hasHandle = this.mutex.WaitOne(5000, false);
                if (this.hasHandle == false)
                {
                    throw new TimeoutException("Timeout waiting for exclusive access to the mutex for the memory event store.");
                }
            }
            catch (AbandonedMutexException)
            {
                // NOTE (Cameron): The mutex was abandoned in another process, it will still get acquired.
                this.hasHandle = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.hasHandle)
            {
                this.mutex.ReleaseMutex();
            }
        }
    }
}
