using System.Collections.Generic;
using ItemStatsSystem;
using UnityEngine;
using HarmonyLib;

namespace EscapeFromDuckovCoopMod.Utils;

public class LootContainerRegistry : MonoBehaviour
{
    public static LootContainerRegistry Instance { get; private set; }
    
    private readonly Dictionary<string, object> _containersByUid = new();
    private readonly Dictionary<Inventory, object> _containersByInventory = new();
    private readonly Dictionary<int, object> _containersByInstanceId = new();
    private readonly List<object> _allContainers = new();
    private readonly object _lock = new();
    
    private void Awake()
    {
        Instance = this;
    }
    
    public void RegisterContainer(object container, string uid = null)
    {
        if (container == null)
        {
            UnityEngine.Debug.LogWarning("[LootRegistry] Attempted to register null container");
            return;
        }
        
        lock (_lock)
        {
            var instanceId = (container as UnityEngine.Object)?.GetInstanceID() ?? 0;
            if (instanceId == 0) return;
            
            if (_containersByInstanceId.ContainsKey(instanceId))
            {
                if (!string.IsNullOrEmpty(uid))
                {
                    _containersByUid[uid] = container;
                }
                return;
            }
            
            _containersByInstanceId[instanceId] = container;
            _allContainers.Add(container);
            
            if (!string.IsNullOrEmpty(uid))
            {
                _containersByUid[uid] = container;
            }
            
            var inv = GetInventorySafe(container);
            if (inv != null)
            {
                _containersByInventory[inv] = container;
            }
            
            Debug.Log($"[LootRegistry] 注册容器: InstanceID={instanceId}, UID={uid ?? "null"}");
        }
    }
    
    public void RegisterContainerWithLootUid(object container, int lootUid)
    {
        if (container == null || lootUid < 0) return;
        RegisterContainer(container, lootUid.ToString());
    }
    
    public void UnregisterContainer(object container)
    {
        if (container == null) return;
        
        lock (_lock)
        {
            var instanceId = (container as UnityEngine.Object)?.GetInstanceID() ?? 0;
            if (instanceId == 0) return;
            
            if (!_containersByInstanceId.ContainsKey(instanceId))
                return;
            
            _containersByInstanceId.Remove(instanceId);
            _allContainers.Remove(container);
            
            var keysToRemove = new List<string>();
            foreach (var kv in _containersByUid)
            {
                if (kv.Value == container)
                    keysToRemove.Add(kv.Key);
            }
            foreach (var key in keysToRemove)
            {
                _containersByUid.Remove(key);
            }
            
            var inv = GetInventorySafe(container);
            if (inv != null && _containersByInventory.ContainsKey(inv))
            {
                _containersByInventory.Remove(inv);
            }
        }
    }
    
    public object FindByUid(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        
        lock (_lock)
        {
            return _containersByUid.TryGetValue(uid, out var container) ? container : null;
        }
    }
    
    public object FindByLootUid(int lootUid)
    {
        if (lootUid < 0) return null;
        return FindByUid(lootUid.ToString());
    }
    
    public object FindByInventory(Inventory inventory)
    {
        if (inventory == null) return null;
        
        lock (_lock)
        {
            return _containersByInventory.TryGetValue(inventory, out var container) ? container : null;
        }
    }
    
    public object FindNearPosition(Vector3 position, float maxDistance = 0.5f)
    {
        lock (_lock)
        {
            object nearest = null;
            float nearestDist = maxDistance;
            
            foreach (var container in _allContainers)
            {
                var obj = container as UnityEngine.Object;
                if (obj == null) continue;
                
                try
                {
                    var transformProp = container.GetType().GetProperty("transform");
                    if (transformProp == null) continue;
                    var tf = transformProp.GetValue(container) as Transform;
                    if (tf == null) continue;
                    
                    var dist = Vector3.Distance(tf.position, position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = container;
                    }
                }
                catch { }
            }
            
            return nearest;
        }
    }
    
    public List<object> GetAllContainers()
    {
        lock (_lock)
        {
            var result = new List<object>(_allContainers.Count);
            foreach (var container in _allContainers)
            {
                var obj = container as UnityEngine.Object;
                if (obj != null)
                    result.Add(container);
            }
            return result;
        }
    }
    
    public void UpdateInventoryMapping(object container)
    {
        if (container == null) return;
        
        lock (_lock)
        {
            var inv = GetInventorySafe(container);
            if (inv != null)
            {
                _containersByInventory[inv] = container;
            }
        }
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _containersByUid.Clear();
            _containersByInventory.Clear();
            _containersByInstanceId.Clear();
            _allContainers.Clear();
        }
    }
    
    private static Inventory GetInventorySafe(object container)
    {
        if (container == null) return null;
        
        try
        {
            var propInfo = container.GetType().GetProperty("Inventory");
            if (propInfo != null)
            {
                return propInfo.GetValue(container) as Inventory;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    public void RefreshFromScene()
    {
        var containerType = AccessTools.TypeByName("InteractableLootbox");
        if (containerType == null) return;
        
        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(bool) });
        if (findMethod == null) return;
        
        var genericMethod = findMethod.MakeGenericMethod(containerType);
        var containers = genericMethod.Invoke(null, new object[] { true }) as UnityEngine.Object[];
        
        if (containers != null)
        {
            foreach (var container in containers)
            {
                if (container != null)
                {
                    RegisterContainer(container);
                }
            }
        }
        
        CleanupInvalidEntries();
    }
    
    private void CleanupInvalidEntries()
    {
        lock (_lock)
        {
            _allContainers.RemoveAll(c => {
                var obj = c as UnityEngine.Object;
                return obj == null;
            });
            
            var keysToRemove = new List<int>();
            foreach (var kv in _containersByInstanceId)
            {
                var obj = kv.Value as UnityEngine.Object;
                if (obj == null)
                    keysToRemove.Add(kv.Key);
            }
            foreach (var key in keysToRemove)
            {
                _containersByInstanceId.Remove(key);
            }
            
            var uidKeysToRemove = new List<string>();
            foreach (var kv in _containersByUid)
            {
                var obj = kv.Value as UnityEngine.Object;
                if (obj == null)
                    uidKeysToRemove.Add(kv.Key);
            }
            foreach (var key in uidKeysToRemove)
            {
                _containersByUid.Remove(key);
            }
            
            var invKeysToRemove = new List<Inventory>();
            foreach (var kv in _containersByInventory)
            {
                var obj = kv.Value as UnityEngine.Object;
                if (kv.Key == null || obj == null)
                    invKeysToRemove.Add(kv.Key);
            }
            foreach (var key in invKeysToRemove)
            {
                _containersByInventory.Remove(key);
            }
        }
    }
}
