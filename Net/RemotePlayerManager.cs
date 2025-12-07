using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net;

public class RemotePlayerManager : MonoBehaviour
{
    public static RemotePlayerManager Instance { get; private set; }
    
    private readonly Dictionary<string, RemotePlayerController> _remotePlayerControllers = new();
    private GameObject _playerPrefab;
    private bool _initialized;
    
    public float InterpolationDelay { get; set; } = 0.1f;
    public float ExtrapolationLimit { get; set; } = 0.5f;
    
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
    }
    
    private void Update()
    {
        if (!_initialized) return;
        
        var client = DuckovTogetherClient.Instance;
        if (client == null || !client.IsConnected) return;
        
        SyncRemotePlayers(client.RemotePlayers);
        CleanupDisconnectedPlayers(client.RemotePlayers);
    }
    
    public void Initialize()
    {
        if (_initialized) return;
        
        _playerPrefab = FindPlayerPrefab();
        if (_playerPrefab == null)
        {
            Debug.LogWarning("[RemotePlayerManager] Player prefab not found");
        }
        
        _initialized = true;
        Debug.Log("[RemotePlayerManager] Initialized");
    }
    
    private GameObject FindPlayerPrefab()
    {
        var mainCharacter = CharacterMainControl.Main;
        if (mainCharacter != null)
        {
            return mainCharacter.gameObject;
        }
        
        var playerObjects = GameObject.FindGameObjectsWithTag("Player");
        if (playerObjects.Length > 0)
        {
            return playerObjects[0];
        }
        
        return null;
    }
    
    private void SyncRemotePlayers(Dictionary<string, RemotePlayerState> remotePlayers)
    {
        foreach (var kvp in remotePlayers)
        {
            var networkId = kvp.Key;
            var state = kvp.Value;
            
            if (!state.IsInGame) continue;
            
            if (!_remotePlayerControllers.TryGetValue(networkId, out var controller))
            {
                controller = CreateRemotePlayer(networkId, state);
                if (controller == null) continue;
                
                _remotePlayerControllers[networkId] = controller;
            }
            
            controller.UpdateState(state);
        }
    }
    
    private void CleanupDisconnectedPlayers(Dictionary<string, RemotePlayerState> remotePlayers)
    {
        var toRemove = new List<string>();
        
        foreach (var kvp in _remotePlayerControllers)
        {
            if (!remotePlayers.ContainsKey(kvp.Key) || !remotePlayers[kvp.Key].IsInGame)
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var id in toRemove)
        {
            RemoveRemotePlayer(id);
        }
    }
    
    private RemotePlayerController CreateRemotePlayer(string networkId, RemotePlayerState state)
    {
        GameObject playerObject = null;
        
        if (_playerPrefab != null)
        {
            playerObject = Instantiate(_playerPrefab);
            playerObject.name = $"RemotePlayer_{networkId}";
            
            var mainControl = playerObject.GetComponent<CharacterMainControl>();
            if (mainControl != null)
            {
                mainControl.enabled = false;
            }
            
            var inputComponents = playerObject.GetComponents<MonoBehaviour>();
            foreach (var comp in inputComponents)
            {
                var typeName = comp.GetType().Name.ToLower();
                if (typeName.Contains("input") || typeName.Contains("player") && typeName.Contains("control"))
                {
                    comp.enabled = false;
                }
            }
        }
        else
        {
            playerObject = CreatePlaceholderPlayer(networkId);
        }
        
        if (playerObject == null) return null;
        
        playerObject.transform.position = state.Position;
        
        var controller = playerObject.AddComponent<RemotePlayerController>();
        controller.NetworkId = networkId;
        controller.InterpolationDelay = InterpolationDelay;
        controller.ExtrapolationLimit = ExtrapolationLimit;
        
        state.CharacterObject = playerObject;
        
        Debug.Log($"[RemotePlayerManager] Created remote player: {state.PlayerName} ({networkId})");
        return controller;
    }
    
    private GameObject CreatePlaceholderPlayer(string networkId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"RemotePlayer_{networkId}";
        go.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        
        var collider = go.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.6f, 1f);
            renderer.material = mat;
        }
        
        var labelGo = new GameObject("NameLabel");
        labelGo.transform.SetParent(go.transform);
        labelGo.transform.localPosition = new Vector3(0, 1.5f, 0);
        
        return go;
    }
    
    private void RemoveRemotePlayer(string networkId)
    {
        if (_remotePlayerControllers.TryGetValue(networkId, out var controller))
        {
            if (controller != null && controller.gameObject != null)
            {
                Destroy(controller.gameObject);
            }
            _remotePlayerControllers.Remove(networkId);
            
            Debug.Log($"[RemotePlayerManager] Removed remote player: {networkId}");
        }
    }
    
    public void RemoveAllRemotePlayers()
    {
        foreach (var kvp in _remotePlayerControllers)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        _remotePlayerControllers.Clear();
    }
    
    public RemotePlayerController GetRemotePlayer(string networkId)
    {
        _remotePlayerControllers.TryGetValue(networkId, out var controller);
        return controller;
    }
    
    public int GetRemotePlayerCount()
    {
        return _remotePlayerControllers.Count;
    }
    
    private void OnDestroy()
    {
        RemoveAllRemotePlayers();
        if (Instance == this) Instance = null;
    }
}

public class RemotePlayerController : MonoBehaviour
{
    public string NetworkId { get; set; }
    public float InterpolationDelay { get; set; } = 0.1f;
    public float ExtrapolationLimit { get; set; } = 0.5f;
    
    private readonly List<TransformSnapshot> _snapshots = new();
    private const int MAX_SNAPSHOTS = 20;
    
    private Animator _animator;
    private CharacterController _characterController;
    
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private float _lastUpdateTime;
    
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirXHash = Animator.StringToHash("DirectionX");
    private static readonly int DirYHash = Animator.StringToHash("DirectionY");
    private static readonly int HandHash = Animator.StringToHash("Hand");
    private static readonly int GunReadyHash = Animator.StringToHash("GunReady");
    private static readonly int DashingHash = Animator.StringToHash("Dashing");
    private static readonly int ReloadingHash = Animator.StringToHash("Reloading");
    
    private float _animSpeed;
    private float _animDirX;
    private float _animDirY;
    private int _animHand;
    private bool _animGunReady;
    private bool _animDashing;
    private bool _animReloading;
    
    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _characterController = GetComponent<CharacterController>();
        
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }
        
        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
    }
    
    private void Update()
    {
        InterpolatePosition();
        InterpolateRotation();
        UpdateAnimation();
    }
    
    public void UpdateState(RemotePlayerState state)
    {
        var now = Time.time;
        
        _snapshots.Add(new TransformSnapshot
        {
            Time = now,
            Position = state.Position,
            Rotation = Quaternion.Euler(state.Rotation),
            Velocity = state.Velocity
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
        _animReloading = state.Reloading;
        
        _lastUpdateTime = now;
    }
    
    private void InterpolatePosition()
    {
        if (_snapshots.Count < 2)
        {
            if (_snapshots.Count == 1)
            {
                transform.position = Vector3.Lerp(transform.position, _snapshots[0].Position, Time.deltaTime * 10f);
            }
            return;
        }
        
        var renderTime = Time.time - InterpolationDelay;
        
        TransformSnapshot? from = null;
        TransformSnapshot? to = null;
        
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
        }
        else if (_snapshots.Count > 0)
        {
            var latest = _snapshots[_snapshots.Count - 1];
            var timeSinceLastUpdate = Time.time - latest.Time;
            
            if (timeSinceLastUpdate < ExtrapolationLimit)
            {
                transform.position = latest.Position + latest.Velocity * timeSinceLastUpdate;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, latest.Position, Time.deltaTime * 5f);
            }
        }
    }
    
    private void InterpolateRotation()
    {
        if (_snapshots.Count == 0) return;
        
        var latest = _snapshots[_snapshots.Count - 1];
        transform.rotation = Quaternion.Slerp(transform.rotation, latest.Rotation, Time.deltaTime * 15f);
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
        _animator.SetBool(ReloadingHash, _animReloading);
    }
    
    public bool IsStale(float timeout = 5f)
    {
        return Time.time - _lastUpdateTime > timeout;
    }
    
    private struct TransformSnapshot
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
    }
}
