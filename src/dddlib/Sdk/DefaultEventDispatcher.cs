// <copyright file="DefaultEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using dddlib.Runtime;

    /*  TODO (Cameron): 
        Any exceptions? - possibly of type RuntimeException (consider).
        Consider what to do with multiple base classes with different dispatchers.
        Add ability to configure method name.
        Duplicate for application against memento.
        Change to operate on any type, not just AggregateRoot.  */

    /// <summary>
    /// Represents the default event dispatcher.
    /// </summary>
    public sealed class DefaultEventDispatcher : IEventDispatcher
    {
        private static readonly string DefaultMethodName = GetDefaultMethodName();

        private readonly Dictionary<Type, List<Action<object, object>>> handlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEventDispatcher"/> class.
        /// </summary>
        /// <param name="type">The type of the target object.</param>
        public DefaultEventDispatcher(Type type)
            : this(type, DefaultMethodName, BindingFlags.Instance | BindingFlags.NonPublic)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEventDispatcher" /> class.
        /// </summary>
        /// <param name="type">The type of the target object.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="bindingFlags">The binding flags for the method.</param>
        public DefaultEventDispatcher(Type type, string methodName, BindingFlags bindingFlags)
        {
            Guard.Against.Null(() => type);
            Guard.Against.NullOrEmpty(() => methodName);

            if (!CodeGenerator.IsValidLanguageIndependentIdentifier(methodName))
            {
                throw new ArgumentException(
                    "The specified target method name is not a valid language independent identifier.",
                    Guard.Expression.Parse(() => methodName));
            }

            this.handlers = GetHandlers(type, methodName, bindingFlags);
        }

        /// <summary>
        /// Dispatches an event to the specified target object.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="event">The event.</param>
        public void Dispatch(object target, object @event)
        {
            Guard.Against.Null(() => target);
            Guard.Against.Null(() => @event);

            var handlerList = default(List<Action<object, object>>);
            if (this.handlers.TryGetValue(@event.GetType(), out handlerList))
            {
                foreach (var handler in handlerList)
                {
                    handler.Invoke(target, @event);
                }
            }
        }

        /// <summary>
        /// Determines whether this event dispatcher can dispatch the specified event type.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <returns>Returns <c>true</c> if the event can be dispatched; otherwise <c>false</c>.</returns>
        public bool CanDispatch(Type eventType)
        {
            return this.handlers.ContainsKey(eventType);
        }

        private static Dictionary<Type, List<Action<object, object>>> GetHandlers(Type type, string methodName, BindingFlags bindingFlags)
        {
            var handlerMethods = type.GetTypeHierarchyUntil(typeof(object))
                .SelectMany(t => t.GetMethods(bindingFlags))
                .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                .Where(method => method.GetParameters().Count() == 1)
                .Where(method => method.DeclaringType != typeof(object))
                .Select(methodInfo =>
                    new
                    {
                        Info = methodInfo,
                        ParameterType = methodInfo.GetParameters().First().ParameterType,
                    })
                .ToArray();

            var invalidHandlerMethodTypes = handlerMethods
                .Where(method => !method.ParameterType.IsClass)
                .ToArray();

            var handlers = new Dictionary<Type, List<Action<object, object>>>();

            foreach (var handlerMethod in handlerMethods.Except(invalidHandlerMethodTypes))
            {
                var handler = CreateHandlerDelegate(type, handlerMethod.Info);
                var handlerList = default(List<Action<object, object>>);
                if (!handlers.TryGetValue(handlerMethod.ParameterType, out handlerList))
                {
                    handlerList = new List<Action<object, object>>();
                    handlers.Add(handlerMethod.ParameterType, handlerList);
                }

                handlerList.Add(handler);
            }

            return handlers;
        }

        // LINK (Cameron): http://www.sapiensworks.com/blog/post/2012/04/19/Invoking-A-Private-Method-On-A-Subclass.aspx
        private static Action<object, object> CreateHandlerDelegate(Type declaringType, MethodInfo methodInfo)
        {
            var dynamicMethod = new DynamicMethod(
                string.Empty,
                typeof(void),
                new[] { typeof(object), typeof(object) },
                declaringType.Module,
                true);

            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);          // load this
            il.Emit(OpCodes.Ldarg_1);          // load event
            il.Emit(OpCodes.Call, methodInfo); // call apply method
            il.Emit(OpCodes.Ret);              // return

            return dynamicMethod.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
        }

        // LINK (Cameron): http://blog.functionalfun.net/2009/10/getting-methodinfo-of-generic-method.html
        private static string GetDefaultMethodName()
        {
            Expression<Action<DefaultEventDispatcher>> expression = aggregate => aggregate.Handle(default(object));
            var lambda = (LambdaExpression)expression;
            var methodCall = (MethodCallExpression)lambda.Body;
            return methodCall.Method.Name;
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "By design.")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "event", Justification = "Also, by design.")]
        private void Handle(object @event)
        {
        }
    }
}
