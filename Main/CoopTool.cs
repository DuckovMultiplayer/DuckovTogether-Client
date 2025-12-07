















using Duckov.UI;
using ItemStatsSystem;
using Object = UnityEngine.Object;
using EscapeFromDuckovCoopMod.Net;  

namespace EscapeFromDuckovCoopMod;

public static class CoopTool
{
    
    public static bool _cliSelfHpPending;

    public static float _cliSelfHpMax, _cliSelfHpCur;

    public static readonly Dictionary<string, List<(int weaponTypeId, int buffId)>> _cliPendingProxyBuffs = new();

    
    public static readonly Dictionary<string, (float max, float cur)> _cliPendingRemoteHp = new();

    private static NetService Service
    {
        get
        {
            var svc = NetService.Instance;
            if (svc == null)
            {
                svc = Object.FindObjectOfType<NetService>();
                if (svc != null) NetService.Instance = svc;
            }

            return svc;
        }
    }

    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager NetManager => Service?.netManager;
    private static NetDataWriter Writer => Service?.writer;
    private static NetPeer ConnectedPeer => Service?.connectedPeer;

    private static Dictionary<NetPeer, GameObject> RemoteCharacters => Service?.remoteCharacters;
    private static Dictionary<NetPeer, PlayerStatus> PlayerStatuses => Service?.playerStatuses;
    private static Dictionary<string, GameObject> ClientRemoteCharacters => Service?.clientRemoteCharacters;
    private static int Port => Service != null ? Service.port : 0;

    public static void Init()
    {
        
        _ = Service;
    }


    public static void SafeKillItemAgent(Item item)
    {
        if (item == null) return;
        try
        {
            var ag = item.ActiveAgent;
            if (ag != null && ag.gameObject != null)
                Object.Destroy(ag.gameObject);
        }
        catch
        {
        }

        try
        {
            item.Detach();
        }
        catch
        {
        }
    }

    
    public static void ClearWeaponSlot(CharacterModel model, HandheldSocketTypes socket)
    {
        COOPManager.ChangeWeaponModel(model, null, socket);
    }

    
    public static HandheldSocketTypes ResolveSocketOrDefault(int slotHash)
    {
        var socket = (HandheldSocketTypes)slotHash;
        if (socket != HandheldSocketTypes.normalHandheld &&
            socket != HandheldSocketTypes.meleeWeapon &&
            socket != HandheldSocketTypes.leftHandSocket)
            socket = HandheldSocketTypes.normalHandheld; 
        return socket;
    }

    public static void TryPlayShootAnim(string shooterId)
    {
        
        if (NetService.Instance.IsSelfId(shooterId)) return;

        var remoteCharacters = ClientRemoteCharacters;
        if (remoteCharacters == null) return;

        if (!remoteCharacters.TryGetValue(shooterId, out var shooterGo) || !shooterGo) return;

        var animCtrl = shooterGo.GetComponent<CharacterAnimationControl_MagicBlend>();
        if (animCtrl && animCtrl.animator) animCtrl.OnAttack();
    }

    public static bool TryGetProjectilePrefab(int weaponTypeId, out Projectile pfb)
    {
        return LocalPlayerManager.Instance._projCacheByWeaponType.TryGetValue(weaponTypeId, out pfb);
    }


    public static void BroadcastReliable(NetDataWriter w)
    {
        Net.HybridNet.HybridNetBroadcast.BroadcastLegacy(w, DeliveryMethod.ReliableOrdered);
    }

    public static void SendReliable(NetDataWriter w)
    {
        Net.HybridNet.HybridNetBroadcast.BroadcastLegacy(w, DeliveryMethod.ReliableOrdered);
    }

    public static void SendBroadcastDiscovery()
    {
        if (IsServer) return;

        var service = Service;
        if (service == null) return;

        var manager = service.netManager;
        if (manager == null || !manager.IsRunning) return;

        var writer = service.writer;
        writer.Reset();
        writer.Put("DISCOVER_REQUEST");
        manager.SendUnconnectedMessage(writer, "255.255.255.255", service.port);
    }

    public static MapSelectionEntry GetMapSelectionEntrylist(string SceneID)
    {
        const string keyword = "MapSelectionEntry";

        var trs = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var gos = trs
            .Select(t => t.gameObject)
            .Where(go => go.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        foreach (var i in gos)
            try
            {
                var map = i.GetComponentInChildren<MapSelectionEntry>();
                if (map != null)
                    if (map.SceneID == SceneID)
                        return map;
            }
            catch
            {
            }

        return null;
    }

    private static string CleanName(string n)
    {
        if (string.IsNullOrEmpty(n)) return string.Empty;
        if (n.EndsWith("(Clone)", StringComparison.Ordinal)) n = n.Substring(0, n.Length - "(Clone)".Length);
        return n.Trim();
    }

    private static string TypeNameOf(Grenade g)
    {
        return g ? g.GetType().FullName : string.Empty;
    }

    public static void CacheGrenadePrefab(int typeId, Grenade prefab)
    {
        if (!prefab) return;
        COOPManager.GrenadeM.prefabByTypeId[typeId] = prefab;
    }


    public static bool TryResolvePrefab(int typeId, string _, string __, out Grenade prefab)
    {
        prefab = null;
        if (COOPManager.GrenadeM.prefabByTypeId.TryGetValue(typeId, out var p) && p)
        {
            prefab = p;
            return true;
        }

        return false;
    }


    public static CharacterMainControl TryGetRemoteCharacterForPeer(NetPeer peer)
    {
        var remotes = RemoteCharacters;
        if (remotes != null && remotes.TryGetValue(peer, out var remoteObj) && remoteObj)
        {
            var cm = remoteObj.GetComponent<CharacterMainControl>().characterModel;
            if (cm != null) return cm.characterMainControl;
        }

        return null;
    }

    
    public static bool IsSelfDR(DamageReceiver dr, CharacterMainControl attacker)
    {
        if (!dr || !attacker) return false;
        var owner = dr.GetComponentInParent<CharacterMainControl>(true);
        return owner == attacker;
    }

    
    public static bool IsCharacterDR(DamageReceiver dr)
    {
        return dr && dr.GetComponentInParent<CharacterMainControl>(true) != null;
    }


    public static void Client_ApplyPendingSelfIfReady()
    {
        if (!_cliSelfHpPending) return;
        var main = CharacterMainControl.Main;
        if (!main) return;

        var h = main.GetComponentInChildren<Health>(true);
        var cmc = main.GetComponent<CharacterMainControl>();
        if (!h) return;

        try
        {
            h.autoInit = false;
        }
        catch
        {
        } 

        HealthTool.BindHealthToCharacter(h, cmc);
        HealthM.Instance.ForceSetHealth(h, _cliSelfHpMax, _cliSelfHpCur);

        
        LocalPlayerManager.Instance.Client_EnsureSelfDeathEvent(h, cmc);

        _cliSelfHpPending = false;
    }

    public static void Client_ApplyPendingRemoteIfAny(string playerId, GameObject go)
    {
        if (string.IsNullOrEmpty(playerId) || !go) return;
        if (!_cliPendingRemoteHp.TryGetValue(playerId, out var snap)) return;

        var cmc = go.GetComponent<CharacterMainControl>();
        var h = cmc.Health;

        if (!h) return;

        try
        {
            h.autoInit = false;
        }
        catch
        {
        }

        HealthTool.BindHealthToCharacter(h, cmc);

        var applyMax = snap.max > 0f ? snap.max : h.MaxHealth > 0f ? h.MaxHealth : 40f;
        var applyCur = snap.cur > 0f ? snap.cur : applyMax;

        HealthM.Instance.ForceSetHealth(h, applyMax, applyCur);
        _cliPendingRemoteHp.Remove(playerId);


        if (_cliPendingProxyBuffs.TryGetValue(playerId, out var pendings) && pendings != null && pendings.Count > 0)
        {
            if (cmc)
                foreach (var (weaponTypeId, buffId) in pendings)
                    COOPManager.ResolveBuffAsync(weaponTypeId, buffId)
                        .ContinueWith(b =>
                        {
                            if (b != null && cmc) cmc.AddBuff(b, null, weaponTypeId);
                        })
                        .Forget();

            _cliPendingProxyBuffs.Remove(playerId);
        }
    }

    public static List<string> BuildParticipantIds_Server()
    {
        var list = new List<string>();

        
        string hostSceneId = null;
        LocalPlayerManager.Instance.ComputeIsInGame(out hostSceneId); 

        
        var hostPid = NetService.Instance.GetPlayerId(null);
        if (!string.IsNullOrEmpty(hostPid)) list.Add(hostPid);

        
        var statuses = PlayerStatuses;
        if (statuses == null) return list;

        foreach (var kv in statuses)
        {
            var peer = kv.Key;
            if (peer == null) continue;

            
            string peerScene = null;
            if (!SceneM._srvPeerScene.TryGetValue(peer, out peerScene))
                peerScene = kv.Value?.SceneId;

            if (!string.IsNullOrEmpty(hostSceneId) && !string.IsNullOrEmpty(peerScene))
            {
                if (peerScene == hostSceneId)
                {
                    var pid = NetService.Instance.GetPlayerId(peer);
                    if (!string.IsNullOrEmpty(pid)) list.Add(pid);
                }
            }
            else
            {
                
                var pid = NetService.Instance.GetPlayerId(peer);
                if (!string.IsNullOrEmpty(pid)) list.Add(pid);
            }
        }

        return list;
    }
}