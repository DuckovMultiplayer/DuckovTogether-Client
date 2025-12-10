using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace EscapeFromDuckovCoopMod
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _actionQueue = new();
        
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            _actionQueue.Enqueue(action);
        }
        
        public static void EnsureInitialized()
        {
            var _ = Instance;
        }
        
        private void Update()
        {
            while (_actionQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] Error: {ex.Message}");
                }
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
