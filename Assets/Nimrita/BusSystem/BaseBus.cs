using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

public abstract class BaseBus<T> where T : class
{
    protected readonly BusConfig Config;
    protected readonly string BusName;
    private readonly Dictionary<Type, List<SubscriberWrapper>> _subscribers = new Dictionary<Type, List<SubscriberWrapper>>();
    private readonly Dictionary<string, TaskCompletionSource<object>> _pendingResponses = new Dictionary<string, TaskCompletionSource<object>>();
    private readonly object _lock = new object();
    private long _publishedCount;
    private long _deliveredCount;
    private long _droppedCount;

    public BaseBus<T> ParentBus { get; private set; }
    private readonly List<BaseBus<T>> _childBuses = new List<BaseBus<T>>();
    public BusScope Scope => Config.Scope;

    protected BaseBus(BusConfig config, string busName, BaseBus<T> parentBus = null)
    {
        Config = config;
        BusName = busName;

        if (parentBus != null)
        {
            ParentBus = parentBus;
            parentBus._childBuses.Add(this);
        }
    }

    public void Subscribe<U>(Action<U> handler, int priority = 0) where U : T
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var messageType = typeof(U);

        Action<object> wrapper = (object message) =>
        {
            try
            {
                if (!IsHandlerAlive(handler))
                {
                    return;
                }

                if (message is IStandardMessage standardMessage && standardMessage.RequiresResponse)
                {
                    ProcessMessageResponse(standardMessage, handler, (U)message);
                }
                else
                {
                    handler((U)message);
                }
            }
            catch (Exception ex)
            {
                if (Config.EnableAdvancedLogging)
                {
                    Debug.LogError($"[{BusName}] Error handling {messageType}: {ex}");
                }
            }
        };

        lock (_lock)
        {
            if (!_subscribers.TryGetValue(messageType, out var subscribers))
            {
                subscribers = new List<SubscriberWrapper>();
                _subscribers[messageType] = subscribers;
            }

            var subscriber = new SubscriberWrapper(
                handler,
                wrapper,
                Config.EnablePriority ? priority : 0,
                handler.Target
            );

            InsertSubscriber(subscribers, subscriber);

            if (Config.EnableAdvancedLogging)
            {
                Debug.Log($"[{BusName}] Subscribed handler for {messageType}");
            }
        }
    }

    private bool IsHandlerAlive<U>(Action<U> handler) where U : T
    {
        var target = handler.Target;
        if (target is MonoBehaviour behaviour)
        {
            return behaviour != null;
        }
        return target != null;
    }

    private void InsertSubscriber(List<SubscriberWrapper> subscribers, SubscriberWrapper subscriber)
    {
        if (Config.EnablePriority)
        {
            int index = subscribers.FindIndex(s => s.Priority < subscriber.Priority);
            if (index == -1) subscribers.Add(subscriber);
            else subscribers.Insert(index, subscriber);
        }
        else
        {
            subscribers.Add(subscriber);
        }
    }

    private async void ProcessMessageResponse<U>(IStandardMessage message, Action<U> handler, U typedMessage)
    {
        try
        {
            handler(typedMessage);

            bool hasPending;
            lock (_lock)
            {
                hasPending = _pendingResponses.ContainsKey(message.MessageId);
            }

            if (hasPending)
            {
                await UnityMainThreadDispatcher.Instance.EnqueueAsync(() => { });
            }
        }
        catch (Exception ex)
        {
            TaskCompletionSource<object> tcs = null;
            lock (_lock)
            {
                _pendingResponses.TryGetValue(message.MessageId, out tcs);
            }
            tcs?.TrySetException(ex);
            if (Config.EnableAdvancedLogging)
            {
                Debug.LogError($"[{BusName}] Error processing message response: {ex}");
            }
        }
    }

    public void Respond<TResponse>(IStandardMessage message, TResponse response)
    {
        SendResponse(message.MessageId, response);
    }

    public void SendResponse<TResponse>(string messageId, TResponse response)
    {
        TaskCompletionSource<object> tcs = null;
        lock (_lock)
        {
            _pendingResponses.TryGetValue(messageId, out tcs);
        }
        tcs?.TrySetResult(response);
    }

    public async Task<TResponse> Request<TMessage, TResponse>(TMessage message, TimeSpan? timeout = null)
        where TMessage : class, T, IStandardMessage
        where TResponse : class
    {
        // Check if message is properly configured for responses
        if (!message.RequiresResponse)
        {
            throw new InvalidOperationException(
                $"Message of type {typeof(TMessage).Name} must have RequiresResponse=true. " +
                $"Use the constructor parameter: new {typeof(TMessage).Name}(requiresResponse: true)");
        }

        // Check if we have any handlers for this message type
        if (!HasSubscribersFor<TMessage>())
        {
            throw new InvalidOperationException(
                $"No handlers are subscribed for message type {typeof(TMessage).Name}. " +
                $"Make sure at least one handler is subscribed before making this request.");
        }

        // Use default timeout of 5 seconds if not specified
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);

        // Use the existing implementation
        return await PublishWithResponse<TMessage, TResponse>(message, actualTimeout);
    }

    public async Task<TResponse> PublishWithResponse<U, TResponse>(U message, TimeSpan timeout)
     where U : class, T, IStandardMessage
    {
        if (!message.RequiresResponse)
        {
            throw new InvalidOperationException($"Message of type {typeof(U)} is not configured to require a response");
        }

        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            _pendingResponses[message.MessageId] = tcs;
        }

        try
        {
            Publish(message);
            var result = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

            if (result != tcs.Task)
            {
                throw new TimeoutException($"Response for message {message.MessageId} not received within timeout period");
            }

            return (TResponse)await tcs.Task;
        }
        finally
        {
            lock (_lock)
            {
                _pendingResponses.Remove(message.MessageId);
            }
        }
    }

    public bool HasSubscribersFor<U>() where U : T
    {
        var messageType = typeof(U);
        lock (_lock)
        {
            return _subscribers.TryGetValue(messageType, out var subs) &&
                   subs.Count > 0 &&
                   subs.Any(s => s.IsAlive);
        }
    }

    public void Unsubscribe<U>(Action<U> handler) where U : T
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var messageType = typeof(U);
        lock (_lock)
        {
            if (_subscribers.TryGetValue(messageType, out var subscribers))
            {
                int removed = subscribers.RemoveAll(sub => sub.ShouldRemove(handler));

                if (subscribers.Count == 0)
                {
                    _subscribers.Remove(messageType);
                }

                if (removed > 0 && Config.EnableAdvancedLogging)
                {
                    Debug.Log($"[{BusName}] Unsubscribed handler for {messageType}");
                }
            }
        }
    }

    public virtual void Publish<U>(U message) where U : class, T
    {
        Interlocked.Increment(ref _publishedCount);

        // Publish locally first
        if (Config.EnableAsyncDispatch)
        {
            PublishAsync(message);
        }
        else
        {
            PublishInternal(message);
        }

        // Handle propagation if message supports it
        if (message is IScopedMessage scopedMsg && scopedMsg.Scope == Scope)
        {
            PropagateMessage(message, scopedMsg.Propagation);
        }
        else if (message is IScopedEvent scopedEvt && scopedEvt.Scope == Scope)
        {
            PropagateMessage(message, scopedEvt.Propagation);
        }
    }

    private void PropagateMessage<U>(U message, PropagationBehavior propagation) where U : class, T
    {
        // Propagate up if needed
        if ((propagation == PropagationBehavior.UpToParent ||
             propagation == PropagationBehavior.UpAndDown) && ParentBus != null)
        {
            ParentBus.Publish(message);
        }

        // Propagate down if needed
        if (propagation == PropagationBehavior.DownToChildren ||
            propagation == PropagationBehavior.UpAndDown)
        {
            foreach (var childBus in _childBuses)
            {
                childBus.Publish(message);
            }
        }
    }

    private async void PublishAsync<U>(U message) where U : class, T
    {
        await UnityMainThreadDispatcher.Instance.EnqueueAsync(() => PublishInternal(message));
    }

    private void PublishInternal<U>(U message) where U : class, T
    {
        var messageType = typeof(U);
        List<SubscriberWrapper> subscribersCopy = null;
        List<SubscriberWrapper> deadSubscribers = null;

        lock (_lock)
        {
            if (_subscribers.TryGetValue(messageType, out var subscribers))
            {
                subscribersCopy = new List<SubscriberWrapper>(subscribers);
            }
        }

        var handled = false;

        if (subscribersCopy != null)
        {
            foreach (var subscriber in subscribersCopy)
            {
                try
                {
                    if (!subscriber.IsAlive)
                    {
                        (deadSubscribers ??= new List<SubscriberWrapper>()).Add(subscriber);
                        continue;
                    }

                    subscriber.WrappedAction(message);
                    Interlocked.Increment(ref _deliveredCount);
                    handled = true;
                }
                catch (Exception ex)
                {
                    if (Config.EnableAdvancedLogging)
                    {
                        Debug.LogError($"[{BusName}] Exception while dispatching {messageType}: {ex}");
                    }
                }
            }

            CleanupDeadSubscribers(messageType, deadSubscribers);
        }
        else
        {
            CleanupDeadSubscribers(messageType, deadSubscribers);
        }

        if (!handled)
        {
            Interlocked.Increment(ref _droppedCount);
            if (Config.EnableAdvancedLogging)
            {
                Debug.LogWarning($"[{BusName}] No active subscribers for {messageType}");
            }
            if (Config.ThrowOnUnhandledMessages)
            {
                throw new InvalidOperationException(
                    $"[{BusName}] Message {messageType.Name} was published but no subscribers handled it. " +
                    $"Register a handler or disable ThrowOnUnhandledMessages in BusConfig.");
            }
        }
    }

    public BusStatus GetStatusSnapshot()
    {
        lock (_lock)
        {
            var subscriberCounts = new Dictionary<Type, int>();
            foreach (var pair in _subscribers)
            {
                subscriberCounts[pair.Key] = pair.Value.Count(s => s.IsAlive);
            }

            return new BusStatus(
                BusName,
                Scope,
                Interlocked.Read(ref _publishedCount),
                Interlocked.Read(ref _deliveredCount),
                Interlocked.Read(ref _droppedCount),
                _pendingResponses.Count,
                subscriberCounts
            );
        }
    }

    private void CleanupDeadSubscribers(Type messageType, List<SubscriberWrapper> deadSubscribers)
    {
        if (deadSubscribers == null) return;

        lock (_lock)
        {
            if (_subscribers.TryGetValue(messageType, out var currentSubscribers))
            {
                foreach (var dead in deadSubscribers)
                {
                    currentSubscribers.Remove(dead);
                }
                if (currentSubscribers.Count == 0)
                {
                    _subscribers.Remove(messageType);
                }
            }
        }
    }

    private class SubscriberWrapper
    {
        private readonly WeakReference _weakTarget;
        private readonly bool _isMonoBehaviour;

        public Delegate OriginalDelegate { get; }
        public Action<object> WrappedAction { get; }
        public int Priority { get; }

        public bool IsAlive =>
            OriginalDelegate.Target == null ||  // Static method
            (_isMonoBehaviour ?
                (OriginalDelegate.Target as MonoBehaviour) != null :
                _weakTarget.IsAlive);

        public SubscriberWrapper(Delegate original, Action<object> wrapped, int priority, object target)
        {
            OriginalDelegate = original;
            WrappedAction = wrapped;
            Priority = priority;
            _isMonoBehaviour = target is MonoBehaviour;
            _weakTarget = _isMonoBehaviour ? null : new WeakReference(target);
        }

        public bool ShouldRemove<U>(Action<U> handler) where U : T
        {
            return OriginalDelegate.Equals(handler) || !IsAlive;
        }
    }

    public readonly struct BusStatus
    {
        public string Name { get; }
        public BusScope Scope { get; }
        public long PublishedMessages { get; }
        public long DeliveredMessages { get; }
        public long DroppedMessages { get; }
        public int PendingResponses { get; }
        public IReadOnlyDictionary<Type, int> ActiveSubscribers { get; }

        internal BusStatus(
            string name,
            BusScope scope,
            long published,
            long delivered,
            long dropped,
            int pendingResponses,
            IDictionary<Type, int> subscriberCounts)
        {
            Name = name;
            Scope = scope;
            PublishedMessages = published;
            DeliveredMessages = delivered;
            DroppedMessages = dropped;
            PendingResponses = pendingResponses;
            ActiveSubscribers = new Dictionary<Type, int>(subscriberCounts);
        }
    }
}
