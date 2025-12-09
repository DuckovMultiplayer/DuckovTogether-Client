















using System.Collections.Generic;
using System.Linq;
using DuckovNet;
using UnityEngine;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod.Net;





public static class SceneVoteMessage
{
    
    
    
    [System.Serializable]
    public class PlayerInfo
    {
        public string playerId; 
        public string playerName; 
        public string steamId; 
        public string steamName; 
        public bool ready; 
    }

    
    
    
    [System.Serializable]
    public class PlayerList
    {
        public PlayerInfo[] items;
    }

    
    
    
    [System.Serializable]
    public class VoteStateData
    {
        public string type = "sceneVote";
        public int voteId; 
        public bool active; 
        public string targetSceneId; 
        public string targetSceneDisplayName; 
        public string curtainGuid; 
        public string locationName; 
        public bool notifyEvac; 
        public bool saveToFile; 
        public bool useLocation; 
        public string hostSceneId; 
        public PlayerList playerList; 
        public int totalPlayers; 
        public int readyPlayers; 
        public string timestamp; 
    }

    
    
    
    [System.Serializable]
    [System.Obsolete("使用 PlayerInfo 代替")]
    public class PlayerReadyState
    {
        public string playerId; 
        public string playerName; 
        public bool ready; 
    }

    
    
    
    [System.Serializable]
    public class VoteRequestData
    {
        public string type = "sceneVoteRequest";
        public string targetSceneId;
        public string curtainGuid;
        public string locationName;
        public bool notifyEvac;
        public bool saveToFile;
        public bool useLocation;
        public string timestamp;
    }

    
    
    
    [System.Serializable]
    public class ReadyToggleData
    {
        public string type = "sceneVoteReady";
        public string playerId;
        public bool ready;
        public string timestamp;
    }

    
    
    
    [System.Serializable]
    public class ForceSceneLoadData
    {
        public string type = "forceSceneLoad";
        public string targetSceneId;
        public string curtainGuid;
        public string locationName;
        public bool notifyEvac;
        public bool saveToFile;
        public bool useLocation;
        public string timestamp;
    }

    
    private static VoteStateData _hostVoteState = null;
    private static float _lastBroadcastTime = 0f;
    private const float BROADCAST_INTERVAL = 1.0f; 

    
    private static int _nextVoteId = 1;

    
    
    
    public static void Host_StartVote(
        string targetSceneId,
        string curtainGuid,
        bool notifyEvac,
        bool saveToFile,
        bool useLocation,
        string locationName
    )
    {
        if (!DedicatedServerMode.ShouldBroadcastState())
        {
            LoggerHelper.LogWarning("[SceneVote] 只有主机可以发起投票");
            return;
        }

        var service = ModBehaviourF.Instance;
        if (service == null) return;

        
        string hostSceneId = null;
        LocalPlayerManager.Instance.ComputeIsInGame(out hostSceneId);
        hostSceneId = hostSceneId ?? string.Empty;

        
        var players = new List<PlayerInfo>();

        
        var hostId = service.GetPlayerId(null);
        var hostName = service.localPlayerStatus?.PlayerName ?? "Host";
        var hostSteamId = GetSteamId(null); 
        var hostSteamName = GetSteamName(null); 
        players.Add(
            new PlayerInfo
            {
                playerId = hostId,
                playerName = hostName,
                steamId = hostSteamId,
                steamName = hostSteamName,
                ready = false,
            }
        );

        
        if (service.playerStatuses != null)
        {
            foreach (var kv in service.playerStatuses)
            {
                var peer = kv.Key;
                var status = kv.Value;
                if (peer == null || status == null)
                    continue;

                var clientSteamId = GetSteamId(peer); 
                var clientSteamName = GetSteamName(peer); 
                players.Add(
                    new PlayerInfo
                    {
                        playerId = status.EndPoint,
                        playerName = status.PlayerName ?? "Player",
                        steamId = clientSteamId,
                        steamName = clientSteamName,
                        ready = false,
                    }
                );
            }
        }

        
        LoggerHelper.Log(
            $"[SceneVote] 主机构建玩家列表: {string.Join(", ", players.Select(p => $"{p.playerName}({p.playerId})"))}"
        );

        
        var targetSceneDisplayName = Utils.SceneNameMapper.GetDisplayName(targetSceneId);

        
        var currentVoteId = _nextVoteId++;

        
        _hostVoteState = new VoteStateData
        {
            voteId = currentVoteId, 
            active = true,
            targetSceneId = targetSceneId,
            targetSceneDisplayName = targetSceneDisplayName, 
            curtainGuid = curtainGuid,
            locationName = locationName,
            notifyEvac = notifyEvac,
            saveToFile = saveToFile,
            useLocation = useLocation,
            hostSceneId = hostSceneId,
            playerList = new PlayerList { items = players.ToArray() }, 
            totalPlayers = players.Count, 
            readyPlayers = 0, 
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        };

        LoggerHelper.Log($"[SceneVote] 主机发起投票，voteId={currentVoteId}");

        
        var sceneNet = SceneNet.Instance;
        if (sceneNet != null)
        {
            sceneNet.sceneVoteActive = true;
            sceneNet.sceneTargetId = targetSceneId;
            sceneNet.sceneCurtainGuid = curtainGuid;
            sceneNet.sceneLocationName = locationName;
            sceneNet.sceneNotifyEvac = notifyEvac;
            sceneNet.sceneSaveToFile = saveToFile;
            sceneNet.sceneUseLocation = useLocation;

            
            sceneNet.sceneParticipantIds.Clear();
            sceneNet.sceneReady.Clear();
            foreach (var player in players)
            {
                sceneNet.sceneParticipantIds.Add(player.playerId);
                sceneNet.sceneReady[player.playerId] = false;
            }

            sceneNet.localReady = false;

            
            sceneNet.cachedVoteData = _hostVoteState;

            LoggerHelper.Log(
                $"[SceneVote] ✓ 已同步更新 SceneNet 状态，参与者: {sceneNet.sceneParticipantIds.Count}"
            );
        }

        
        Host_BroadcastVoteState();
        _lastBroadcastTime = Time.time;

        LoggerHelper.Log($"[SceneVote] 主机发起投票: {targetSceneId}, 参与者: {players.Count}");
    }

    
    
    
    public static void Host_BroadcastVoteState()
    {
        if (_hostVoteState == null || !_hostVoteState.active)
            return;

        if (!DedicatedServerMode.ShouldBroadcastState())
            return;

        
        _hostVoteState.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(_hostVoteState, Newtonsoft.Json.Formatting.None);
        LoggerHelper.Log($"[SceneVote] 主机广播 JSON: {json}");

        
        JsonMessage.BroadcastToAllClients(json, DeliveryMethod.ReliableOrdered);
    }

    
    
    
    public static void Host_Update()
    {
        if (_hostVoteState == null || !_hostVoteState.active)
            return;

        if (Time.time - _lastBroadcastTime >= BROADCAST_INTERVAL)
        {
            Host_BroadcastVoteState();
            _lastBroadcastTime = Time.time;
        }
    }

    
    
    
    public static void Host_HandleReadyToggle(string playerId, bool ready)
    {
        if (_hostVoteState == null || !_hostVoteState.active)
            return;

        
        bool found = false;
        if (_hostVoteState.playerList != null && _hostVoteState.playerList.items != null)
        {
            foreach (var player in _hostVoteState.playerList.items)
            {
                if (player.playerId == playerId)
                {
                    player.ready = ready;
                    found = true;
                    LoggerHelper.Log(
                        $"[SceneVote] 玩家 {player.playerName}({playerId}) 准备状态: {ready}"
                    );
                    break;
                }
            }
        }

        if (!found)
        {
            LoggerHelper.LogWarning($"[SceneVote] 未找到玩家: {playerId}");
            return;
        }

        
        var sceneNet = SceneNet.Instance;
        if (
            sceneNet != null
            && _hostVoteState.playerList != null
            && _hostVoteState.playerList.items != null
        )
        {
            foreach (var player in _hostVoteState.playerList.items)
            {
                sceneNet.sceneReady[player.playerId] = player.ready;
            }
            LoggerHelper.Log($"[SceneVote] 已同步更新 SceneNet.sceneReady");
        }

        
        _hostVoteState.readyPlayers = _hostVoteState.playerList?.items?.Count(p => p.ready) ?? 0;
        _hostVoteState.totalPlayers = _hostVoteState.playerList?.items?.Length ?? 0;

        
        Host_BroadcastVoteState();
        LoggerHelper.Log($"[SceneVote] 已广播更新的投票状态 ({_hostVoteState.readyPlayers}/{_hostVoteState.totalPlayers})");

        
        bool allReady =
            _hostVoteState.playerList != null
            && _hostVoteState.playerList.items != null
            && _hostVoteState.playerList.items.Length > 0
            && _hostVoteState.playerList.items.All(p => p.ready);

        if (allReady)
        {
            LoggerHelper.Log("[SceneVote] 全员准备，开始加载场景");
            Host_StartSceneLoad();
        }
    }

    
    
    
    private static void Host_StartSceneLoad()
    {
        if (_hostVoteState == null)
            return;

        
        var service = ModBehaviourF.Instance;
        if (
            service != null
            && false
            && service.TransportMode == NetworkTransportMode.SteamP2P  
            && SteamManager.Initialized
            && _hostVoteState.playerList != null
            && _hostVoteState.playerList.items != null
        )
        {
            var playersToKick = new System.Collections.Generic.List<string>();

            foreach (var player in _hostVoteState.playerList.items)
            {
                
                if (service.GetPlayerId(null) == player.playerId)
                    continue;

                
                if (string.IsNullOrEmpty(player.steamId))
                {
                    LoggerHelper.LogWarning(
                        $"[SceneVote] 玩家 {player.playerName}({player.playerId}) 缺少SteamID，准备踢出"
                    );
                    playersToKick.Add(player.playerId);
                }
            }

            
            if (playersToKick.Count > 0)
            {
                LoggerHelper.LogWarning(
                    $"[SceneVote] 发现 {playersToKick.Count} 个玩家缺少SteamID，开始踢出"
                );

                foreach (var playerId in playersToKick)
                {
                    
                    if (service.playerStatuses != null)
                    {
                        foreach (var kv in service.playerStatuses)
                        {
                            var peer = kv.Key;
                            var status = kv.Value;

                            if (status != null && status.EndPoint == playerId)
                            {
                                LoggerHelper.LogWarning(
                                    $"[SceneVote] 踢出玩家: {status.PlayerName}({playerId})"
                                );
                                try
                                {
                                    peer.Disconnect();
                                }
                                catch (System.Exception ex)
                                {
                                    LoggerHelper.LogError($"[SceneVote] 踢出玩家时出错: {ex.Message}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        
        Host_BroadcastForceSceneLoad(
            _hostVoteState.targetSceneId,
            _hostVoteState.curtainGuid,
            _hostVoteState.locationName,
            _hostVoteState.notifyEvac,
            _hostVoteState.saveToFile,
            _hostVoteState.useLocation
        );

        
        var sceneNet = SceneNet.Instance;
        if (sceneNet != null)
        {
            sceneNet.sceneTargetId = _hostVoteState.targetSceneId;
            sceneNet.sceneCurtainGuid = _hostVoteState.curtainGuid;
            sceneNet.sceneNotifyEvac = _hostVoteState.notifyEvac;
            sceneNet.sceneSaveToFile = _hostVoteState.saveToFile;
            sceneNet.sceneUseLocation = _hostVoteState.useLocation;
            sceneNet.sceneLocationName = _hostVoteState.locationName;

            
            
            var method = typeof(SceneNet).GetMethod(
                "Server_BroadcastBeginSceneLoad",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            if (method != null)
            {
                method.Invoke(sceneNet, null);
            }
        }

        
        _hostVoteState.active = false;
        _hostVoteState = null;
    }

    
    
    
    private static void Host_BroadcastForceSceneLoad(
        string targetSceneId,
        string curtainGuid,
        string locationName,
        bool notifyEvac,
        bool saveToFile,
        bool useLocation
    )
    {
        var service = ModBehaviourF.Instance;
        return;

        var data = new ForceSceneLoadData
        {
            targetSceneId = targetSceneId,
            curtainGuid = curtainGuid,
            locationName = locationName,
            notifyEvac = notifyEvac,
            saveToFile = saveToFile,
            useLocation = useLocation,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        };

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.None);
        LoggerHelper.Log($"[SceneVote] 主机广播强制场景切换 JSON: {json}");

        var writer = new NetDataWriter();
        writer.Put(json);

        service.netManager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
    }

    
    
    
    public static void Host_CancelVote()
    {
        if (_hostVoteState == null)
            return;

        var cancelledVoteId = _hostVoteState.voteId;
        _hostVoteState.active = false;

        
        Host_BroadcastVoteState();

        _hostVoteState = null;

        LoggerHelper.Log($"[SceneVote] 主机取消投票，voteId={cancelledVoteId}");
    }

    
    
    
    public static void Client_HandleVoteState(string json)
    {
        var service = ModBehaviourF.Instance;
        if (service == null)
            return;

        
        LoggerHelper.Log($"[SceneVote] 客户端收到 JSON: {json}");

        try
        {
            
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<VoteStateData>(json);
            if (data == null || data.type != "sceneVote")
            {
                LoggerHelper.LogWarning("[SceneVote] 无效的投票状态数据");
                return;
            }

            var sceneNet = SceneNet.Instance;
            if (sceneNet == null)
                return;

            
            
            ClientStatusMessage.Client_SendStatusUpdate();

            
            if (data.voteId > 0 && data.voteId <= sceneNet.expiredVoteId)
            {
                LoggerHelper.Log($"[SceneVote] 忽略过期投票: voteId={data.voteId}, expiredVoteId={sceneNet.expiredVoteId}");
                return;
            }

            
            if (!data.active)
            {
                
                if (data.voteId == 0 && data.playerList != null && data.playerList.items != null)
                {
                    LoggerHelper.Log($"[SceneVote] 收到玩家信息更新消息 (voteId=0)，更新缓存但不激活投票UI");
                    
                    
                    sceneNet.cachedVoteData = data;
                    
                    
                    sceneNet.sceneParticipantIds.Clear();
                    sceneNet.sceneReady.Clear();
                    
                    
                    var playerDb = Utils.Database.PlayerInfoDatabase.Instance;
                    
                    foreach (var player in data.playerList.items)
                    {
                        if (string.IsNullOrEmpty(player.playerId))
                            continue;
                        
                        if (!sceneNet.sceneParticipantIds.Contains(player.playerId))
                        {
                            sceneNet.sceneParticipantIds.Add(player.playerId);
                        }
                        sceneNet.sceneReady[player.playerId] = player.ready;
                        
                        
                        if (service.IsSelfId(player.playerId))
                        {
                            sceneNet.localReady = player.ready;
                        }
                        
                        
                        if (!string.IsNullOrEmpty(player.steamId) && !string.IsNullOrEmpty(player.steamName))
                        {
                            playerDb.AddOrUpdatePlayer(
                                steamId: player.steamId,
                                playerName: player.steamName,
                                avatarUrl: "", 
                                isLocal: service.IsSelfId(player.playerId),
                                endPoint: player.playerId,
                                lastUpdate: System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            );
                        }
                    }
                    
                    LoggerHelper.Log($"[SceneVote] Player cache updated, {data.playerList.items.Length} players");
                    return;
                }
                
                
                sceneNet.expiredVoteId = data.voteId;
                LoggerHelper.Log($"[SceneVote] 收到投票取消通知，voteId={data.voteId}，更新 expiredVoteId={sceneNet.expiredVoteId}");

                if (sceneNet.sceneVoteActive)
                {
                    sceneNet.sceneVoteActive = false;
                    sceneNet.sceneReady.Clear();
                    sceneNet.localReady = false;
                    sceneNet.sceneParticipantIds.Clear();
                }
                return;
            }

            
            string mySceneId = null;
            LocalPlayerManager.Instance.ComputeIsInGame(out mySceneId);
            mySceneId = mySceneId ?? string.Empty;

            if (!string.IsNullOrEmpty(data.hostSceneId) && !string.IsNullOrEmpty(mySceneId))
            {
                if (!string.Equals(data.hostSceneId, mySceneId, System.StringComparison.Ordinal))
                {
                    
                    LoggerHelper.Log(
                        $"[SceneVote] 不同场景，忽略投票: host={data.hostSceneId}, me={mySceneId}"
                    );
                    return;
                }
            }

            
            sceneNet.sceneVoteActive = true;
            sceneNet.sceneTargetId = data.targetSceneId;
            sceneNet.sceneCurtainGuid = data.curtainGuid;
            sceneNet.sceneLocationName = data.locationName;
            sceneNet.sceneNotifyEvac = data.notifyEvac;
            sceneNet.sceneSaveToFile = data.saveToFile;
            sceneNet.sceneUseLocation = data.useLocation;

            
            
            sceneNet.sceneParticipantIds.Clear();
            sceneNet.sceneReady.Clear();

            
            if (data.playerList != null && data.playerList.items != null)
            {
                LoggerHelper.Log(
                    $"[SceneVote] 收到 {data.playerList.items.Length} 个玩家信息: {string.Join(", ", data.playerList.items.Select(p => $"{p.playerName}({p.playerId})"))}"
                );

                
                foreach (var player in data.playerList.items)
                {
                    if (string.IsNullOrEmpty(player.playerId))
                        continue;

                    LoggerHelper.Log(
                        $"[SceneVote] 解析玩家: name='{player.playerName}', id='{player.playerId}', steamId='{player.steamId}', ready={player.ready}"
                    );

                    
                    if (!sceneNet.sceneParticipantIds.Contains(player.playerId))
                    {
                        sceneNet.sceneParticipantIds.Add(player.playerId);
                        LoggerHelper.Log(
                            $"[SceneVote] 添加参与者: {player.playerName}({player.playerId}), IsSelfId={service.IsSelfId(player.playerId)}"
                        );
                    }
                    sceneNet.sceneReady[player.playerId] = player.ready;

                    
                    if (service.IsSelfId(player.playerId))
                    {
                        sceneNet.localReady = player.ready;
                        LoggerHelper.Log(
                            $"[SceneVote] 识别到自己: {player.playerName}({player.playerId})"
                        );
                    }
                }
            }
            else
            {
                LoggerHelper.LogWarning("[SceneVote] 收到的投票状态没有玩家信息");
            }

            
            sceneNet.cachedVoteData = data;

            LoggerHelper.Log(
                $"[SceneVote] 更新投票状态: {data.targetSceneId}, 参与者: {sceneNet.sceneParticipantIds.Count}, 已准备: {data.readyPlayers}/{data.totalPlayers}"
            );
            LoggerHelper.Log($"[SceneVote] 参与者列表: {string.Join(", ", sceneNet.sceneParticipantIds)}");
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogError($"[SceneVote] 处理投票状态失败: {ex.Message}");
        }
    }

    
    
    
    public static void Client_ToggleReady(bool ready)
    {
        var service = ModBehaviourF.Instance;
        if (service == null)
            return;

        var myId = service.localPlayerStatus?.EndPoint ?? "";
        if (string.IsNullOrEmpty(myId))
        {
            LoggerHelper.LogWarning("[SceneVote] 无法获取本地玩家ID");
            return;
        }

        var data = new ReadyToggleData
        {
            playerId = myId,
            ready = ready,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        };

        LoggerHelper.Log($"[SceneVote] 客户端发送准备状态切换: playerId={myId}, ready={ready}");
        JsonMessage.SendToHost(data, DeliveryMethod.ReliableOrdered);

        
        var sceneNet = SceneNet.Instance;
        if (sceneNet != null && sceneNet.sceneVoteActive)
        {
            sceneNet.localReady = ready;
            if (sceneNet.sceneReady.ContainsKey(myId))
            {
                sceneNet.sceneReady[myId] = ready;
            }
            LoggerHelper.Log($"[SceneVote] 本地乐观更新完成");
        }

        LoggerHelper.Log($"[SceneVote] 客户端切换准备状态: {ready}");
    }

    
    
    
    public static void Client_RequestVote(
        string targetSceneId,
        string curtainGuid,
        bool notifyEvac,
        bool saveToFile,
        bool useLocation,
        string locationName
    )
    {
        var service = ModBehaviourF.Instance;
        if (service == null)
            return;

        var data = new VoteRequestData
        {
            targetSceneId = targetSceneId,
            curtainGuid = curtainGuid,
            locationName = locationName,
            notifyEvac = notifyEvac,
            saveToFile = saveToFile,
            useLocation = useLocation,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
        };

        JsonMessage.SendToHost(data, DeliveryMethod.ReliableOrdered);

        LoggerHelper.Log($"[SceneVote] 客户端请求发起投票: {targetSceneId}");
    }

    
    
    
    public static void Host_HandleVoteRequest(string json)
    {
        var service = ModBehaviourF.Instance;
        return;

        try
        {
            var data = JsonUtility.FromJson<VoteRequestData>(json);
            if (data == null || data.type != "sceneVoteRequest")
            {
                LoggerHelper.LogWarning("[SceneVote] 无效的投票请求数据");
                return;
            }

            LoggerHelper.Log($"[SceneVote] 收到客户端投票请求: {data.targetSceneId}");

            
            Host_StartVote(
                data.targetSceneId,
                data.curtainGuid,
                data.notifyEvac,
                data.saveToFile,
                data.useLocation,
                data.locationName
            );
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogError($"[SceneVote] 处理投票请求失败: {ex.Message}");
        }
    }

    
    
    
    public static void Host_HandleReadyToggle(string json)
    {
        var service = ModBehaviourF.Instance;
        return;

        try
        {
            LoggerHelper.Log($"[SceneVote] 主机收到准备状态切换消息: {json}");

            var data = JsonUtility.FromJson<ReadyToggleData>(json);
            if (data == null || data.type != "sceneVoteReady")
            {
                LoggerHelper.LogWarning("[SceneVote] 无效的准备状态数据");
                return;
            }

            LoggerHelper.Log($"[SceneVote] 解析成功: playerId={data.playerId}, ready={data.ready}");
            Host_HandleReadyToggle(data.playerId, data.ready);
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogError($"[SceneVote] 处理准备状态失败: {ex.Message}");
        }
    }

    
    
    
    public static void Client_HandleForceSceneLoad(string json)
    {
        var service = ModBehaviourF.Instance;
        if (service == null)
            return;

        LoggerHelper.Log($"[SceneVote] 客户端收到强制场景切换 JSON: {json}");

        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ForceSceneLoadData>(json);
            if (data == null || data.type != "forceSceneLoad")
            {
                LoggerHelper.LogWarning("[SceneVote] 无效的强制场景切换数据");
                return;
            }

            var sceneNet = SceneNet.Instance;
            if (sceneNet == null)
            {
                LoggerHelper.LogWarning("[SceneVote] SceneNet 实例不存在");
                return;
            }

            LoggerHelper.Log($"[SceneVote] 🚀 强制场景切换: {data.targetSceneId}");

            
            if (sceneNet.sceneVoteActive)
            {
                LoggerHelper.Log("[SceneVote] 停止投票 UI，准备传送");
                sceneNet.sceneVoteActive = false;
                sceneNet.sceneReady.Clear();
                sceneNet.localReady = false;
                sceneNet.sceneParticipantIds.Clear();
            }

            
            sceneNet.sceneTargetId = data.targetSceneId;
            sceneNet.sceneCurtainGuid = data.curtainGuid;
            sceneNet.sceneLocationName = data.locationName;
            sceneNet.sceneNotifyEvac = data.notifyEvac;
            sceneNet.sceneSaveToFile = data.saveToFile;
            sceneNet.sceneUseLocation = data.useLocation;

            
            sceneNet.allowLocalSceneLoad = true;

            
            var method = typeof(SceneNet).GetMethod(
                "TryPerformSceneLoad_Local",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            if (method != null)
            {
                method.Invoke(
                    sceneNet,
                    new object[]
                    {
                        data.targetSceneId,
                        data.curtainGuid,
                        data.notifyEvac,
                        data.saveToFile,
                        data.useLocation,
                        data.locationName
                    }
                );
                LoggerHelper.Log($"[SceneVote] ✅ 已触发场景加载: {data.targetSceneId}");
            }
            else
            {
                LoggerHelper.LogError("[SceneVote] 无法找到 TryPerformSceneLoad_Local 方法");
            }
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogError($"[SceneVote] 处理强制场景切换失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private static string GetSteamName(NetPeer peer)
    {
        try
        {
            if (!SteamManager.Initialized)
            {
                return "";
            }

            if (peer == null)
            {
                
                return Steamworks.SteamFriends.GetPersonaName();
            }

            
            var steamIdStr = GetSteamId(peer);
            if (!string.IsNullOrEmpty(steamIdStr))
            {
                var cachedName = ClientStatusMessage.GetSteamNameFromSteamId(steamIdStr);
                if (!string.IsNullOrEmpty(cachedName))
                {
                    LoggerHelper.Log(
                        $"[SceneVote] 从缓存获取 Steam 名字: {steamIdStr} -> {cachedName}"
                    );
                    return cachedName;
                }
            }

            
            if (!string.IsNullOrEmpty(steamIdStr) && ulong.TryParse(steamIdStr, out var steamIdValue))
            {
                var steamId = new Steamworks.CSteamID(steamIdValue);
                var steamName = Steamworks.SteamFriends.GetFriendPersonaName(steamId);
                if (!string.IsNullOrEmpty(steamName) && steamName != "[unknown]")
                {
                    LoggerHelper.Log(
                        $"[SceneVote] 从 Steam API 获取名字: {steamIdStr} -> {steamName}"
                    );
                    return steamName;
                }
            }
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogWarning($"[SceneVote] 获取Steam用户名失败: {ex.Message}");
        }

        return "";
    }

    
    
    
    private static string GetSteamId(NetPeer peer)
    {
        try
        {
            if (!SteamManager.Initialized)
            {
                return "";
            }

            if (peer == null)
            {
                
                return Steamworks.SteamUser.GetSteamID().ToString();
            }

            
            var service = ModBehaviourF.Instance;
            if (service == null || service.playerStatuses == null)
            {
                return "";
            }

            if (!service.playerStatuses.TryGetValue(peer, out var status))
            {
                LoggerHelper.LogWarning($"[SceneVote] 找不到 PlayerStatus: {peer.EndPoint}");
                return "";
            }

            
            var endPoint = status.EndPoint;

            
            if (endPoint.StartsWith("Steam:"))
            {
                var steamIdStr = endPoint.Substring(6); 
                if (ulong.TryParse(steamIdStr, out ulong steamId))
                {
                    LoggerHelper.Log($"[SceneVote] 从 Steam: 格式获取 SteamID: {endPoint} -> {steamId}");
                    return steamId.ToString();
                }
            }

            
            if (endPoint.StartsWith("Host:"))
            {
                if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby && SteamManager.Initialized)
                {
                    var lobbyOwner = Steamworks.SteamMatchmaking.GetLobbyOwner(
                        new Steamworks.CSteamID(SteamLobbyManager.Instance.CurrentLobbyId)
                    );
                    LoggerHelper.Log($"[SceneVote] 从 Host: 格式获取 SteamID: {endPoint} -> {lobbyOwner.m_SteamID}");
                    return lobbyOwner.m_SteamID.ToString();
                }
            }

            
            var parts = endPoint.Split(':');
            if (
                parts.Length == 2
                && System.Net.IPAddress.TryParse(parts[0], out var ipAddr)
                && int.TryParse(parts[1], out var port)
            )
            {
                var ipEndPoint = new System.Net.IPEndPoint(ipAddr, port);
                if (
                    SteamEndPointMapper.Instance != null
                    && SteamEndPointMapper.Instance.TryGetSteamID(ipEndPoint, out var cSteamId)
                )
                {
                    LoggerHelper.Log($"[SceneVote] 从虚拟 IP 获取 SteamID: {endPoint} -> {cSteamId.m_SteamID}");
                    return cSteamId.m_SteamID.ToString();
                }
                else
                {
                    LoggerHelper.LogWarning($"[SceneVote] 无法从虚拟 IP 获取 SteamID: {endPoint}");
                }
            }
        }
        catch (System.Exception ex)
        {
            LoggerHelper.LogWarning($"[SceneVote] 获取SteamID失败: {ex.Message}\n{ex.StackTrace}");
        }

        return ""; 
    }

    
    
    
    public static bool HasActiveVote()
    {
        return _hostVoteState != null && _hostVoteState.active;
    }

    
    
    
    public static void UpdatePlayerEndPoint(string oldEndPoint, string newEndPoint, string steamName)
    {
        if (_hostVoteState == null || _hostVoteState.playerList == null || _hostVoteState.playerList.items == null)
            return;

        
        foreach (var player in _hostVoteState.playerList.items)
        {
            if (player.playerId == oldEndPoint)
            {
                player.playerId = newEndPoint;
                
                
                if (!string.IsNullOrEmpty(steamName))
                {
                    player.steamName = steamName;
                }

                LoggerHelper.Log(
                    $"[SceneVote] ✓ 已更新投票玩家列表: {oldEndPoint} -> {newEndPoint}, steamName={steamName}"
                );
                
                
                Host_BroadcastVoteState();
                break;
            }
        }
    }
}
