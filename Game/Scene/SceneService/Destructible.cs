















using EscapeFromDuckovCoopMod.Net;
using EscapeFromDuckovCoopMod.Utils; 
using System.Reflection;
using IEnumerator = System.Collections.IEnumerator;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class Destructible
{
    private readonly Dictionary<uint, HealthSimpleBase> _clientDestructibles = new();


    
    private readonly HashSet<uint> _dangerDestructibleIds = new();

    public readonly HashSet<uint> _deadDestructibleIds = new();

    
    private readonly Dictionary<uint, HealthSimpleBase> _serverDestructibles = new();
    private NetService Service => NetService.Instance;

    
    private static FieldInfo _fieldHalfObsticleIsDead;
    private static bool _halfObsticleFieldInitialized = false;

    
    private bool _scanScheduled = false;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;


    public void RegisterDestructible(uint id, HealthSimpleBase hs)
    {
        if (id == 0 || hs == null) return;
        if (IsServer) _serverDestructibles[id] = hs;
        else _clientDestructibles[id] = hs;
    }

    
    
    
    public void ClearDestructibles()
    {
        _serverDestructibles.Clear();
        _clientDestructibles.Clear();
        _deadDestructibleIds.Clear();
        _dangerDestructibleIds.Clear();
    }

    
    public HealthSimpleBase FindDestructible(uint id)
    {
        HealthSimpleBase hs = null;
        if (IsServer) _serverDestructibles.TryGetValue(id, out hs);
        else _clientDestructibles.TryGetValue(id, out hs);
        if (hs) return hs;

        
        if (GameObjectCacheManager.Instance != null)
        {
            hs = GameObjectCacheManager.Instance.Destructibles.FindById(id);
            if (hs)
            {
                RegisterDestructible(id, hs);
                return hs;
            }
        }

        
        var all = Object.FindObjectsOfType<HealthSimpleBase>(true);
        foreach (var e in all)
        {
            var tag = e.GetComponent<NetDestructibleTag>() ?? e.gameObject.AddComponent<NetDestructibleTag>();
            RegisterDestructible(tag.id, e);
            if (tag.id == id) hs = e;
        }

        return hs;
    }


    
    public void Client_ApplyDestructibleDead_Snapshot(uint id)
    {
        if (_deadDestructibleIds.Contains(id)) return;
        var hs = FindDestructible(id);
        if (!hs) return;

        
        var br = hs.GetComponent<Breakable>();
        if (br)
            try
            {
                if (br.normalVisual) br.normalVisual.SetActive(false);
                if (br.dangerVisual) br.dangerVisual.SetActive(false);
                if (br.breakedVisual) br.breakedVisual.SetActive(true);
                if (br.mainCollider) br.mainCollider.SetActive(false);
            }
            catch
            {
            }

        
        var half = hs.GetComponent<HalfObsticle>();
        if (half)
            try
            {
                half.Dead(new DamageInfo());
            }
            catch
            {
            }

        
        try
        {
            foreach (var c in hs.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
        catch
        {
        }

        _deadDestructibleIds.Add(id);
    }

    private static Transform FindBreakableWallRoot(Transform t)
    {
        var p = t;
        while (p != null)
        {
            var nm = p.name;
            if (!string.IsNullOrEmpty(nm) &&
                nm.IndexOf("BreakableWall", StringComparison.OrdinalIgnoreCase) >= 0)
                return p;
            p = p.parent;
        }

        return null;
    }

    private static uint ComputeStableIdForDestructible(HealthSimpleBase hs)
    {
        if (!hs) return 0u;
        var root = FindBreakableWallRoot(hs.transform);
        if (root == null) root = hs.transform;
        try
        {
            return NetDestructibleTag.ComputeStableId(root.gameObject);
        }
        catch
        {
            return 0u;
        }
    }

    
    
    
    private IEnumerator ScanAndMarkInitiallyDeadDestructiblesAsync(int itemsPerFrame = 20)
    {
        if (_deadDestructibleIds == null) yield break;
        if (_serverDestructibles == null || _serverDestructibles.Count == 0) yield break;

        Debug.Log($"[Destructible] 开始扫描 {_serverDestructibles.Count} 个可破坏物，每帧处理 {itemsPerFrame} 个");

        int processed = 0;
        int frameCount = 0;

        foreach (var kv in _serverDestructibles)
        {
            var id = kv.Key;
            var hs = kv.Value;
            if (!hs) continue;
            if (_deadDestructibleIds.Contains(id)) continue;

            var isDead = false;

            
            try
            {
                if (hs.HealthValue <= 0f) isDead = true;
            }
            catch
            {
            }

            
            if (!isDead)
                try
                {
                    var br = hs.GetComponent<Breakable>();
                    if (br)
                    {
                        var brokenView = br.breakedVisual && br.breakedVisual.activeInHierarchy;
                        var mainOff = br.mainCollider && !br.mainCollider.activeSelf;
                        if (brokenView || mainOff) isDead = true;
                    }
                }
                catch
                {
                }

            
            if (!isDead)
                try
                {
                    var half = hs.GetComponent("HalfObsticle"); 
                    if (half != null)
                    {
                        
                        if (!_halfObsticleFieldInitialized)
                        {
                            var t = half.GetType();
                            try
                            {
                                _fieldHalfObsticleIsDead = AccessTools.Field(t, "isDead");
                            }
                            catch {  }
                            _halfObsticleFieldInitialized = true;
                        }

                        
                        if (_fieldHalfObsticleIsDead != null)
                        {
                            var v = _fieldHalfObsticleIsDead.GetValue(half);
                            if (v is bool && (bool)v) isDead = true;
                        }
                    }
                }
                catch
                {
                }

            if (isDead) _deadDestructibleIds.Add(id);

            processed++;
            
            if (processed % itemsPerFrame == 0)
            {
                frameCount++;
                yield return null;
            }
        }

        Debug.Log($"[Destructible] 扫描完成，共处理 {processed} 个物体，用时 {frameCount} 帧，发现 {_deadDestructibleIds.Count} 个已破坏物体");

        
        var syncUI = WaitingSynchronizationUI.Instance;
        if (syncUI != null) syncUI.CompleteTask("destructible", $"发现 {_deadDestructibleIds.Count} 个已破坏物体");
    }

    
    
    
    private void ScanAndMarkInitiallyDeadDestructibles()
    {
        
        if (_scanScheduled)
        {
            Debug.Log("[Destructible] 扫描已调度，跳过重复调用");
            return;
        }
        _scanScheduled = true;

        
        var initManager = SceneInitManager.Instance;
        if (initManager != null)
        {
            initManager.EnqueueDelayedTask(() =>
            {
                
                NetService.Instance.StartCoroutine(ScanAndMarkInitiallyDeadDestructiblesAsync(20));
            }, 2.0f, "Destructible_Scan"); 
        }
        else
        {
            
            NetService.Instance.StartCoroutine(ScanAndMarkInitiallyDeadDestructiblesAsync(20));
        }
    }

    
    
    private void Client_ApplyDestructibleDead_Inner(uint id, Vector3 point, Vector3 normal)
    {
        if (_deadDestructibleIds.Contains(id)) return;
        _deadDestructibleIds.Add(id);

        var hs = FindDestructible(id);
        if (!hs) return;

        
        var br = hs.GetComponent<Breakable>();
        if (br)
            try
            {
                
                if (br.normalVisual) br.normalVisual.SetActive(false);
                if (br.dangerVisual) br.dangerVisual.SetActive(false);
                if (br.breakedVisual) br.breakedVisual.SetActive(true);

                
                if (br.mainCollider) br.mainCollider.SetActive(false);

                
                if (br.createExplosion)
                {
                    
                    var di = br.explosionDamageInfo;
                    di.fromCharacter = null;
                    LevelManager.Instance.ExplosionManager.CreateExplosion(
                        hs.transform.position, br.explosionRadius, di
                    );
                }
            }
            catch
            {
                
            }

        
        var half = hs.GetComponent<HalfObsticle>();
        if (half)
            try
            {
                half.Dead(new DamageInfo { damagePoint = point, damageNormal = normal });
            }
            catch
            {
            }

        
        var hv = hs.GetComponent<HurtVisual>();
        if (hv && hv.DeadFx) Object.Instantiate(hv.DeadFx, hs.transform.position, hs.transform.rotation);

        
        foreach (var c in hs.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    
    public void Client_ApplyDestructibleDead(NetDataReader r)
    {
        var id = r.GetUInt();
        var point = r.GetV3cm();
        var normal = r.GetDir();
        Client_ApplyDestructibleDead_Inner(id, point, normal);
    }


    
    public void Server_BroadcastDestructibleHurt(uint id, float newHealth, DamageInfo dmg)
    {
        if (!networkStarted || !IsServer) return;
        var msg = new Net.HybridNet.EnvironmentHurtEventMessage { ObjectId = (int)id, NewHealth = newHealth, HitPoint = dmg.damagePoint, HitNormal = dmg.damageNormal };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    public void Server_BroadcastDestructibleDead(uint id, DamageInfo dmg)
    {
        var msg = new Net.HybridNet.EnvironmentDeadEventMessage { ObjectId = (int)id, HitPoint = dmg.damagePoint, HitNormal = dmg.damageNormal };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    
    
    public void Client_ApplyDestructibleHurt(NetDataReader r)
    {
        var id = r.GetUInt();
        var curHealth = r.GetFloat();
        var point = r.GetV3cm();
        var normal = r.GetDir();

        
        if (_deadDestructibleIds.Contains(id)) return;

        
        if (curHealth <= 0f)
        {
            Client_ApplyDestructibleDead_Inner(id, point, normal);
            return;
        }

        var hs = FindDestructible(id);
        if (!hs) return;

        
        var hv = hs.GetComponent<HurtVisual>();
        if (hv && hv.HitFx) Object.Instantiate(hv.HitFx, point, Quaternion.LookRotation(normal));

        
        var br = hs.GetComponent<Breakable>();
        if (br)
            
            try
            {
                
                if (curHealth <= br.dangerHealth && !_dangerDestructibleIds.Contains(id))
                {
                    
                    if (br.normalVisual) br.normalVisual.SetActive(false);
                    if (br.dangerVisual) br.dangerVisual.SetActive(true);
                    if (br.dangerFx) Object.Instantiate(br.dangerFx, br.transform.position, br.transform.rotation);
                    _dangerDestructibleIds.Add(id);
                }
            }
            catch
            {
                
            }
    }

    
    
    
    public void BuildDestructibleIndex()
    {
        
        if (_deadDestructibleIds != null) _deadDestructibleIds.Clear();
        if (_dangerDestructibleIds != null) _dangerDestructibleIds.Clear();

        if (_serverDestructibles != null) _serverDestructibles.Clear();
        if (_clientDestructibles != null) _clientDestructibles.Clear();

        
        _scanScheduled = false;

        
        var cacheManager = Utils.GameObjectCacheManager.Instance;
        if (cacheManager != null)
        {
            Debug.Log("[Destructible] 使用 DestructibleCache 构建索引");
            cacheManager.Destructibles.RefreshCache();
        }

        
        
        HealthSimpleBase[] all = null;
        if (cacheManager != null)
        {
            
            all = cacheManager.Destructibles.GetAllDestructibles();
        }

        
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning("[Destructible] DestructibleCache 不可用，降级使用 FindObjectsOfType");
            all = Object.FindObjectsOfType<HealthSimpleBase>(true);
        }

        
        for (var i = 0; i < all.Length; i++)
        {
            var hs = all[i];
            if (!hs) continue;

            var tag = hs.GetComponent<NetDestructibleTag>();
            if (!tag) continue; 

            
            var id = ComputeStableIdForDestructible(hs);
            if (id == 0u)
                
                try
                {
                    id = NetDestructibleTag.ComputeStableId(hs.gameObject);
                }
                catch
                {
                }

            tag.id = id;

            
            RegisterDestructible(tag.id, hs);
        }

        Debug.Log($"[Destructible] 索引构建完成，共注册 {_serverDestructibles.Count + _clientDestructibles.Count} 个可破坏物");

        
        if (IsServer) 
            ScanAndMarkInitiallyDeadDestructibles();
    }
}