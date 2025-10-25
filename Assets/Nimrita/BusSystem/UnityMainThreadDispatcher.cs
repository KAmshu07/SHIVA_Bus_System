using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static readonly object _instanceLock = new object();
    private static bool _quitting;

    private static UnityMainThreadDispatcher _instance;

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_quitting)
            {
                throw new InvalidOperationException("UnityMainThreadDispatcher has been shut down and cannot be reacquired.");
            }

            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = FindExistingDispatcher();
                        if (_instance == null)
                        {
                            var obj = new GameObject(nameof(UnityMainThreadDispatcher));
                            _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                        }
                    }
                }
            }
            return _instance;
        }
    }

    private static UnityMainThreadDispatcher FindExistingDispatcher()
    {
#if UNITY_2020_1_OR_NEWER
        var existing = FindObjectOfType<UnityMainThreadDispatcher>(true);
#else
        var existing = FindObjectOfType<UnityMainThreadDispatcher>();
#endif
        if (existing != null)
        {
            existing.InitializePersistentInstance();
        }
        return existing;
    }

    private void Awake()
    {
        InitializePersistentInstance();
    }

    private void OnDestroy()
    {
        lock (_instanceLock)
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                var action = _executionQueue.Dequeue();
                action?.Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;
        if (_quitting) return;

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    public Task EnqueueAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_quitting)
        {
            tcs.SetCanceled();
            return tcs.Task;
        }

        void WrappedAction()
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(WrappedAction);
        }

        return tcs.Task;
    }

    private void InitializePersistentInstance()
    {
        lock (_instanceLock)
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
