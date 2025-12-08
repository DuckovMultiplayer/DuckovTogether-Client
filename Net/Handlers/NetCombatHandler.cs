using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Net;

public class ClientCombatManager : MonoBehaviour
{
    public static ClientCombatManager Instance { get; private set; }
    
    private readonly Dictionary<int, GrenadeTracker> _activeGrenades = new();
    private int _nextGrenadeId = 1;
    
    public GameObject MuzzleFlashPrefab { get; set; }
    public GameObject BulletTracerPrefab { get; set; }
    public GameObject BloodEffectPrefab { get; set; }
    public GameObject ExplosionPrefab { get; set; }
    
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
        LoadEffectPrefabs();
    }
    
    private void Update()
    {
        UpdateGrenades();
    }
    
    private void RegisterEvents() { }
    private void UnregisterEvents() { }
    
    private void LoadEffectPrefabs()
    {
        MuzzleFlashPrefab = Resources.Load<GameObject>("Effects/MuzzleFlash");
        BulletTracerPrefab = Resources.Load<GameObject>("Effects/BulletTracer");
        BloodEffectPrefab = Resources.Load<GameObject>("Effects/BloodHit");
        ExplosionPrefab = Resources.Load<GameObject>("Effects/Explosion");
    }
    
    private void OnRemoteWeaponFire(int shooterId, int weaponId, Vector3 origin, Vector3 direction)
    {
        SpawnMuzzleFlash(origin, direction, shooterId);
        SpawnBulletTracer(origin, direction);
        PlayFireSound(origin, weaponId);
    }
    
    private void OnDamageReceived(int targetId, int attackerId, float damage, string damageType, Vector3 hitPoint)
    {
        SpawnHitEffect(hitPoint, damageType);
        
        var client = DuckovTogetherClient.Instance;
        if (client == null) return;
        
        var localNetworkId = client.NetworkId;
        if (targetId.ToString() == localNetworkId)
        {
            ApplyLocalDamage(damage, damageType, hitPoint);
        }
    }
    
    private void OnGrenadeThrow(int throwerId, int grenadeType, Vector3 origin, Vector3 velocity)
    {
        var grenadeId = _nextGrenadeId++;
        
        var grenadeObject = CreateGrenadeObject(grenadeType);
        if (grenadeObject == null) return;
        
        grenadeObject.transform.position = origin;
        
        var tracker = new GrenadeTracker
        {
            GrenadeId = grenadeId,
            GrenadeObject = grenadeObject,
            Velocity = velocity,
            ThrowTime = Time.time
        };
        
        _activeGrenades[grenadeId] = tracker;
    }
    
    private void OnGrenadeExplode(int grenadeId, Vector3 position, float radius, float damage)
    {
        if (_activeGrenades.TryGetValue(grenadeId, out var tracker))
        {
            if (tracker.GrenadeObject != null)
            {
                Destroy(tracker.GrenadeObject);
            }
            _activeGrenades.Remove(grenadeId);
        }
        
        SpawnExplosionEffect(position, radius);
        PlayExplosionSound(position);
        
        ApplyExplosionDamageToLocal(position, radius, damage);
    }
    
    private void UpdateGrenades()
    {
        var toRemove = new List<int>();
        
        foreach (var kvp in _activeGrenades)
        {
            var tracker = kvp.Value;
            
            if (tracker.GrenadeObject == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }
            
            tracker.Velocity += Physics.gravity * Time.deltaTime;
            tracker.GrenadeObject.transform.position += tracker.Velocity * Time.deltaTime;
            
            if (Time.time - tracker.ThrowTime > 10f)
            {
                Destroy(tracker.GrenadeObject);
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var id in toRemove)
        {
            _activeGrenades.Remove(id);
        }
    }
    
    private void SpawnMuzzleFlash(Vector3 origin, Vector3 direction, int shooterId)
    {
        var client = DuckovTogetherClient.Instance;
        if (client == null) return;
        
        if (client.RemotePlayers.TryGetValue(shooterId.ToString(), out var playerState))
        {
            if (playerState.CharacterObject != null)
            {
                var muzzlePoint = FindMuzzlePoint(playerState.CharacterObject);
                if (muzzlePoint != null)
                {
                    origin = muzzlePoint.position;
                }
            }
        }
        
        if (MuzzleFlashPrefab != null)
        {
            var flash = Instantiate(MuzzleFlashPrefab, origin, Quaternion.LookRotation(direction));
            Destroy(flash, 0.1f);
        }
    }
    
    private void SpawnBulletTracer(Vector3 origin, Vector3 direction)
    {
        if (BulletTracerPrefab == null) return;
        
        var tracer = Instantiate(BulletTracerPrefab, origin, Quaternion.LookRotation(direction));
        
        var tracerScript = tracer.GetComponent<BulletTracer>();
        if (tracerScript != null)
        {
            tracerScript.Initialize(origin, direction * 500f);
        }
        else
        {
            var rb = tracer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = direction * 500f;
            }
        }
        
        Destroy(tracer, 2f);
    }
    
    private void SpawnHitEffect(Vector3 hitPoint, string damageType)
    {
        GameObject prefab = null;
        
        switch (damageType?.ToLower())
        {
            case "bullet":
            case "projectile":
                prefab = BloodEffectPrefab;
                break;
            case "explosion":
                prefab = ExplosionPrefab;
                break;
            default:
                prefab = BloodEffectPrefab;
                break;
        }
        
        if (prefab != null)
        {
            var effect = Instantiate(prefab, hitPoint, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    private void SpawnExplosionEffect(Vector3 position, float radius)
    {
        if (ExplosionPrefab != null)
        {
            var explosion = Instantiate(ExplosionPrefab, position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * (radius / 5f);
            Destroy(explosion, 3f);
        }
    }
    
    private void PlayFireSound(Vector3 position, int weaponId)
    {
        var audioSource = GetPooledAudioSource();
        if (audioSource == null) return;
        
        audioSource.transform.position = position;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 100f;
        
        var clip = GetWeaponFireSound(weaponId);
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    private void PlayExplosionSound(Vector3 position)
    {
        var audioSource = GetPooledAudioSource();
        if (audioSource == null) return;
        
        audioSource.transform.position = position;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 200f;
        
        var clip = Resources.Load<AudioClip>("Audio/Explosion");
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    private Transform FindMuzzlePoint(GameObject characterObject)
    {
        var muzzle = characterObject.transform.Find("MuzzlePoint");
        if (muzzle != null) return muzzle;
        
        muzzle = characterObject.transform.Find("Weapon/MuzzlePoint");
        if (muzzle != null) return muzzle;
        
        var weapon = characterObject.GetComponentInChildren<ItemAgent_Gun>();
        if (weapon != null)
        {
            var weaponMuzzle = weapon.transform.Find("MuzzlePoint");
            if (weaponMuzzle != null) return weaponMuzzle;
        }
        
        return null;
    }
    
    private GameObject CreateGrenadeObject(int grenadeType)
    {
        var grenadePrefab = Resources.Load<GameObject>($"Grenades/Grenade_{grenadeType}");
        if (grenadePrefab != null)
        {
            return Instantiate(grenadePrefab);
        }
        
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = Vector3.one * 0.1f;
        sphere.name = $"Grenade_{grenadeType}";
        
        var collider = sphere.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        return sphere;
    }
    
    private void ApplyLocalDamage(float damage, string damageType, Vector3 hitPoint)
    {
        var mainCharacter = CharacterMainControl.Main;
        if (mainCharacter == null) return;
        
        mainCharacter.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
    
    private void ApplyExplosionDamageToLocal(Vector3 explosionCenter, float radius, float maxDamage)
    {
        var mainCharacter = CharacterMainControl.Main;
        if (mainCharacter == null) return;
        
        var distance = Vector3.Distance(mainCharacter.transform.position, explosionCenter);
        if (distance > radius) return;
        
        var damageMultiplier = 1f - (distance / radius);
        var damage = maxDamage * damageMultiplier;
        
        mainCharacter.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
    
    private AudioSource GetPooledAudioSource()
    {
        var go = new GameObject("PooledAudio");
        var source = go.AddComponent<AudioSource>();
        Destroy(go, 5f);
        return source;
    }
    
    private AudioClip GetWeaponFireSound(int weaponId)
    {
        return Resources.Load<AudioClip>($"Audio/Weapons/Fire_{weaponId}");
    }
    
    public void SendWeaponFire(int weaponId, Vector3 origin, Vector3 direction, int ammoType)
    {
        DuckovTogetherClient.Instance?.SendWeaponFire(weaponId, origin, direction, ammoType);
    }
    
    public void SendDamage(int targetType, int targetId, float damage, string damageType, Vector3 hitPoint)
    {
        DuckovTogetherClient.Instance?.SendDamage(targetType, targetId, damage, damageType, hitPoint);
    }
    
    private void OnDestroy()
    {
        UnregisterEvents();
        
        foreach (var kvp in _activeGrenades)
        {
            if (kvp.Value.GrenadeObject != null)
            {
                Destroy(kvp.Value.GrenadeObject);
            }
        }
        _activeGrenades.Clear();
        
        if (Instance == this) Instance = null;
    }
    
    private class GrenadeTracker
    {
        public int GrenadeId;
        public GameObject GrenadeObject;
        public Vector3 Velocity;
        public float ThrowTime;
    }
}

public class BulletTracer : MonoBehaviour
{
    private Vector3 _targetPosition;
    private float _speed = 500f;
    private float _lifetime = 2f;
    private float _spawnTime;
    
    public void Initialize(Vector3 start, Vector3 velocity)
    {
        transform.position = start;
        _targetPosition = start + velocity.normalized * 500f;
        _speed = velocity.magnitude;
        _spawnTime = Time.time;
    }
    
    private void Update()
    {
        if (Time.time - _spawnTime > _lifetime)
        {
            Destroy(gameObject);
            return;
        }
        
        var direction = (_targetPosition - transform.position).normalized;
        transform.position += direction * _speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
        
        if (Vector3.Distance(transform.position, _targetPosition) < 1f)
        {
            Destroy(gameObject);
        }
    }
}
