















using Duckov.UI;
using EscapeFromDuckovCoopMod.Net;  
using EscapeFromDuckovCoopMod.Utils;
using System.Collections;

namespace EscapeFromDuckovCoopMod;

public class SceneNet : MonoBehaviour
{
    public static SceneNet Instance;
    public string _sceneReadySidSent;
    public bool sceneVoteActive;
    public string sceneTargetId; 
    public string sceneCurtainGuid; 
    public bool sceneNotifyEvac;
    public bool sceneSaveToFile = true;

    public bool allowLocalSceneLoad;

    public bool sceneUseLocation;
    public string sceneLocationName;

    public bool localReady;

    
    public volatile bool _cliSceneGateReleased;
    public string _cliGateSid;
    public string _srvGateSid;
    public bool IsMapSelectionEntry;

    public readonly Dictionary<string, string> _cliLastSceneIdByPlayer = new();

    
    public readonly HashSet<string> _srvGateReadyPids = new();

    
    public readonly List<string> sceneParticipantIds = new();

    
    public readonly Dictionary<string, bool> sceneReady = new();

    
    public SceneVoteMessage.VoteStateData cachedVoteData = null;

    
    public int expiredVoteId = 0;

    private readonly Dictionary<string, string> _cliServerPidToLocal = new();
    private readonly Dictionary<string, string> _cliLocalPidToServer = new();
    private float _cliGateDeadline;

    public bool _srvSceneGateOpen; 
    private ModBehaviourF Service => ModBehaviourF.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private int port => Service?.port ?? 0;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;
    private Dictionary<string, PlayerStatus> clientPlayerStatuses => Service?.clientPlayerStatuses;

    private void ResetClientParticipantMappings()
    {
        _cliServerPidToLocal.Clear();
        _cliLocalPidToServer.Clear();
    }

    private void RegisterClientParticipantId(string serverPid, string localPid)
    {
        serverPid ??= string.Empty;
        localPid ??= string.Empty;
        _cliServerPidToLocal[serverPid] = localPid;
        _cliLocalPidToServer[localPid] = serverPid;
    }

    private string MapServerPidToLocal(string pid)
    {
        if (string.IsNullOrEmpty(pid)) return pid ?? string.Empty;
        return _cliServerPidToLocal.TryGetValue(pid, out var local) ? local : pid;
    }

    internal string NormalizeParticipantId(string pid) => MapServerPidToLocal(pid);

    private string ResolveClientAliasForServerPid(string serverPid)
    {
        if (string.IsNullOrEmpty(serverPid)) return string.Empty;

        if (localPlayerStatus != null && string.Equals(localPlayerStatus.EndPoint, serverPid, StringComparison.Ordinal))
            return localPlayerStatus.EndPoint;

        if (playerStatuses != null)
            foreach (var kv in playerStatuses)
            {
                var st = kv.Value;
                if (st == null) continue;
                if (!string.Equals(st.EndPoint, serverPid, StringComparison.Ordinal)) continue;

                return !string.IsNullOrEmpty(st.ClientReportedId) ? st.ClientReportedId : serverPid;
            }

        return serverPid;
    }

    private string ResolveLocalAliasFromServerPid(string serverPid, string aliasFromServer)
    {
        if (string.IsNullOrEmpty(serverPid)) return aliasFromServer ?? string.Empty;

        if (!string.IsNullOrEmpty(aliasFromServer)) return aliasFromServer;

        if (localPlayerStatus == null) return aliasFromServer ?? string.Empty;

        var me = localPlayerStatus.EndPoint ?? string.Empty;
        if (string.IsNullOrEmpty(me)) return aliasFromServer ?? string.Empty;

        if (string.Equals(serverPid, me, StringComparison.Ordinal)) return me;

        if (clientPlayerStatuses != null && clientPlayerStatuses.TryGetValue(serverPid, out var st) && st != null)
        {
            var sameName = !string.IsNullOrEmpty(st.PlayerName) &&
                           !string.IsNullOrEmpty(localPlayerStatus.PlayerName) &&
                           string.Equals(st.PlayerName, localPlayerStatus.PlayerName, StringComparison.Ordinal);

            var sameScene = !string.IsNullOrEmpty(st.SceneId) &&
                            !string.IsNullOrEmpty(localPlayerStatus.SceneId) &&
                            string.Equals(st.SceneId, localPlayerStatus.SceneId, StringComparison.Ordinal);

            if (sameName && sameScene) return me;

            if (!string.IsNullOrEmpty(localPlayerStatus.CustomFaceJson) &&
                !string.IsNullOrEmpty(st.CustomFaceJson) &&
                string.Equals(st.CustomFaceJson, localPlayerStatus.CustomFaceJson, StringComparison.Ordinal))
                return me;
        }

        return aliasFromServer ?? string.Empty;
    }

    public void Init()
    {
        Instance = this;
    }

    public void TrySendSceneReadyOnce()
    {
        if (!networkStarted) return;

        
        if (!LocalPlayerManager.Instance.ComputeIsInGame(out var sid) || string.IsNullOrEmpty(sid)) return;
        if (_sceneReadySidSent == sid) return; 

        var lm = LevelManager.Instance;
        var pos = lm && lm.MainCharacter ? lm.MainCharacter.transform.position : Vector3.zero;
        var rot = lm && lm.MainCharacter ? lm.MainCharacter.modelRoot.transform.rotation : Quaternion.identity;

        
        var sceneReadyMsg = new Net.HybridNet.SceneReadyMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? (IsServer ? $"Host:{port}" : "Client:Unknown"),
            SceneId = 0
        };
        Net.HybridNet.HybridNetCore.Send(sceneReadyMsg);

        _sceneReadySidSent = sid;

        
        SendPlayerAppearance();

        if (!IsServer)
        {
            Client_RequestSceneAISeeds(sid);
        }
    }

    private void Client_RequestSceneAISeeds(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId) || connectedPeer == null) return;

        var requestMsg = new Net.HybridNet.SceneAISeedRequestMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? "",
            SceneId = sceneId
        };
        Net.HybridNet.HybridNetCore.Send(requestMsg, connectedPeer);

        Debug.Log($"[SCENE] 客户端请求场景AI种子: sceneId={sceneId}");
    }

    
    
    
    public void SendPlayerAppearance()
    {
        if (!networkStarted) return;

        var faceJson = CustomFace.LoadLocalCustomFaceJson() ?? string.Empty;
        if (string.IsNullOrEmpty(faceJson)) return; 

        var appearanceMsg = new Net.HybridNet.PlayerAppearanceMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? (IsServer ? $"Host:{port}" : "Client:Unknown"),
            FaceJson = faceJson
        };
        Net.HybridNet.HybridNetCore.Send(appearanceMsg);
    }

    
    
    
    
    
    private void Server_BroadcastBeginSceneLoad()
    {
        if (Spectator.Instance._spectatorActive && Spectator.Instance._spectatorEndOnVotePending)
        {
            Spectator.Instance._spectatorEndOnVotePending = false;
            Spectator.Instance.EndSpectatorAndShowClosure();
        }

        
        sceneVoteActive = false;
        sceneParticipantIds.Clear();
        ResetClientParticipantMappings();
        sceneReady.Clear();
        localReady = false;

        
        if (this != null && gameObject != null) 
        {
            StartCoroutine(BroadcastAfterCleanupCoroutine());
        }
    }

    
    
    
    private IEnumerator BroadcastAfterCleanupCoroutine()
    {
        Debug.Log("[SCENE] ========== 开始场景切换清理流程 ==========");

        
        yield return null;

        
        Debug.Log("[SCENE] 清理游戏对象缓存...");
        if (Utils.GameObjectCacheManager.Instance != null)
        {
            Utils.GameObjectCacheManager.Instance.ClearAllCaches();
        }

        
        Debug.Log("[SCENE] 清理战利品数据...");
        if (LootManager.Instance != null)
        {
            LootManager.Instance.ClearCaches();
        }

        
        Debug.Log("[SCENE] 清空异步消息队列...");
        if (Utils.AsyncMessageQueue.Instance != null)
        {
            Utils.AsyncMessageQueue.Instance.ClearQueue();
        }

        
        yield return null;

        Debug.Log("[SCENE] ========== 清理完成，开始广播场景切换 ==========");

        
        var beginLoadMsg = new Net.HybridNet.SceneBeginLoadMessage
        {
            TargetSceneId = sceneTargetId ?? ""
        };
        Net.HybridNet.HybridNetCore.Send(beginLoadMsg);
        Debug.Log($"[SCENE] 已广播场景切换: {sceneTargetId}");

        
        allowLocalSceneLoad = true;
        var map = CoopTool.GetMapSelectionEntrylist(sceneTargetId);
        if (map != null && IsMapSelectionEntry)
        {
            IsMapSelectionEntry = false;
            allowLocalSceneLoad = false;
            SceneM.Call_NotifyEntryClicked_ByInvoke(MapSelectionView.Instance, map, null);
        }
        else
        {
            TryPerformSceneLoad_Local(sceneTargetId, sceneCurtainGuid, sceneNotifyEvac, sceneSaveToFile, sceneUseLocation, sceneLocationName);
        }

        Debug.Log("[SCENE] ========== 场景切换广播流程完成 ==========");
    }

    
    public void Server_OnSceneReadySet(NetPeer fromPeer, bool ready)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;

        
        var pid = fromPeer != null ? ModBehaviourF.Instance.GetPlayerId(fromPeer) : ModBehaviourF.Instance.GetPlayerId(null);

        if (!sceneVoteActive) return;
        if (!sceneReady.ContainsKey(pid)) return; 

        sceneReady[pid] = ready;

        
        var readySetMsg = new Net.HybridNet.SceneReadySetMessage
        {
            PlayerId = pid,
            IsReady = ready
        };
        Net.HybridNet.HybridNetCore.Send(readySetMsg);

        
        foreach (var id in sceneParticipantIds)
            if (!sceneReady.TryGetValue(id, out var r) || !r)
                return;

        
        Server_BroadcastBeginSceneLoad();
    }

    
    public void Client_OnSceneVoteStart(NetDataReader r)
    {
        
        if (!EnsureAvailable(r, 2))
        {
            Debug.LogWarning("[SCENE] vote: header too short");
            return;
        }

        var ver = r.GetByte(); 
        if (ver != 1 && ver != 2 && ver != 3)
        {
            Debug.LogWarning($"[SCENE] vote: unsupported ver={ver}");
            return;
        }

        if (!TryGetString(r, out sceneTargetId))
        {
            Debug.LogWarning("[SCENE] vote: bad sceneId");
            return;
        }

        if (!EnsureAvailable(r, 1))
        {
            Debug.LogWarning("[SCENE] vote: no flags");
            return;
        }

        var flags = r.GetByte();
        bool hasCurtain, useLoc, notifyEvac, saveToFile;
        PackFlag.UnpackFlags(flags, out hasCurtain, out useLoc, out notifyEvac, out saveToFile);

        string curtainGuid = null;
        if (hasCurtain)
            if (!TryGetString(r, out curtainGuid))
            {
                Debug.LogWarning("[SCENE] vote: bad curtain");
                return;
            }

        string locName = null;
        if (!TryGetString(r, out locName))
        {
            Debug.LogWarning("[SCENE] vote: bad location");
            return;
        }

        var hostSceneId = string.Empty;
        if (ver >= 2)
        {
            if (!TryGetString(r, out hostSceneId))
            {
                Debug.LogWarning("[SCENE] vote: bad hostSceneId");
                return;
            }

            hostSceneId = hostSceneId ?? string.Empty;
        }

        if (!EnsureAvailable(r, 4))
        {
            Debug.LogWarning("[SCENE] vote: no count");
            return;
        }

        var cnt = r.GetInt();
        if (cnt < 0 || cnt > 256)
        {
            Debug.LogWarning("[SCENE] vote: weird count");
            return;
        }

        sceneParticipantIds.Clear();
        ResetClientParticipantMappings();
        for (var i = 0; i < cnt; i++)
        {
            if (!TryGetString(r, out var pid))
            {
                Debug.LogWarning($"[SCENE] vote: bad pid[{i}]");
                return;
            }

            pid ??= string.Empty;
            string aliasFromServer = string.Empty;

            if (ver >= 3)
            {
                if (!TryGetString(r, out aliasFromServer))
                {
                    Debug.LogWarning($"[SCENE] vote: bad alias[{i}]");
                    return;
                }
            }

            var localPid = ResolveLocalAliasFromServerPid(pid, aliasFromServer);
            if (string.IsNullOrEmpty(localPid)) localPid = pid;

            RegisterClientParticipantId(pid, localPid);
            sceneParticipantIds.Add(localPid);
        }

        
        string mySceneId = null;
        LocalPlayerManager.Instance.ComputeIsInGame(out mySceneId);
        mySceneId = mySceneId ?? string.Empty;

        
        if (!string.IsNullOrEmpty(hostSceneId) && !string.IsNullOrEmpty(mySceneId))
            if (!string.Equals(hostSceneId, mySceneId, StringComparison.Ordinal))
            {
                Debug.Log($"[SCENE] vote: ignore (diff scene) host='{hostSceneId}' me='{mySceneId}'");
                return;
            }

        
        if (sceneParticipantIds.Count > 0 && localPlayerStatus != null)
        {
            var me = localPlayerStatus.EndPoint ?? string.Empty;
            if (!string.IsNullOrEmpty(me) && !sceneParticipantIds.Contains(me))
            {
                Debug.Log($"[SCENE] vote: ignore (not in participants) me='{me}'");
                var peerId = connectedPeer != null && connectedPeer.EndPoint != null
                    ? connectedPeer.EndPoint.ToString()
                    : string.Empty;
                Debug.Log($"[SCENE] vote: ignore (not in participants) local='{localPlayerStatus.EndPoint}' peer='{peerId}'");
                return;
            }
        }

        
        sceneCurtainGuid = curtainGuid;
        sceneUseLocation = useLoc;
        sceneNotifyEvac = notifyEvac;
        sceneSaveToFile = saveToFile;
        sceneLocationName = locName ?? "";

        sceneVoteActive = true;
        localReady = false;
        sceneReady.Clear();
        foreach (var pid in sceneParticipantIds) sceneReady[pid] = false;

        Debug.Log($"[SCENE] 收到投票 v{ver}: target='{sceneTargetId}', hostScene='{hostSceneId}', myScene='{mySceneId}', players={cnt}");

        
        
    }

    
    private void Client_OnSomeoneReadyChanged(NetDataReader r)
    {
        var pid = r.GetString();
        var rd = r.GetBool();
        var localPid = MapServerPidToLocal(pid);
        if (sceneReady.ContainsKey(localPid)) sceneReady[localPid] = rd;
    }

    public void Client_OnBeginSceneLoad(NetDataReader r)
    {
        if (!EnsureAvailable(r, 2))
        {
            Debug.LogWarning("[SCENE] begin: header too short");
            return;
        }

        var ver = r.GetByte();
        if (ver != 1)
        {
            Debug.LogWarning($"[SCENE] begin: unsupported ver={ver}");
            return;
        }

        if (!TryGetString(r, out var id))
        {
            Debug.LogWarning("[SCENE] begin: bad sceneId");
            return;
        }

        if (!EnsureAvailable(r, 1))
        {
            Debug.LogWarning("[SCENE] begin: no flags");
            return;
        }

        var flags = r.GetByte();
        bool hasCurtain, useLoc, notifyEvac, saveToFile;
        PackFlag.UnpackFlags(flags, out hasCurtain, out useLoc, out notifyEvac, out saveToFile);

        string curtainGuid = null;
        if (hasCurtain)
            if (!TryGetString(r, out curtainGuid))
            {
                Debug.LogWarning("[SCENE] begin: bad curtain");
                return;
            }

        if (!TryGetString(r, out var locName))
        {
            Debug.LogWarning("[SCENE] begin: bad locName");
            return;
        }

        
        sceneTargetId = id;
        sceneCurtainGuid = curtainGuid;
        sceneLocationName = locName;
        sceneNotifyEvac = notifyEvac;
        sceneSaveToFile = saveToFile;
        sceneUseLocation = useLoc;

        Debug.Log($"[SCENE] 客户端收到场景加载通知: targetId={sceneTargetId}, curtain={sceneCurtainGuid}, loc={sceneLocationName}");

        allowLocalSceneLoad = true;
        var map = CoopTool.GetMapSelectionEntrylist(sceneTargetId);
        if (map != null && sceneLocationName == "OnPointerClick")
        {
            IsMapSelectionEntry = false;
            allowLocalSceneLoad = false;
            SceneM.Call_NotifyEntryClicked_ByInvoke(MapSelectionView.Instance, map, null);
        }
        else
        {
            TryPerformSceneLoad_Local(sceneTargetId, sceneCurtainGuid, sceneNotifyEvac, sceneSaveToFile, sceneUseLocation, sceneLocationName);
        }

        sceneVoteActive = false;
        sceneParticipantIds.Clear();
        ResetClientParticipantMappings();
        sceneReady.Clear();
        localReady = false;
    }

    public void Client_SendReadySet(bool ready)
    {
        if (IsServer || connectedPeer == null) return;

        var readySetMsg = new Net.HybridNet.SceneReadySetMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? "",
            IsReady = ready
        };

        var service = ModBehaviourF.Instance;
        if (service != null && service.connectedPeer != null)
        {
            Net.HybridNet.HybridNetCore.Send(readySetMsg, service.connectedPeer);
        }

        
        if (sceneVoteActive && localPlayerStatus != null)
        {
            var me = localPlayerStatus.EndPoint ?? string.Empty;
            if (!string.IsNullOrEmpty(me) && sceneReady.ContainsKey(me))
                sceneReady[me] = ready;
        }
    }

    
    
    
    public void CancelVote()
    {
        if (!sceneVoteActive)
        {
            Debug.LogWarning("[SCENE] 没有正在进行的投票");
            return;
        }

        Debug.Log("[SCENE] 取消投票，重置场景触发器");

        
        if (IsServer && networkStarted && netManager != null)
        {
            
            SceneVoteMessage.Host_CancelVote();

            
            var cancelMsg = new Net.HybridNet.SceneCancelMessage { Reason = "vote_cancelled" };
            Net.HybridNet.HybridNetCore.Send(cancelMsg);
            Debug.Log("[SCENE] 服务器已广播取消投票消息（HybridNet）");
        }

        
        sceneVoteActive = false;
        sceneParticipantIds.Clear();
        ResetClientParticipantMappings();
        sceneReady.Clear();
        localReady = false;

        
        EscapeFromDuckovCoopMod.Utils.SceneTriggerResetter.ResetAllSceneTriggers();
    }

    
    
    
    public void Client_OnVoteCancelled()
    {
        if (IsServer)
        {
            Debug.LogWarning("[SCENE] 服务器不应该接收客户端的取消投票消息");
            return;
        }

        Debug.Log("[SCENE] 收到服务器取消投票通知，重置本地状态");

        
        sceneVoteActive = false;
        sceneParticipantIds.Clear();
        ResetClientParticipantMappings();
        sceneReady.Clear();
        localReady = false;

        
        EscapeFromDuckovCoopMod.Utils.SceneTriggerResetter.ResetAllSceneTriggers();
    }

    private void TryPerformSceneLoad_Local(string targetSceneId, string curtainGuid,
        bool notifyEvac, bool save,
        bool useLocation, string locationName)
    {
        try
        {
            var loader = SceneLoader.Instance;
            var launched = false; 

            

            
            
            IEnumerable<SceneLoaderProxy> sceneLoaders = GameObjectCacheManager.Instance != null
                ? GameObjectCacheManager.Instance.Environment.GetAllSceneLoaders()
                : FindObjectsOfType<SceneLoaderProxy>();

            foreach (var ii in sceneLoaders)
                try
                {
                    if (Traverse.Create(ii).Field<string>("sceneID").Value == targetSceneId)
                    {
                        ii.LoadScene();
                        launched = true;
                        Debug.Log($"[SCENE] Fallback via SceneLoaderProxy -> {targetSceneId}");
                        break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[SCENE] proxy check failed: " + e);
                }

            if (!launched) Debug.LogWarning($"[SCENE] Local load fallback failed: no proxy for '{targetSceneId}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SCENE] Local load failed: " + e);
        }
        finally
        {
            allowLocalSceneLoad = false;
            if (networkStarted)
            {
                if (IsServer) SendLocalPlayerStatus.Instance.SendPlayerStatusUpdate();
                else Send_ClientStatus.Instance.SendClientStatusUpdate();
            }
        }
    }

    public void Server_HandleSceneReady(NetPeer fromPeer, string playerId, string sceneId, Vector3 pos, Quaternion rot)
    {
        if (fromPeer != null) SceneM._srvPeerScene[fromPeer] = sceneId;

        
        foreach (var kv in SceneM._srvPeerScene)
        {
            var other = kv.Key;
            if (other == fromPeer) continue;
            if (kv.Value == sceneId)
            {
                
                var opos = Vector3.zero;
                var orot = Quaternion.identity;
                if (playerStatuses.TryGetValue(other, out var s) && s != null)
                {
                    opos = s.Position;
                    orot = s.Rotation;
                }

                
                var createMsg = new Net.HybridNet.RemoteCharacterCreateMessage
                {
                    PlayerId = playerStatuses[other].EndPoint,
                    SceneId = 0,
                };
                Net.HybridNet.HybridNetCore.Send(createMsg, fromPeer);

                
                if (!string.IsNullOrEmpty(s?.CustomFaceJson))
                {
                    var appearanceMsg = new Net.HybridNet.PlayerAppearanceMessage
                    {
                        PlayerId = s.EndPoint,
                        FaceJson = s.CustomFaceJson
                    };
                    Net.HybridNet.HybridNetCore.Send(appearanceMsg, fromPeer);
                }
            }
        }

        
        foreach (var kv in SceneM._srvPeerScene)
        {
            var other = kv.Key;
            if (other == fromPeer) continue;
            if (kv.Value == sceneId)
            {
                
                var createMsg = new Net.HybridNet.RemoteCharacterCreateMessage
                {
                    PlayerId = playerId,
                    SceneId = 0,
                };
                Net.HybridNet.HybridNetCore.Send(createMsg, other);
            }
        }

        
        foreach (var kv in SceneM._srvPeerScene)
        {
            var other = kv.Key;
            if (other == fromPeer) continue;
            if (kv.Value != sceneId)
            {
                
                var despawnMsg1 = new Net.HybridNet.RemoteCharacterDespawnMessage
                {
                    PlayerId = playerId
                };
                Net.HybridNet.HybridNetCore.Send(despawnMsg1, other);

                var despawnMsg2 = new Net.HybridNet.RemoteCharacterDespawnMessage
                {
                    PlayerId = playerStatuses[other].EndPoint
                };
                Net.HybridNet.HybridNetCore.Send(despawnMsg2, fromPeer);
            }
        }

        
        if (!remoteCharacters.TryGetValue(fromPeer, out var exists) || exists == null)
        {
            
            var face = fromPeer != null && playerStatuses.TryGetValue(fromPeer, out var s2) && !string.IsNullOrEmpty(s2.CustomFaceJson)
                ? s2.CustomFaceJson
                : string.Empty;
            CreateRemoteCharacter.CreateRemoteCharacterAsync(fromPeer, pos, rot, face).Forget();
        }
    }

    public void Host_BeginSceneVote_Simple(string targetSceneId, string curtainGuid,
        bool notifyEvac, bool saveToFile,
        bool useLocation, string locationName)
    {
        sceneTargetId = targetSceneId ?? "";
        sceneCurtainGuid = string.IsNullOrEmpty(curtainGuid) ? null : curtainGuid;
        sceneNotifyEvac = notifyEvac;
        sceneSaveToFile = saveToFile;
        sceneUseLocation = useLocation;
        sceneLocationName = locationName ?? "";

        
        _srvSceneGateOpen = false;
        _srvGateReadyPids.Clear();
        Debug.Log("[GATE] 投票开始，重置场景门控状态");

        
        SceneVoteMessage.Host_StartVote(targetSceneId, curtainGuid, notifyEvac, saveToFile, useLocation, locationName);
        Debug.Log($"[SCENE] 投票开始 (JSON): target='{targetSceneId}', loc='{locationName}'");

        
        
        sceneParticipantIds.Clear();
        sceneParticipantIds.AddRange(CoopTool.BuildParticipantIds_Server());

        sceneVoteActive = true;
        localReady = false;
        sceneReady.Clear();
        foreach (var pid in sceneParticipantIds) sceneReady[pid] = false;

        
        

        
        
    }

    public void Client_RequestBeginSceneVote(
        string targetId, string curtainGuid,
        bool notifyEvac, bool saveToFile,
        bool useLocation, string locationName)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;

        
        var voteReqMsg = new Net.HybridNet.SceneVoteRequestMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? "",
            TargetSceneId = targetId
        };
        Net.HybridNet.HybridNetCore.Send(voteReqMsg, connectedPeer);
    }

    public UniTask AppendSceneGate(UniTask original)
    {
        return Internal();

        async UniTask Internal()
        {
            
            await original;

            try
            {
                if (!networkStarted) return;

                
                

                await Client_SceneGateAsync();
            }
            catch (Exception e)
            {
                Debug.LogError("[SCENE-GATE] " + e);
            }
        }
    }

    public async UniTask Client_SceneGateAsync()
    {
        if (!networkStarted || IsServer) return;

        
        var connectDeadline = Time.realtimeSinceStartup + 8f;
        while (connectedPeer == null && Time.realtimeSinceStartup < connectDeadline)
            await UniTask.Delay(100);

        
        _cliSceneGateReleased = false;

        var sid = _cliGateSid;
        if (string.IsNullOrEmpty(sid))
            sid = TryGuessActiveSceneId();
        _cliGateSid = sid;

        
        if (connectedPeer != null)
        {
            var myPid = localPlayerStatus != null ? localPlayerStatus.EndPoint : "";
            
            var gateReadyMsg = new Net.HybridNet.SceneGateReadyMessage
            {
                PlayerId = myPid,
                SceneId = sid ?? ""
            };
            Net.HybridNet.HybridNetCore.Send(gateReadyMsg, connectedPeer);
            Debug.Log($"[GATE] 客户端举手：pid={myPid}, sid={sid}");
        }
        else
        {
            Debug.LogWarning("[GATE] 客户端无法举手：connectedPeer 为空");
        }

        
        var retryDeadline = Time.realtimeSinceStartup + 5f;
        while (connectedPeer == null && Time.realtimeSinceStartup < retryDeadline)
        {
            await UniTask.Delay(200);
            if (connectedPeer != null)
            {
                
                var gateReadyRetryMsg = new Net.HybridNet.SceneGateReadyMessage
                {
                    PlayerId = localPlayerStatus != null ? localPlayerStatus.EndPoint : "",
                    SceneId = sid ?? ""
                };
                Net.HybridNet.HybridNetCore.Send(gateReadyRetryMsg, connectedPeer);
                break;
            }
        }

        
        
        _cliGateDeadline = Time.realtimeSinceStartup + 1f;

        Debug.Log($"[GATE] 客户端等待主机放行... (超时: 150秒)");

        while (!_cliSceneGateReleased && Time.realtimeSinceStartup < _cliGateDeadline)
        {
            try
            {
                SceneLoader.LoadingComment = CoopLocalization.Get("scene.waitingForHost");
            }
            catch
            {
            }

            await UniTask.Delay(100);
        }

        if (!_cliSceneGateReleased)
        {
            Debug.LogWarning("[GATE] 客户端等待超时（150秒），强制开始加载。主机可能崩溃或网络异常。");
        }
        else
        {
            Debug.Log("[GATE] 客户端收到主机放行，开始加载场景");
        }


        
        try
        {
            SceneLoader.LoadingComment = CoopLocalization.Get("scene.hostReady");
        }
        catch
        {
        }

        
        if (ModBehaviourF.Instance != null && !ModBehaviourF.Instance.IsServer)
        {
            Debug.Log("[AUTO_RECONNECT] 场景门控完成，触发自动重连检查");
            ModBehaviourF.Instance.TryAutoReconnect();
        }
    }

    
    public async UniTask Server_SceneGateAsync()
    {
        if (!IsServer || !networkStarted) return;

        _srvGateSid = TryGuessActiveSceneId();

        
        
        _srvSceneGateOpen = true;

        Debug.Log($"[GATE] 主机场景加载完成，开始放行客户端。已举手: {_srvGateReadyPids.Count} 人");
        Debug.Log($"[GATE] _srvGateReadyPids: [{string.Join(", ", _srvGateReadyPids)}]");
        Debug.Log($"[GATE] playerStatuses 数量: {(playerStatuses != null ? playerStatuses.Count : 0)}");

        
        int releasedCount = 0;
        if (playerStatuses != null && playerStatuses.Count > 0)
        {
            foreach (var kv in playerStatuses)
            {
                var peer = kv.Key;
                var st = kv.Value;
                if (peer == null || st == null)
                {
                    Debug.LogWarning($"[GATE] 跳过空的 peer 或 status");
                    continue;
                }

                var peerAddr = peer.EndPoint != null ? peer.EndPoint.ToString() : "Unknown";
                Debug.Log($"[GATE] 检查客户端: EndPoint={st.EndPoint}, PeerAddr={peerAddr}, 是否举手: {_srvGateReadyPids.Contains(st.EndPoint)}");

                if (_srvGateReadyPids.Contains(st.EndPoint))
                {
                    Server_SendGateRelease(peer, _srvGateSid);
                    Debug.Log($"[GATE] ✅ 放行客户端: {st.EndPoint}");
                    releasedCount++;
                }
            }
        }
        else
        {
            Debug.LogWarning($"[GATE] playerStatuses 为空或数量为 0！");
        }

        Debug.Log($"[GATE] 放行完成，共放行 {releasedCount} 个客户端");

        
        await UniTask.Yield(); 
    }

    private void Server_SendGateRelease(NetPeer peer, string sid)
    {
        if (peer == null) return;
        
        var gateReleaseMsg = new Net.HybridNet.SceneGateReleaseMessage
        {
            SceneId = sid ?? ""
        };
        Net.HybridNet.HybridNetCore.Send(gateReleaseMsg, peer);

        
        ModBehaviourF.Instance.StartCoroutine(Server_SendLootFullSyncDelayed(peer));
    }

    
    
    
    
    private System.Collections.IEnumerator Server_SendLootFullSyncDelayed(NetPeer peer)
    {
        
        yield return null;

        
        
        Debug.Log($"[GATE] 战利品全量同步已禁用（避免大型地图网络IO阻塞） → {peer.EndPoint}");
        Debug.Log($"[GATE] 战利品将通过增量同步（玩家交互时）自动同步");

        yield break;

        
    }


    private string TryGuessActiveSceneId()
    {
        return sceneTargetId;
    }

    
    public static bool TryGetString(NetDataReader r, out string s)
    {
        try
        {
            s = r.GetString();
            return true;
        }
        catch
        {
            s = null;
            return false;
        }
    }

    public static bool EnsureAvailable(NetDataReader r, int need)
    {
        return r.AvailableBytes >= need;
    }
}