using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net;

public class ClientWorldManager : MonoBehaviour
{
    public static ClientWorldManager Instance { get; private set; }
    
    public string CurrentSceneId { get; private set; } = "";
    public float TimeOfDay { get; private set; } = 12f;
    public int Weather { get; private set; } = 0;
    public float WeatherIntensity { get; private set; } = 0f;
    
    private readonly Dictionary<int, DoorState> _doorStates = new();
    private readonly Dictionary<int, SwitchState> _switchStates = new();
    
    private float _serverTimeDelta;
    private bool _isLoadingScene;
    
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
        RegisterEvents();
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        if (CoopNetClient.Instance != null)
        {
            CoopNetClient.Instance.OnWorldSyncReceived += OnWorldSyncReceived;
            CoopNetClient.Instance.OnServerStateReceived += OnServerStateReceived;
        }
        
        var gameScene = FindGameScene();
        if (!string.IsNullOrEmpty(gameScene))
        {
            CurrentSceneId = gameScene;
            var client = DuckovTogetherClient.Instance;
            if (client?.LocalPlayer != null)
            {
                client.LocalPlayer.SceneId = CurrentSceneId;
                client.LocalPlayer.IsInGame = true;
                Debug.Log($"[ClientWorld] Already in scene: {CurrentSceneId}, IsInGame=true");
            }
        }
    }
    
    private void OnServerStateReceived(ServerStateData data)
    {
        Debug.Log($"[ClientWorld] Server state: scene={data.scene}, day={data.gameDay}, time={data.gameTime}");
    }
    
    private void OnWorldSyncReceived(WorldSyncData data)
    {
        Debug.Log($"[ClientWorld] Applying world sync: buildings={data.buildings?.Count ?? 0}");
        
        if (data.buildings != null)
        {
            foreach (var b in data.buildings)
            {
                ApplyBuilding(b);
            }
        }
    }
    
    private void ApplyBuilding(BuildingSyncEntry b)
    {
        Debug.Log($"[ClientWorld] Building: {b.id} type={b.buildingType} pos=({b.posX},{b.posY},{b.posZ})");
    }
    
    private string FindGameScene()
    {
        var excludeScenes = new[] { "MainMenu", "Startup", "LoadingScreen", "LoadingScreen_Black", "Base", "DontDestroyOnLoad" };
        
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !string.IsNullOrEmpty(scene.name))
            {
                bool isExcluded = false;
                foreach (var ex in excludeScenes)
                {
                    if (scene.name.Contains(ex) || scene.name == ex)
                    {
                        isExcluded = true;
                        break;
                    }
                }
                
                if (!isExcluded && (scene.name.Contains("Level") || scene.name.Contains("Scene")))
                {
                    return scene.name;
                }
            }
        }
        
        var active = SceneManager.GetActiveScene();
        if (active.isLoaded && !string.IsNullOrEmpty(active.name))
        {
            bool isExcluded = false;
            foreach (var ex in excludeScenes)
            {
                if (active.name.Contains(ex) || active.name == ex)
                {
                    isExcluded = true;
                    break;
                }
            }
            if (!isExcluded) return active.name;
        }
        
        return "";
    }
    
    private void Update()
    {
        if (DuckovTogetherClient.Instance?.IsConnected == true)
        {
            UpdateLocalPlayer();
        }
    }
    
    private void RegisterEvents() { }
    private void UnregisterEvents() { }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isLoadingScene = false;
        
        var gameScene = FindGameScene();
        if (!string.IsNullOrEmpty(gameScene))
        {
            CurrentSceneId = gameScene;
        }
        else
        {
            CurrentSceneId = scene.name;
        }
        
        SyncAllDoors();
        SyncAllSwitches();
        ApplyTimeOfDay();
        ApplyWeather();
        
        var client = DuckovTogetherClient.Instance;
        if (client?.LocalPlayer != null)
        {
            client.LocalPlayer.SceneId = CurrentSceneId;
            client.LocalPlayer.IsInGame = true;
        }
        
        Debug.Log($"[ClientWorld] Scene loaded: {CurrentSceneId}");
    }
    
    private void UpdateLocalPlayer()
    {
        var client = DuckovTogetherClient.Instance;
        if (client == null) return;
        
        var mainCharacter = CharacterMainControl.Main;
        if (mainCharacter == null)
        {
            client.LocalPlayer.IsInGame = false;
            return;
        }
        
        client.LocalPlayer.IsInGame = true;
        client.LocalPlayer.Position = mainCharacter.transform.position;
        client.LocalPlayer.Rotation = mainCharacter.transform.eulerAngles;
        
        var rb = mainCharacter.GetComponent<Rigidbody>();
        if (rb != null)
        {
            client.LocalPlayer.Velocity = rb.velocity;
        }
        
        var animator = mainCharacter.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            client.LocalPlayer.Speed = animator.GetFloat("Speed");
            client.LocalPlayer.DirX = animator.GetFloat("DirectionX");
            client.LocalPlayer.DirY = animator.GetFloat("DirectionY");
            client.LocalPlayer.HandState = animator.GetInteger("Hand");
            client.LocalPlayer.GunReady = animator.GetBool("GunReady");
            client.LocalPlayer.Dashing = animator.GetBool("Dashing");
            client.LocalPlayer.Reloading = animator.GetBool("Reloading");
        }
        
        var healthType = mainCharacter.GetType().Assembly.GetType("CharacterHealth");
        if (healthType != null)
        {
            var health = mainCharacter.GetComponent(healthType);
            if (health != null)
            {
                var currentField = healthType.GetField("currentHealth") ?? healthType.GetProperty("currentHealth")?.GetGetMethod()?.ReturnParameter?.Member as System.Reflection.FieldInfo;
                var maxField = healthType.GetField("maxHealth") ?? healthType.GetProperty("maxHealth")?.GetGetMethod()?.ReturnParameter?.Member as System.Reflection.FieldInfo;
                if (currentField != null) client.LocalPlayer.CurrentHealth = (float)currentField.GetValue(health);
                if (maxField != null) client.LocalPlayer.MaxHealth = (float)maxField.GetValue(health);
            }
        }
    }
    
    private void OnSceneLoadRequested(string sceneId, float timeOfDay, int weather)
    {
        TimeOfDay = timeOfDay;
        Weather = weather;
        
        Debug.Log($"[ClientWorld] Scene load requested: {sceneId}");
    }
    
    private void OnForceSceneLoad(string sceneId)
    {
        if (_isLoadingScene) return;
        if (CurrentSceneId == sceneId) return;
        
        _isLoadingScene = true;
        Debug.Log($"[ClientWorld] Force loading scene: {sceneId}");
        
        StartCoroutine(LoadSceneAsync(sceneId));
    }
    
    private System.Collections.IEnumerator LoadSceneAsync(string sceneId)
    {
        var async = SceneManager.LoadSceneAsync(sceneId);
        if (async == null)
        {
            Debug.LogError($"[ClientWorld] Failed to load scene: {sceneId}");
            _isLoadingScene = false;
            yield break;
        }
        
        while (!async.isDone)
        {
            yield return null;
        }
    }
    
    private void OnWorldState(string sceneId, float timeOfDay, int weather, float weatherIntensity)
    {
        TimeOfDay = timeOfDay;
        Weather = weather;
        WeatherIntensity = weatherIntensity;
        
        ApplyTimeOfDay();
        ApplyWeather();
        
        if (sceneId != CurrentSceneId && !_isLoadingScene)
        {
            OnForceSceneLoad(sceneId);
        }
    }
    
    private void OnTimeSync(float timeOfDay, long serverTime)
    {
        TimeOfDay = timeOfDay;
        _serverTimeDelta = (serverTime / 10000000f) - Time.realtimeSinceStartup;
        
        ApplyTimeOfDay();
    }
    
    private void OnWeatherSync(int weather, float intensity)
    {
        Weather = weather;
        WeatherIntensity = intensity;
        
        ApplyWeather();
    }
    
    private void OnDoorInteract(int doorId, bool isOpen, int playerId)
    {
        if (!_doorStates.TryGetValue(doorId, out var state))
        {
            state = new DoorState { DoorId = doorId };
            _doorStates[doorId] = state;
        }
        
        state.IsOpen = isOpen;
        state.LastInteractPlayer = playerId;
        
        ApplyDoorState(doorId, isOpen);
    }
    
    private void ApplyTimeOfDay()
    {
        var dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (dayNightCycle != null)
        {
            dayNightCycle.SetTime(TimeOfDay);
        }
        
        var lighting = FindObjectOfType<GlobalLighting>();
        if (lighting != null)
        {
            lighting.SetTimeOfDay(TimeOfDay);
        }
    }
    
    private void ApplyWeather()
    {
        var weatherSystem = FindObjectOfType<WeatherSystem>();
        if (weatherSystem != null)
        {
            weatherSystem.SetWeather(Weather, WeatherIntensity);
        }
    }
    
    private void ApplyDoorState(int doorId, bool isOpen)
    {
        var doors = FindObjectsOfType<Door>();
        foreach (var door in doors)
        {
            if (door.GetInstanceID() == doorId || door.doorId == doorId)
            {
                if (isOpen)
                {
                    door.Open();
                }
                else
                {
                    door.Close();
                }
                break;
            }
        }
    }
    
    private void SyncAllDoors()
    {
        foreach (var kvp in _doorStates)
        {
            ApplyDoorState(kvp.Key, kvp.Value.IsOpen);
        }
    }
    
    private void SyncAllSwitches()
    {
        foreach (var kvp in _switchStates)
        {
            ApplySwitchState(kvp.Key, kvp.Value.IsOn);
        }
    }
    
    private void ApplySwitchState(int switchId, bool isOn)
    {
        var switches = FindObjectsOfType<InteractableSwitch>();
        foreach (var sw in switches)
        {
            if (sw.GetInstanceID() == switchId || sw.switchId == switchId)
            {
                sw.SetState(isOn);
                break;
            }
        }
    }
    
    public void SendDoorInteract(int doorId, bool isOpen)
    {
        DuckovTogetherClient.Instance?.SendDoorInteract(doorId, isOpen);
    }
    
    public void SendSwitchInteract(int switchId, bool isOn)
    {
        DuckovTogetherClient.Instance?.SendJson(new
        {
            type = "switchInteract",
            switchId = switchId,
            isOn = isOn
        });
    }
    
    public void SendExtractStart(string extractId)
    {
        DuckovTogetherClient.Instance?.SendJson(new
        {
            type = "extractStart",
            extractId = extractId
        });
    }
    
    private void OnDestroy()
    {
        UnregisterEvents();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
}

public class DoorState
{
    public int DoorId;
    public bool IsOpen;
    public int LastInteractPlayer;
}

public class SwitchState
{
    public int SwitchId;
    public bool IsOn;
}

public class DayNightCycle : MonoBehaviour
{
    public void SetTime(float timeOfDay) { }
}

public class GlobalLighting : MonoBehaviour
{
    public void SetTimeOfDay(float time) { }
}

public class WeatherSystem : MonoBehaviour
{
    public void SetWeather(int type, float intensity) { }
}

public class Door : MonoBehaviour
{
    public int doorId;
    public void Open() { }
    public void Close() { }
}

public class InteractableSwitch : MonoBehaviour
{
    public int switchId;
    public void SetState(bool isOn) { }
}
