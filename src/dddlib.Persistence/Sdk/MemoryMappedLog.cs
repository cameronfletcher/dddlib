// <copyright file="MemoryMappedLog.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if PERSISTENCE
namespace dddlib.Persistence.Sdk
#else
namespace dddlib.Persistence.EventDispatcher.Sdk
#endif
{
    using System;
    using System.Collections.Generic;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Represents the memory mapped log.
    /// </summary>
    /// <typeparam name="T">The type of message to log.</typeparam>
    public sealed class MemoryMappedLog<T> : IDisposable
    {
        private const string Namespace = @"Local\{4442507C-0CCF-4C1E-864C-59B821E3DD7D}";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly List<T> contents = new List<T>();

        private readonly MemoryMappedFile file;
        private readonly Mutex mutex;
        private readonly EventWaitHandle waitHandle;

        private int index;
        private long readOffset;
        private long writeOffset;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedLog{T}"/> class.
        /// </summary>
        public MemoryMappedLog()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedLog{T}"/> class.
        /// </summary>
        /// <param name="name">The name of the log.</param>
        public MemoryMappedLog(string name)
        {
            if (!string.IsNullOrEmpty(name) && !name.All(char.IsLetterOrDigit))
            {
                throw new ArgumentException("name");
            }

            var logNamespace = string.IsNullOrEmpty(name)
                ? Namespace
                : string.Concat(Namespace, ".", name);

            var mutexSecuritySettings = new MutexSecurity();
            mutexSecuritySettings.AddAccessRule(
                new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow));

            var waitHandleSecuritySettings = new EventWaitHandleSecurity();
            waitHandleSecuritySettings.AddAccessRule(
                new EventWaitHandleAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                EventWaitHandleRights.FullControl,
                AccessControlType.Allow));

            this.file = MemoryMappedFile.CreateOrOpen(string.Concat(logNamespace, ".", typeof(MemoryMappedFile).Name), 10 * 1024 * 1024);

            var created = false;
            this.mutex = new Mutex(false, string.Concat(logNamespace, ".", typeof(Mutex).Name), out created, mutexSecuritySettings);
            this.waitHandle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                string.Concat(logNamespace, ".", typeof(EventWaitHandle).Name),
                out created,
                waitHandleSecuritySettings);
        }

        /// <summary>
        /// Tries the append.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>True if the message was appended.</returns>
        public bool TryAppend(T message)
        {
            ////Guard.Against.Null(() => message);

            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();

                var serializedMessage = Serializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(serializedMessage);

                using (var accessor = this.file.CreateViewAccessor(this.writeOffset, 4 + buffer.Length))
                {
                    accessor.Write(0, buffer.Length);
                    accessor.WriteArray(4, buffer, 0, buffer.Length);
                    this.waitHandle.Set();
                }

                this.writeOffset += 4 + buffer.Length;
            }

            return true;
        }

        /// <summary>
        /// Tries the read.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>True if the message was read.</returns>
        public bool TryRead(out T message)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();
            }

            if (this.index >= this.contents.Count)
            {
                message = default(T);
                return false;
            }

            message = this.contents[this.index++];
            return true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            this.file.Dispose();
            this.mutex.Dispose();
            this.waitHandle.Dispose();
        }

        private void Synchronize()
        {
            var length = 0;
            do
            {
                using (var accessor = this.file.CreateViewAccessor(this.readOffset, 4))
                {
                    length = accessor.ReadInt32(0);
                    if (length == 0)
                    {
                        break;
                    }
                }

                var buffer = new byte[length];
                using (var accessor = this.file.CreateViewAccessor(this.readOffset + 4, length))
                {
                    accessor.ReadArray(0, buffer, 0, length);
                }

                var serializedMessage = Encoding.UTF8.GetString(buffer);
                var message = Serializer.Deserialize<T>(serializedMessage);

                this.contents.Add(message);
                this.readOffset += 4 + buffer.Length;
            }
            while (length > 0);

            this.writeOffset = this.readOffset;
        }

        private sealed class ExclusiveCodeBlock : IDisposable
        {
            private readonly Mutex mutex;

            private bool hasHandle;

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

            public void Dispose()
            {
                if (this.hasHandle)
                {
                    this.mutex.ReleaseMutex();
                }
            }
        }
    }
}
