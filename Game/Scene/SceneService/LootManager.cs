















using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using static EscapeFromDuckovCoopMod.LootNet;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public static class LootUiGuards
{
    [ThreadStatic] public static int InLootAddAtDepth;
    [ThreadStatic] public static int BlockNextSendToInventory;
    public static bool InLootAddAt => InLootAddAtDepth > 0;
}

internal static class LootSearchWorldGate
{
    private static readonly Dictionary<Inventory, bool> _world = new();

    private static MemberInfo _miNeedInspection;

    public static void EnsureWorldFlag(Inventory inv)
    {
        if (inv) _world[inv] = true; 
    }

    public static bool IsWorldLootByInventory(Inventory inv)
    {
        if (!inv) return false;
        if (_world.TryGetValue(inv, out var yes) && yes) return true;

        
        
        try
        {
            IEnumerable<InteractableLootbox> boxes;
            var registry = Utils.LootContainerRegistry.Instance;
            
            if (registry != null)
                boxes = registry.GetAllContainers().OfType<InteractableLootbox>();
            else if (Utils.GameObjectCacheManager.Instance != null)
                boxes = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
            else
                boxes = Object.FindObjectsOfType<InteractableLootbox>(true);

            foreach (var b in boxes)
            {
                if (!b) continue;
                if (b.Inventory == inv)
                {
                    var isWorld = b.GetComponent<LootBoxLoader>() != null;
                    if (isWorld) _world[inv] = true;
                    return isWorld;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    internal static bool GetNeedInspection(Inventory inv)
    {
        if (inv == null) return false;
        try
        {
            var m = FindNeedInspectionMember(inv.GetType());
            if (m is FieldInfo fi) return (bool)(fi.GetValue(inv) ?? false);
            if (m is PropertyInfo pi) return (bool)(pi.GetValue(inv) ?? false);
        }
        catch
        {
        }

        return false;
    }

    private static MemberInfo FindNeedInspectionMember(Type t)
    {
        if (_miNeedInspection != null) return _miNeedInspection;
        _miNeedInspection = (MemberInfo)t.GetField("NeedInspection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? t.GetProperty("NeedInspection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return _miNeedInspection;
    }

    internal static void TrySetNeedInspection(Inventory inv, bool v)
    {
        if (!inv) return;
        inv.NeedInspection = v;
    }


    internal static void ForceTopLevelUninspected(Inventory inv)
    {
        if (inv == null) return;
        try
        {
            foreach (var it in inv)
            {
                if (!it) continue;
                try
                {
                    it.Inspected = false;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}

internal static class WorldLootPrime
{
    public static void PrimeIfClient(InteractableLootbox lb)
    {
        var mod = ModBehaviourF.Instance;
        if (mod == null || mod.IsServer) return;
        if (!lb) return;

        var inv = lb.Inventory;
        if (!inv) return;

        
        LootSearchWorldGate.EnsureWorldFlag(inv);

        try
        {
            lb.needInspect = false;
        }
        catch
        {
        }

        try
        {
            inv.NeedInspection = false;
        }
        catch
        {
        }

        
        try
        {
            foreach (var it in inv)
            {
                if (!it) continue;
                try
                {
                    it.Inspected = true;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}

internal static class DeadLootSpawnContext
{
    [ThreadStatic] public static CharacterMainControl InOnDead;
}

public static class LootboxDetectUtil
{
    public static bool IsPrivateInventory(Inventory inv)
    {
        if (inv == null) return false;
        if (ReferenceEquals(inv, PlayerStorage.Inventory)) return true; 
        if (ReferenceEquals(inv, PetProxy.PetInventory)) return true; 
        return false;
    }

    
    private static readonly HashSet<Inventory> _tombInventories = new HashSet<Inventory>();
    private static readonly HashSet<Inventory> _validLootboxInventories = new HashSet<Inventory>();
    private static float _lastCacheClearTime = 0f;
    private static readonly object _cacheLock = new object();

    
    private static int _totalChecks = 0;
    private static int _cacheHits = 0;
    private static int _tombDetections = 0;

    
    
    
    
    
    public static bool IsLootboxInventory(Inventory inv)
    {
        _totalChecks++;

        if (inv == null) return false;

        
        if (IsPrivateInventory(inv)) return false;

        
        if (Time.time - _lastCacheClearTime > 30f)
        {
            lock (_cacheLock)
            {
                if (Time.time - _lastCacheClearTime > 30f) 
                {
                    
                    if (_totalChecks > 0)
                    {
                        float cacheHitRate = (_cacheHits / (float)_totalChecks) * 100f;
                        Debug.Log($"[LootManager] 性能统计 - 总检查: {_totalChecks}, 缓存命中: {_cacheHits} ({cacheHitRate:F1}%), 墓碑检测: {_tombDetections}");
                    }

                    _tombInventories.Clear();
                    _validLootboxInventories.Clear();
                    _lastCacheClearTime = Time.time;

                    
                    _totalChecks = 0;
                    _cacheHits = 0;
                    _tombDetections = 0;
                }
            }
        }

        
        lock (_cacheLock)
        {
            if (_tombInventories.Contains(inv))
            {
                _cacheHits++;
                return false; 
            }
            if (_validLootboxInventories.Contains(inv))
            {
                _cacheHits++;
                return true; 
            }
        }

        
        
        
        try
        {
            var lootbox = inv.GetComponent<InteractableLootbox>();
            if (lootbox != null)
            {
                
                

                
                var objName = inv.gameObject.name;
                bool isTomb = objName.Contains("Tomb") || objName.Contains("墓碑");
                bool isAILoot = objName.Contains("EnemyDie") || objName.Contains("Enemy") || objName.Contains("AI");

                if (isTomb && !isAILoot)
                {
                    
                    lock (_cacheLock)
                    {
                        bool isNewTomb = _tombInventories.Add(inv);
                        if (isNewTomb)
                        {
                            _tombDetections++;
                            Debug.Log($"[LootManager] [{System.DateTime.Now:HH:mm:ss.fff}] 排除玩家墓碑 Inventory: {objName}（首次检测，总计 {_tombDetections} 个墓碑）");
                        }
                    }
                    return false;
                }
                else if (isAILoot)
                {
                    
                    Debug.Log($"[LootManager] 识别为AI战利品盒子: {objName}，允许同步");
                    lock (_cacheLock)
                    {
                        _validLootboxInventories.Add(inv);
                    }
                    return true;
                }
                
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LootManager] 检查墓碑失败: {ex.Message}");
        }

        
        
        try
        {
            var objName = inv.gameObject.name;

            
            
            if (!objName.StartsWith("Inventory_"))
            {
                
                return false;
            }
        }
        catch
        {
            
        }

        
        try
        {
            var dict = InteractableLootbox.Inventories;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    if (kv.Value == inv)
                    {
                        lock (_cacheLock)
                        {
                            _validLootboxInventories.Add(inv); 
                        }
                        return true; 
                    }
                }
            }
        }
        catch
        {
            
        }

        
        return false;
    }

    
    
    
    public static void ClearInventoryCaches()
    {
        lock (_cacheLock)
        {
            _tombInventories.Clear();
            _validLootboxInventories.Clear();
            _lastCacheClearTime = Time.time;
        }
    }
}

public class LootManager : MonoBehaviour
{
    public static LootManager Instance;

    public int _nextLootUid = 1; 

    
    public readonly Dictionary<int, Inventory> _cliLootByUid = new();

    
    private readonly Dictionary<Inventory, int> _invToUidCache = new();

    
    private readonly Dictionary<Inventory, int> _invToPosKeyCache = new();


    public readonly Dictionary<uint, (Inventory inv, int pos)> _cliPendingReorder = new();

    
    public readonly Dictionary<uint, PendingTakeDest> _cliPendingTake = new();

    public readonly Dictionary<int, (int capacity, List<(int pos, ItemSnapshot snap)>)> _pendingLootStatesByUid = new();

    
    public readonly Dictionary<int, Inventory> _srvLootByUid = new();

    
    public readonly Dictionary<Inventory, float> _srvLootMuteUntil = new(new RefEq<Inventory>());

    
    private readonly Dictionary<Inventory, InteractableLootbox> _invToLootboxCache = new(new RefEq<Inventory>());
    private float _lastLootboxCacheUpdate = 0f;
    private const float LOOTBOX_CACHE_REFRESH_INTERVAL = 2f; 

    
    private readonly HashSet<Inventory> _pendingBroadcastInvs = new(new RefEq<Inventory>());
    private bool _hasPendingBroadcasts = false;

    private NetService Service => NetService.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;


    public void Init()
    {
        Instance = this;
        StartCoroutine(PeriodicCleanup());
    }

    
    
    
    private IEnumerator PeriodicCleanup()
    {
        var wait = new WaitForSeconds(5f);
        while (true)
        {
            yield return wait;

            try
            {
                
                var now = Time.time;
                var toRemove = _srvLootMuteUntil.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList();
                foreach (var inv in toRemove)
                {
                    _srvLootMuteUntil.Remove(inv);
                }

                
                var invalidCacheKeys = _invToLootboxCache.Where(kv => !kv.Key || !kv.Value).Select(kv => kv.Key).ToList();
                foreach (var inv in invalidCacheKeys)
                {
                    _invToLootboxCache.Remove(inv);
                }

                if (toRemove.Count > 0 || invalidCacheKeys.Count > 0)
                {
                    Debug.Log($"[LootManager] 清理完成：移除 {toRemove.Count} 个过期静音记录，{invalidCacheKeys.Count} 个失效缓存");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LootManager] 定期清理失败: {ex.Message}");
            }
        }
    }


    public int ComputeLootKey(Transform t)
    {
        if (!t) return -1;
        var v = t.position * 10f;
        var x = Mathf.RoundToInt(v.x);
        var y = Mathf.RoundToInt(v.y);
        var z = Mathf.RoundToInt(v.z);
        return new Vector3Int(x, y, z).GetHashCode();
    }


    public void PutLootId(NetDataWriter w, Inventory inv)
    {
        var scene = SceneManager.GetActiveScene().buildIndex;
        var posKey = -1;
        var instanceId = -1;

        
        try
        {
            
            if (LevelManager.Instance == null)
            {
                
                w.Put(posKey);
                w.Put(instanceId);
                w.Put(scene);
                return;
            }

            
            if (inv != null && _invToPosKeyCache.TryGetValue(inv, out var cachedPosKey))
            {
                posKey = cachedPosKey; 
            }
            else
            {
                
                var dict = InteractableLootbox.Inventories;
                if (inv != null && dict != null)
                    foreach (var kv in dict)
                        if (kv.Value == inv)
                        {
                            posKey = kv.Key;
                            _invToPosKeyCache[inv] = posKey; 
                            break;
                        }
            }
        }
        catch (Exception ex)
        {
            
            Debug.LogWarning($"[LootManager] PutLootId 访问 Inventories 失败: {ex.Message}");
        }

        if (inv != null && (posKey < 0 || instanceId < 0))
        {
            try
            {
                
                var lootbox = FindLootboxByInventory(inv);
                if (lootbox)
                {
                    posKey = ComputeLootKey(lootbox.transform);
                    instanceId = lootbox.GetInstanceID();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LootManager] PutLootId 查找 InteractableLootbox 失败: {ex.Message}");
            }
        }

        
        var lootUid = -1;
        if (inv != null && _invToUidCache.TryGetValue(inv, out var cachedUid))
        {
            lootUid = cachedUid; 
        }
        else if (inv != null)
        {
            
            if (IsServer)
            {
                
                foreach (var kv in _srvLootByUid)
                    if (kv.Value == inv)
                    {
                        lootUid = kv.Key;
                        _invToUidCache[inv] = lootUid; 
                        break;
                    }
            }
            else
            {
                
                foreach (var kv in _cliLootByUid)
                    if (kv.Value == inv)
                    {
                        lootUid = kv.Key;
                        _invToUidCache[inv] = lootUid; 
                        break;
                    }
            }
        }

        w.Put(scene);
        w.Put(posKey);
        w.Put(instanceId);
        w.Put(lootUid);
    }


    public bool TryResolveLootById(int scene, int posKey, int iid, out Inventory inv)
    {
        inv = null;

        
        if (posKey != 0 && TryGetLootInvByKeyEverywhere(posKey, out inv)) return true;

        
        
        if (iid != 0)
            try
            {
                var registry = Utils.LootContainerRegistry.Instance;
                IEnumerable<InteractableLootbox> all;
                
                if (registry != null)
                    all = registry.GetAllContainers().OfType<InteractableLootbox>();
                else if (Utils.GameObjectCacheManager.Instance != null)
                    all = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
                else
                    all = FindObjectsOfType<InteractableLootbox>(true);

                foreach (var b in all)
                {
                    if (!b) continue;
                    if (b.GetInstanceID() == iid && (scene < 0 || b.gameObject.scene.buildIndex == scene))
                    {
                        inv = b.Inventory; 
                        if (inv) return true;
                    }
                }
            }
            catch
            {
            }

        return false; 
    }

    
    public IEnumerator ClearLootLoadingTimeout(Inventory inv, float seconds)
    {
        var t = 0f;
        while (inv && inv.Loading && t < seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (inv && inv.Loading) inv.Loading = false;
    }

    public static int ComputeLootKeyFromPos(Vector3 pos)
    {
        var v = pos * 10f;
        var x = Mathf.RoundToInt(v.x);
        var y = Mathf.RoundToInt(v.y);
        var z = Mathf.RoundToInt(v.z);
        return new Vector3Int(x, y, z).GetHashCode();
    }

    
    
    
    public InteractableLootbox FindLootboxByInventory(Inventory inv)
    {
        if (!inv) return null;

        
        if (_invToLootboxCache.TryGetValue(inv, out var cached) && cached)
        {
            return cached;
        }

        
        if (Time.time - _lastLootboxCacheUpdate > LOOTBOX_CACHE_REFRESH_INTERVAL)
        {
            RefreshLootboxCache();
        }

        
        if (_invToLootboxCache.TryGetValue(inv, out cached) && cached)
        {
            return cached;
        }

        
        
        var registry = Utils.LootContainerRegistry.Instance;
        IEnumerable<InteractableLootbox> boxes;
        
        if (registry != null)
            boxes = registry.GetAllContainers().OfType<InteractableLootbox>();
        else if (Utils.GameObjectCacheManager.Instance != null)
            boxes = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
        else
            boxes = FindObjectsOfType<InteractableLootbox>();

        foreach (var b in boxes)
        {
            if (!b) continue;
            if (b.Inventory == inv)
            {
                _invToLootboxCache[inv] = b; 
                return b;
            }
        }

        return null;
    }

    
    
    
    private void RefreshLootboxCache()
    {
        _lastLootboxCacheUpdate = Time.time;
        _invToLootboxCache.Clear();

        try
        {
            
            var registry = Utils.LootContainerRegistry.Instance;
            IEnumerable<InteractableLootbox> boxes;
            
            if (registry != null)
                boxes = registry.GetAllContainers().OfType<InteractableLootbox>();
            else if (Utils.GameObjectCacheManager.Instance != null)
                boxes = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
            else
                boxes = Object.FindObjectsOfType<InteractableLootbox>(true);

            foreach (var b in boxes)
            {
                if (!b) continue;
                var inv = b.Inventory;
                if (inv)
                {
                    _invToLootboxCache[inv] = b;
                }
            }
            Debug.Log($"[LootManager] 刷新缓存完成，找到 {_invToLootboxCache.Count} 个战利品箱");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LootManager] 刷新缓存失败: {ex.Message}");
        }
    }

    
    public bool TryGetLootboxWorldPos(Inventory inv, out Vector3 pos)
    {
        pos = default;
        if (!inv) return false;

        
        var lootbox = FindLootboxByInventory(inv);
        if (lootbox)
        {
            pos = lootbox.transform.position;
            return true;
        }

        return false;
    }

    
    private bool TryResolveLootByHint(Vector3 posHint, out Inventory inv, float radius = 2.5f)
    {
        inv = null;
        var best = float.MaxValue;
        var registry = Utils.LootContainerRegistry.Instance;
        IEnumerable<InteractableLootbox> boxes;
        
        if (registry != null)
            boxes = registry.GetAllContainers().OfType<InteractableLootbox>();
        else if (Utils.GameObjectCacheManager.Instance != null)
            boxes = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
        else
            boxes = FindObjectsOfType<InteractableLootbox>();

        foreach (var b in boxes)
        {
            if (!b || b.Inventory == null) continue;
            var d = Vector3.Distance(b.transform.position, posHint);
            if (d < radius && d < best)
            {
                best = d;
                inv = b.Inventory;
            }
        }

        return inv != null;
    }

    
    public void KickLootTimeout(Inventory inv, float seconds = 1.5f)
    {
        StartCoroutine(ClearLootLoadingTimeout(inv, seconds));
    }

    
    public static bool IsCurrentLootInv(Inventory inv)
    {
        var lv = LootView.Instance;
        return lv && inv && ReferenceEquals(inv, lv.TargetInventory);
    }

    public bool Server_TryResolveLootAggressive(int scene, int posKey, int iid, Vector3 posHint, out Inventory inv)
    {
        inv = null;

        
        if (TryResolveLootById(scene, posKey, iid, out inv)) return true;
        if (TryResolveLootByHint(posHint, out inv)) return true;

        
        
        var best = 9f; 
        InteractableLootbox bestBox = null;
        var registry = Utils.LootContainerRegistry.Instance;
        IEnumerable<InteractableLootbox> allBoxes;
        
        if (registry != null)
            allBoxes = registry.GetAllContainers().OfType<InteractableLootbox>();
        else if (Utils.GameObjectCacheManager.Instance != null)
            allBoxes = Utils.GameObjectCacheManager.Instance.Loot.GetAllLootboxes();
        else
            allBoxes = FindObjectsOfType<InteractableLootbox>();

        foreach (var b in allBoxes)
        {
            if (!b || !b.gameObject.activeInHierarchy) continue;
            if (scene >= 0 && b.gameObject.scene.buildIndex != scene) continue;
            var d2 = (b.transform.position - posHint).sqrMagnitude;
            if (d2 < best)
            {
                best = d2;
                bestBox = b;
            }
        }

        if (!bestBox) return false;

        
        inv = bestBox.Inventory; 
        if (!inv) return false;

        
        var dict = InteractableLootbox.Inventories;
        if (dict != null)
        {
            var key = ComputeLootKey(bestBox.transform);
            dict[key] = inv;
        }

        return true;
    }

    public void Server_HandleLootOpenRequest(NetPeer peer, NetDataReader r)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;

        
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();

        
        var lootUid = -1;
        if (r.AvailableBytes >= 4) lootUid = r.GetInt();

        
        byte reqVer = 0;
        if (r.AvailableBytes >= 1) reqVer = r.GetByte();

        
        var posHint = Vector3.zero;
        if (r.AvailableBytes >= 12) posHint = r.GetV3cm();

        Debug.Log($"[LOOT-REQ] 收到客户端请求: scene={scene}, posKey={posKey}, iid={iid}, lootUid={lootUid}, posHint={posHint}");

        
        Inventory inv = null;
        if (lootUid >= 0) _srvLootByUid.TryGetValue(lootUid, out inv);

        if (LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Debug.LogWarning($"[LOOT-REQ] 拒绝：私有Inventory");
            COOPManager.LootNet.Server_SendLootDeny(peer, "no_inv");
            return;
        }

        
        if (inv == null && !Server_TryResolveLootAggressive(scene, posKey, iid, posHint, out inv))
        {
            Debug.LogWarning($"[LOOT-REQ] 无法解析Inventory: scene={scene}, posKey={posKey}, iid={iid}");
            COOPManager.LootNet.Server_SendLootDeny(peer, "no_inv");
            return;
        }

        Debug.Log($"[LOOT-REQ] 成功解析Inventory: {inv?.gameObject?.name}, 物品数={inv?.Content?.Count ?? 0}");

        
        COOPManager.LootNet.Server_SendLootboxState(peer, inv);
    }

    public void NoteLootReorderPending(uint token, Inventory inv, int targetPos)
    {
        if (token != 0 && inv) _cliPendingReorder[token] = (inv, targetPos);
    }

    public static bool TryGetLootInvByKeyEverywhere(int posKey, out Inventory inv)
    {
        inv = null;

        
        try
        {
            var dictA = InteractableLootbox.Inventories;
            if (dictA != null && dictA.TryGetValue(posKey, out inv) && inv) return true;
        }
        catch (Exception ex)
        {
            
            Debug.LogWarning($"[LOOT] InteractableLootbox.Inventories access failed (scene loading?): {ex.Message}");
        }

        
        try
        {
            var lm = LevelManager.Instance;
            
            if (lm == null)
            {
                Debug.LogWarning("[LOOT] LevelManager.Instance is null (scene loading?)");
                return false;
            }

            var dictB = LevelManager.LootBoxInventories;
            if (dictB == null)
            {
                Debug.LogWarning("[LOOT] LevelManager.LootBoxInventories is null (scene loading?)");
                return false;
            }

            if (dictB.TryGetValue(posKey, out inv) && inv)
            {
                
                try
                {
                    var dictA = InteractableLootbox.Inventories;
                    if (dictA != null) dictA[posKey] = inv;
                }
                catch
                {
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            
            Debug.LogWarning($"[LOOT] LevelManager.LootBoxInventories access failed (scene loading?): {ex.Message}");
        }

        inv = null;
        return false;
    }


    public InteractableLootbox ResolveDeadLootPrefabOnServer()
    {
        var any = GameplayDataSettings.Prefabs;
        try
        {
            if (any != null && any.LootBoxPrefab_Tomb != null) return any.LootBoxPrefab_Tomb;
        }
        catch
        {
        }

        if (any != null) return any.LootBoxPrefab;

        return null; 
    }


    
    public void WriteItemRef(NetDataWriter w, Inventory inv, Item item)
    {
        
        var root = item;
        while (root != null && root.PluggedIntoSlot != null) root = root.PluggedIntoSlot.Master;
        var rootIndex = inv != null ? inv.GetIndex(root) : -1;
        w.Put(rootIndex);

        
        var keys = new List<string>();
        var cur = item;
        while (cur != null && cur.PluggedIntoSlot != null)
        {
            var s = cur.PluggedIntoSlot;
            keys.Add(s.Key ?? "");
            cur = s.Master;
        }

        keys.Reverse();
        w.Put(keys.Count);
        foreach (var k in keys) w.Put(k ?? "");
    }


    
    public Item ReadItemRef(NetDataReader r, Inventory inv)
    {
        var rootIndex = r.GetInt();
        var keyCount = r.GetInt();
        var it = inv.GetItemAt(rootIndex);
        for (var i = 0; i < keyCount && it != null; i++)
        {
            var key = r.GetString();
            var slot = it.Slots?.GetSlot(key);
            it = slot != null ? slot.Content : null;
        }

        return it;
    }


    
    public Inventory ResolveLootInv(int scene, int posKey, int iid, int lootUid)
    {
        Inventory inv = null;

        
        if (lootUid >= 0)
        {
            var registry = Utils.LootContainerRegistry.Instance;
            if (registry != null)
            {
                var box = registry.FindByLootUid(lootUid) as InteractableLootbox;
                if (box != null && box.Inventory != null)
                {
                    inv = box.Inventory;
                    Debug.Log($"[LootManager] 通过注册表快速解析: lootUid={lootUid}");
                    return inv;
                }
            }
        }

        
        if (lootUid >= 0)
        {
            if (IsServer)
            {
                if (_srvLootByUid != null && _srvLootByUid.TryGetValue(lootUid, out inv) && inv)
                    return inv;
            }
            else
            {
                if (_cliLootByUid != null && _cliLootByUid.TryGetValue(lootUid, out inv) && inv)
                    return inv;
            }
        }

        
        if (TryResolveLootById(scene, posKey, iid, out inv) && inv)
            return inv;

        return null;
    }

    public bool Server_IsLootMuted(Inventory inv)
    {
        if (!inv) return false;
        if (_srvLootMuteUntil.TryGetValue(inv, out var until))
        {
            if (Time.time < until) return true;
            _srvLootMuteUntil.Remove(inv); 
        }

        return false;
    }

    public void Server_MuteLoot(Inventory inv, float seconds)
    {
        if (!inv) return;
        _srvLootMuteUntil[inv] = Time.time + Mathf.Max(0.01f, seconds);
    }

    
    
    
    public void Server_QueueLootBroadcast(Inventory inv)
    {
        if (!inv || !IsServer) return;
        if (LootboxDetectUtil.IsPrivateInventory(inv)) return;
        if (!LootboxDetectUtil.IsLootboxInventory(inv)) return;

        
        _pendingBroadcastInvs.Add(inv);

        
        if (!_hasPendingBroadcasts)
        {
            _hasPendingBroadcasts = true;
            DeferedRunner.EndOfFrame(ProcessPendingBroadcasts);
        }
    }

    
    
    
    private void ProcessPendingBroadcasts()
    {
        if (_pendingBroadcastInvs.Count == 0)
        {
            _hasPendingBroadcasts = false;
            return;
        }

        var count = 0;
        foreach (var inv in _pendingBroadcastInvs)
        {
            try
            {
                if (!inv) continue;
                if (Server_IsLootMuted(inv)) continue;
                if (!LootboxDetectUtil.IsLootboxInventory(inv) || LootboxDetectUtil.IsPrivateInventory(inv)) continue;

                COOPManager.LootNet.Server_SendLootboxState(null, inv);
                count++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LootManager] 批量广播失败: {ex.Message}");
            }
        }

        if (count > 0)
        {
            Debug.Log($"[LootManager] 批量广播完成，发送 {count}/{_pendingBroadcastInvs.Count} 个容器状态");
        }

        _pendingBroadcastInvs.Clear();
        _hasPendingBroadcasts = false;
    }

    
    
    
    public void ClearCaches()
    {
        _invToLootboxCache.Clear();
        _invToUidCache.Clear(); 
        _invToPosKeyCache.Clear(); 
        _pendingBroadcastInvs.Clear();
        _hasPendingBroadcasts = false;
        _lastLootboxCacheUpdate = 0f;
        Debug.Log("[LootManager] 缓存已清理");
    }

    private sealed class RefEq<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T a, T b)
        {
            return ReferenceEquals(a, b);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    public int GetLootUid(Inventory inv)
    {
        if (!inv) return -1;
        if (_invToUidCache.TryGetValue(inv, out var uid)) return uid;
        foreach (var kv in _srvLootByUid)
        {
            if (ReferenceEquals(kv.Value, inv))
            {
                _invToUidCache[inv] = kv.Key;
                return kv.Key;
            }
        }
        foreach (var kv in _cliLootByUid)
        {
            if (ReferenceEquals(kv.Value, inv))
            {
                _invToUidCache[inv] = kv.Key;
                return kv.Key;
            }
        }
        return -1;
    }

    public int GetLootScene(Inventory inv)
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
    }

    public string SerializeLootState(Inventory inv)
    {
        if (!inv) return "{}";
        return "{}";
    }
}