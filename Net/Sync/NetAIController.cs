using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net;

public class RemoteAIManager : MonoBehaviour
{
    public static RemoteAIManager Instance { get; private set; }
    
    private readonly Dictionary<int, RemoteAIController> _remoteAIControllers = new();
    private bool _initialized;
    
    public float InterpolationDelay { get; set; } = 0.1f;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        Initialize();
        RegisterEvents();
    }
    
    private void Update()
    {
        if (!_initialized) return;
        
        var client = DuckovTogetherClient.Instance;
        if (client == null || !client.IsConnected) return;
        
        CleanupDeadAI();
    }
    
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Debug.Log("[RemoteAIManager] Initialized");
    }
    
    private void RegisterEvents() { }
    private void UnregisterEvents() { }
    
    private void SyncRemoteAI(Dictionary<int, RemoteAIState> remoteAI)
    {
        foreach (var kvp in remoteAI)
        {
            var aiId = kvp.Key;
            var state = kvp.Value;
            
            if (!_remoteAIControllers.TryGetValue(aiId, out var controller))
            {
                controller = FindOrCreateAIController(aiId, state);
                if (controller == null) continue;
                
                _remoteAIControllers[aiId] = controller;
            }
            
            controller.UpdateState(state);
        }
    }
    
    private RemoteAIController FindOrCreateAIController(int aiId, RemoteAIState state)
    {
        var existingAI = FindExistingAI(aiId);
        
        if (existingAI != null)
        {
            var controller = existingAI.GetComponent<RemoteAIController>();
            if (controller == null)
            {
                controller = existingAI.AddComponent<RemoteAIController>();
                controller.AIId = aiId;
                controller.InterpolationDelay = InterpolationDelay;
            }
            
            state.AIObject = existingAI;
            return controller;
        }
        
        return null;
    }
    
    private GameObject FindExistingAI(int aiId)
    {
        var netAiTags = FindObjectsOfType<NetAiTag>();
        foreach (var tag in netAiTags)
        {
            if (tag.aiId == aiId)
            {
                return tag.gameObject;
            }
        }
        
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("AI_") || obj.name.Contains("Enemy_"))
            {
                var instanceId = obj.GetInstanceID();
                if (instanceId == aiId || obj.name.Contains(aiId.ToString()))
                {
                    return obj;
                }
            }
        }
        
        return null;
    }
    
    private void CleanupDeadAI()
    {
        var toRemove = new List<int>();
        
        foreach (var kvp in _remoteAIControllers)
        {
            var controller = kvp.Value;
            if (controller == null || controller.gameObject == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }
            
            var client = DuckovTogetherClient.Instance;
            if (client != null && client.RemoteAI.TryGetValue(kvp.Key, out var state))
            {
                if (state.IsDead && controller.IsDeathAnimationComplete())
                {
                    toRemove.Add(kvp.Key);
                }
            }
        }
        
        foreach (var id in toRemove)
        {
            RemoveAI(id);
        }
    }
    
    private void RemoveAI(int aiId)
    {
        if (_remoteAIControllers.TryGetValue(aiId, out var controller))
        {
            if (controller != null)
            {
                Destroy(controller);
            }
            _remoteAIControllers.Remove(aiId);
        }
    }
    
    private void OnAIHealthChanged(int aiId, float currentHealth, float maxHealth)
    {
        if (_remoteAIControllers.TryGetValue(aiId, out var controller))
        {
            controller.OnHealthChanged(currentHealth, maxHealth);
        }
    }
    
    public void RemoveAllAI()
    {
        foreach (var kvp in _remoteAIControllers)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        _remoteAIControllers.Clear();
    }
    
    public RemoteAIController GetAIController(int aiId)
    {
        _remoteAIControllers.TryGetValue(aiId, out var controller);
        return controller;
    }
    
    private void OnDestroy()
    {
        UnregisterEvents();
        RemoveAllAI();
        if (Instance == this) Instance = null;
    }
}

public class RemoteAIController : MonoBehaviour
{
    public int AIId { get; set; }
    public float InterpolationDelay { get; set; } = 0.1f;
    
    private readonly List<AITransformSnapshot> _snapshots = new();
    private const int MAX_SNAPSHOTS = 20;
    
    private Animator _animator;
    private MonoBehaviour _characterAI;
    
    private float _lastUpdateTime;
    private bool _isDead;
    private float _deathTime;
    
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirXHash = Animator.StringToHash("DirectionX");
    private static readonly int DirYHash = Animator.StringToHash("DirectionY");
    private static readonly int HandHash = Animator.StringToHash("Hand");
    private static readonly int GunReadyHash = Animator.StringToHash("GunReady");
    private static readonly int DashingHash = Animator.StringToHash("Dashing");
    private static readonly int DeadHash = Animator.StringToHash("Dead");
    
    private float _animSpeed;
    private float _animDirX;
    private float _animDirY;
    private int _animHand;
    private bool _animGunReady;
    private bool _animDashing;
    
    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        
        var aiComponents = GetComponents<MonoBehaviour>();
        foreach (var comp in aiComponents)
        {
            var typeName = comp.GetType().Name.ToLower();
            if (typeName.Contains("characterai") || typeName.Contains("aicontroller"))
            {
                _characterAI = comp;
                comp.enabled = false;
                break;
            }
        }
        
        DisableLocalAIComponents();
    }
    
    private void DisableLocalAIComponents()
    {
        var aiComponents = GetComponents<MonoBehaviour>();
        foreach (var comp in aiComponents)
        {
            if (comp == this) continue;
            
            var typeName = comp.GetType().Name.ToLower();
            if (typeName.Contains("ai") && (typeName.Contains("behavior") || typeName.Contains("decision") || typeName.Contains("pathfind")))
            {
                comp.enabled = false;
            }
        }
        
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        InterpolatePosition();
        UpdateAnimation();
    }
    
    public void UpdateState(RemoteAIState state)
    {
        if (_isDead && !state.IsDead)
        {
            _isDead = false;
        }
        
        if (state.IsDead && !_isDead)
        {
            OnDeath();
            return;
        }
        
        var now = Time.time;
        
        _snapshots.Add(new AITransformSnapshot
        {
            Time = now,
            Position = state.Position,
            Forward = state.Forward
        });
        
        while (_snapshots.Count > MAX_SNAPSHOTS)
        {
            _snapshots.RemoveAt(0);
        }
        
        _animSpeed = state.Speed;
        _animDirX = state.DirX;
        _animDirY = state.DirY;
        _animHand = state.HandState;
        _animGunReady = state.GunReady;
        _animDashing = state.Dashing;
        
        _lastUpdateTime = now;
    }
    
    private void InterpolatePosition()
    {
        if (_snapshots.Count < 2)
        {
            if (_snapshots.Count == 1)
            {
                transform.position = Vector3.Lerp(transform.position, _snapshots[0].Position, Time.deltaTime * 10f);
                
                if (_snapshots[0].Forward != Vector3.zero)
                {
                    var targetRot = Quaternion.LookRotation(_snapshots[0].Forward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                }
            }
            return;
        }
        
        var renderTime = Time.time - InterpolationDelay;
        
        AITransformSnapshot? from = null;
        AITransformSnapshot? to = null;
        
        for (int i = 0; i < _snapshots.Count - 1; i++)
        {
            if (_snapshots[i].Time <= renderTime && _snapshots[i + 1].Time >= renderTime)
            {
                from = _snapshots[i];
                to = _snapshots[i + 1];
                break;
            }
        }
        
        if (from.HasValue && to.HasValue)
        {
            var t = (renderTime - from.Value.Time) / (to.Value.Time - from.Value.Time);
            t = Mathf.Clamp01(t);
            
            transform.position = Vector3.Lerp(from.Value.Position, to.Value.Position, t);
            
            if (from.Value.Forward != Vector3.zero && to.Value.Forward != Vector3.zero)
            {
                var fromRot = Quaternion.LookRotation(from.Value.Forward);
                var toRot = Quaternion.LookRotation(to.Value.Forward);
                transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            }
        }
        else if (_snapshots.Count > 0)
        {
            var latest = _snapshots[_snapshots.Count - 1];
            transform.position = Vector3.Lerp(transform.position, latest.Position, Time.deltaTime * 5f);
            
            if (latest.Forward != Vector3.zero)
            {
                var targetRot = Quaternion.LookRotation(latest.Forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }
    }
    
    private void UpdateAnimation()
    {
        if (_animator == null) return;
        
        _animator.SetFloat(SpeedHash, _animSpeed);
        _animator.SetFloat(DirXHash, _animDirX);
        _animator.SetFloat(DirYHash, _animDirY);
        _animator.SetInteger(HandHash, _animHand);
        _animator.SetBool(GunReadyHash, _animGunReady);
        _animator.SetBool(DashingHash, _animDashing);
    }
    
    public void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (currentHealth <= 0 && !_isDead)
        {
            OnDeath();
        }
    }
    
    private void OnDeath()
    {
        _isDead = true;
        _deathTime = Time.time;
        
        if (_animator != null)
        {
            _animator.SetBool(DeadHash, true);
            _animator.SetTrigger("Die");
        }
        
        Debug.Log($"[RemoteAI] AI {AIId} died");
    }
    
    public bool IsDeathAnimationComplete()
    {
        if (!_isDead) return false;
        return Time.time - _deathTime > 5f;
    }
    
    private struct AITransformSnapshot
    {
        public float Time;
        public Vector3 Position;
        public Vector3 Forward;
    }
}
