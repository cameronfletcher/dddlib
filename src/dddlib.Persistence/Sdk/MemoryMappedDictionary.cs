// <copyright file="MemoryMappedDictionary.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if PERSISTENCE
namespace dddlib.Persistence.Sdk
#else
namespace dddlib.Persistence.EventDispatcher.Sdk
#endif
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Represents a memory-mapped dictionary.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public sealed class MemoryMappedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        private const string Namespace = @"Local\{4442507C-0CCF-4C1E-864C-59B821E3DD7D}";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly ManualResetEvent resetEvent = new ManualResetEvent(false);

        private readonly Dictionary<TKey, TValue> dictionary;
        private readonly MemoryMappedFile file;
        private readonly Mutex mutex;
        private readonly EventWaitHandle waitHandle;

        private long readOffset;
        private long writeOffset;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedDictionary{TKey, TValue}"/> class.
        /// </summary>
        public MemoryMappedDictionary()
            : this(EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="name">The name of the dictionary.</param>
        public MemoryMappedDictionary(string name)
            : this(name, EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="name">The name of the dictionary.</param>
        /// <param name="listen">If set to <c>true</c> will cause the dictionary to listen (nonsense).</param>
        public MemoryMappedDictionary(string name, bool listen)
            : this(name, EqualityComparer<TKey>.Default)
        {
            if (listen)
            {
                Task.Factory.StartNew(this.Notify);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        public MemoryMappedDictionary(IEqualityComparer<TKey> comparer)
            : this(null, comparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedDictionary{TKey, TValue}" /> class.
        /// </summary>
        /// <param name="name">The name of the dictionary.</param>
        /// <param name="comparer">The comparer.</param>
        public MemoryMappedDictionary(string name, IEqualityComparer<TKey> comparer)
        {
            if (!string.IsNullOrEmpty(name) && !name.All(char.IsLetterOrDigit))
            {
                throw new ArgumentException("name");
            }

            var dictionaryNamespace = string.IsNullOrEmpty(name)
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

            this.dictionary = new Dictionary<TKey, TValue>(comparer);
            this.file = MemoryMappedFile.CreateOrOpen(string.Concat(dictionaryNamespace, ".", typeof(MemoryMappedFile).Name), 10 * 1024 * 1024);

            var created = false;
            this.mutex = new Mutex(false, string.Concat(dictionaryNamespace, ".", typeof(Mutex).Name), out created, mutexSecuritySettings);
            this.waitHandle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                string.Concat(dictionaryNamespace, ".", typeof(EventWaitHandle).Name),
                out created,
                waitHandleSecuritySettings);
        }

        /// <summary>
        /// Occurs when the dictionary is changed.
        /// </summary>
        public event EventHandler OnChange;

        private enum Operation : ushort
        {
            Create = 1,
            Update = 2,
            Delete = 3,
            DeleteAll = 4,
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the keys of the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <value>The keys from the dictionary.</value>
        public ICollection<TKey> Keys
        {
            get
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                this.Synchronize();
                return this.dictionary.Keys;
            }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.Generic.ICollection`1" /> containing the values in the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <value>The values from the dictionary.</value>
        public ICollection<TValue> Values
        {
            get
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                this.Synchronize();
                return this.dictionary.Values;
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <value>The number of elements.</value>
        public int Count
        {
            get
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                this.Synchronize();
                return this.dictionary.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        /// <value>Returns <c>true</c> if the dictionary is read-only; otherwise <c>false</c>.</value>
        public bool IsReadOnly
        {
            get
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return false;
            }
        }

        /// <summary>
        /// Gets or sets the value with the specified key.
        /// </summary>
        /// <value>The value.</value>
        /// <param name="key">The key for the value.</param>
        /// <returns>The value for the specified key.</returns>
        public TValue this[TKey key]
        {
            get
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                this.Synchronize();
                return this.dictionary[key];
            }

            set
            {
                if (this.isDisposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                this.Synchronize();
                this.Write(Operation.Update, key, value);
            }
        }

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        public void Add(TKey key, TValue value)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();
            this.Write(Operation.Create, key, value);
        }

        /// <summary>
        /// Removes the element with the specified key from the <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        /// Returns <c>true</c> if the element is successfully removed; otherwise, <c>false</c>.
        /// This method also returns <c>false</c> if <paramref name="key" /> was not found in the original <see cref="T:System.Collections.Generic.IDictionary`2" />.
        /// </returns>
        public bool Remove(TKey key)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();
            this.Write(Operation.Delete, key, default(TValue));
            return this.dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        public void Clear()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Write(Operation.DeleteAll, default(TKey), default(TValue));
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            this.Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return this.dictionary.ContainsKey(item.Key);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="T:System.Collections.Generic.IDictionary`2" />.</param>
        /// <returns>
        /// Returns <c>true</c> if the <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the key;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();
            return this.dictionary.ContainsKey(key);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();
            return this.dictionary.GetEnumerator();
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            return this.Remove(item.Key);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
        /// <returns>Returns <c>true</c> if the object that implements <see cref="T:System.Collections.Generic.IDictionary`2" /> contains an element with the specified key; otherwise, <c>false</c>.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.Synchronize();
            return this.dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            return this.dictionary.GetEnumerator();
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

            this.resetEvent.Set();
            this.resetEvent.Dispose();

            this.file.Dispose();
            this.mutex.Dispose();
            this.waitHandle.Dispose();

            this.dictionary.Clear();
        }

        // LINK (Cameron): http://weblogs.asp.net/ricardoperes/local-machine-interprocess-communication-with-net
        private void Notify()
        {
            while (WaitHandle.WaitAny(new WaitHandle[] { this.resetEvent, this.waitHandle }) == 1)
            {
                if (this.isDisposed)
                {
                    break;
                }

                if (this.OnChange != null)
                {
                    this.OnChange.Invoke(this, new EventArgs());
                }
            }
        }

        private void Synchronize()
        {
            Trace.TraceInformation(
                "[{0:00}] MemoryMappedDictionary.Synchronize (writeOffset = {1}, readOffset = {2}",
                Thread.CurrentThread.ManagedThreadId,
                this.writeOffset,
                this.readOffset);

            var length = 0;
            do
            {
                using (var accessor = this.file.CreateViewAccessor(this.readOffset, 2))
                {
                    length = accessor.ReadUInt16(0);
                    if (length == 0)
                    {
                        break;
                    }
                }

                var buffer = new byte[length];
                using (var accessor = this.file.CreateViewAccessor(this.readOffset + 2, length))
                {
                    accessor.ReadArray(0, buffer, 0, length);
                }

                var serializedEvent = Encoding.UTF8.GetString(buffer);
                var memoryMappedEvent = Serializer.Deserialize<MemoryMappedEvent>(serializedEvent);

                this.readOffset += 2 + buffer.Length;

                switch (memoryMappedEvent.Op)
                {
                    case Operation.Create:
                        this.dictionary.Add(memoryMappedEvent.Key, memoryMappedEvent.Value);
                        break;

                    case Operation.Update:
                        this.dictionary[memoryMappedEvent.Key] = memoryMappedEvent.Value;
                        break;

                    case Operation.Delete:
                        this.dictionary.Remove(memoryMappedEvent.Key);
                        break;

                    case Operation.DeleteAll:
                        this.dictionary.Clear();
                        break;
                }
            }
            while (length > 0);

            this.writeOffset = this.readOffset;
        }

        private void Write(Operation operation, TKey key, TValue value)
        {
            using (new ExclusiveCodeBlock(this.mutex))
            {
                this.Synchronize();

                // NOTE (Cameron): Verification.
                switch (operation)
                {
                    case Operation.Create:
                        new Dictionary<TKey, TValue>(this.dictionary).Add(key, value);
                        break;

                    case Operation.Update:
                        new Dictionary<TKey, TValue>(this.dictionary)[key] = value;
                        break;

                    case Operation.Delete:
                        new Dictionary<TKey, TValue>(this.dictionary).Remove(key);
                        break;

                    case Operation.DeleteAll:
                        break;
                }

                var memoryMappedEvent = new MemoryMappedEvent
                {
                    Op = operation,
                    Key = key,
                    Value = value,
                };

                var serializedEvent = Serializer.Serialize(memoryMappedEvent);
                var buffer = Encoding.UTF8.GetBytes(serializedEvent);

                using (var accessor = this.file.CreateViewAccessor(this.writeOffset, 2 + buffer.Length))
                {
                    accessor.Write(0, (ushort)buffer.Length);
                    accessor.WriteArray(2, buffer, 0, buffer.Length);
                    this.waitHandle.Set();
                }

                this.writeOffset += 2 + buffer.Length;
            }
        }

        private class MemoryMappedEvent
        {
            public Operation Op { get; set; }

            public TKey Key { get; set; }

            public TValue Value { get; set; }
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
