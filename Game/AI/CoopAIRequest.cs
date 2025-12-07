















using EscapeFromDuckovCoopMod.Utils;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod;

public class AIRequest : MonoBehaviour
{
    public static AIRequest Instance;

    private static NetService Service => NetService.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;

    private readonly Dictionary<string, HashSet<int>> _sceneSeedsSent = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, List<CharacterSpawnerRoot>> _sceneRootsCache = new Dictionary<string, List<CharacterSpawnerRoot>>();
    private float _lastCacheRefreshTime = 0f;
    private const float CACHE_REFRESH_INTERVAL = 5f;

    public void Init()
    {
        Instance = this;
    }

    
    public void Server_SendRootSeedDelta(CharacterSpawnerRoot r, NetPeer target = null)
    {
        if (!IsServer || r == null) return;

        var idA = AITool.StableRootId(r);
        var idB = AITool.StableRootId_Alt(r);

        var seed = AITool.DeriveSeed(COOPManager.AIHandle.sceneSeed, idA);
        COOPManager.AIHandle.aiRootSeeds[idA] = seed;

        var rootSceneId = GetRootSceneId(r);

        var msg = new Net.HybridNet.AISeedPatchMessage
        {
            Count = idA == idB ? 1 : 2,
            AiIdA = idA,
            SeedA = seed,
            AiIdB = idA == idB ? 0 : idB,
            SeedB = seed
        };

        if (target != null)
        {
            Net.HybridNet.HybridNetCore.Send(msg, target);
        }
        else
        {
            var service = NetService.Instance;
            if (service?.playerStatuses != null)
            {
                foreach (var kv in service.playerStatuses)
                {
                    var peer = kv.Key;
                    var status = kv.Value;
                    if (peer == null || status == null) continue;

                    if (string.IsNullOrEmpty(rootSceneId) || string.Equals(status.SceneId, rootSceneId, StringComparison.Ordinal))
                    {
                        Net.HybridNet.HybridNetCore.Send(msg, peer);
                    }
                }
            }
        }

        Debug.Log($"[AI-SEED] 增量发送 Root 种子: rootId={idA}, sceneId={rootSceneId}, seed={seed}");
    }

    public string GetRootSceneId(CharacterSpawnerRoot root)
    {
        if (root == null) return string.Empty;

        try
        {
            var fi = typeof(CharacterSpawnerRoot).GetField("relatedScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var sceneIndex = (int)fi.GetValue(root);
                if (sceneIndex >= 0)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex(sceneIndex);
                    if (scene.IsValid())
                    {
                        return scene.name;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AI-SEED] 获取 Root 场景ID失败: {ex.Message}");
        }

        return string.Empty;
    }

    public void Server_HandleSceneAISeedRequest(string playerId, string sceneId, NetPeer peer)
    {
        if (!IsServer || peer == null) return;

        Debug.Log($"[AI-SEED] 收到场景AI种子请求: playerId={playerId}, sceneId={sceneId}");

        var peerKey = $"{peer.EndPoint}_{sceneId}";
        if (!_sceneSeedsSent.ContainsKey(peerKey))
        {
            _sceneSeedsSent[peerKey] = new HashSet<int>();
        }

        RefreshSceneRootsCache();

        if (!_sceneRootsCache.TryGetValue(sceneId, out var roots))
        {
            Debug.LogWarning($"[AI-SEED] 未找到场景{sceneId}的Root");
            return;
        }

        var newSeeds = new List<Net.HybridNet.AISeedPair>();
        var sentSet = _sceneSeedsSent[peerKey];

        foreach (var r in roots)
        {
            if (r == null) continue;

            var idA = AITool.StableRootId(r);
            var idB = AITool.StableRootId_Alt(r);

            if (!sentSet.Contains(idA))
            {
                var seed = AITool.DeriveSeed(COOPManager.AIHandle.sceneSeed, idA);
                newSeeds.Add(new Net.HybridNet.AISeedPair { RootId = idA, Seed = seed });
                sentSet.Add(idA);

                if (idB != idA && !sentSet.Contains(idB))
                {
                    newSeeds.Add(new Net.HybridNet.AISeedPair { RootId = idB, Seed = seed });
                    sentSet.Add(idB);
                }
            }
        }

        if (newSeeds.Count > 0)
        {
            var responseMsg = new Net.HybridNet.SceneAISeedResponseMessage
            {
                SceneId = sceneId,
                SceneSeed = COOPManager.AIHandle.sceneSeed,
                Seeds = newSeeds
            };
            Net.HybridNet.HybridNetCore.Send(responseMsg, peer);

            Debug.Log($"[AI-SEED] 发送场景AI种子响应: sceneId={sceneId}, 种子数={newSeeds.Count}");
        }
        else
        {
            Debug.Log($"[AI-SEED] 所有种子已发送给 {playerId}");
        }
    }

    private void RefreshSceneRootsCache()
    {
        if (Time.time - _lastCacheRefreshTime < CACHE_REFRESH_INTERVAL) return;

        _sceneRootsCache.Clear();
        var allRoots = UnityEngine.Object.FindObjectsOfType<CharacterSpawnerRoot>(true);

        foreach (var r in allRoots)
        {
            if (r == null) continue;

            var sceneId = GetRootSceneId(r);
            if (string.IsNullOrEmpty(sceneId)) continue;

            if (!_sceneRootsCache.ContainsKey(sceneId))
            {
                _sceneRootsCache[sceneId] = new List<CharacterSpawnerRoot>();
            }
            _sceneRootsCache[sceneId].Add(r);
        }

        _lastCacheRefreshTime = Time.time;
        Debug.Log($"[AI-SEED] 刷新场景Root缓存: 共{_sceneRootsCache.Count}个场景");
    }

    public void Client_HandleSceneAISeedResponse(string sceneId, int sceneSeed, List<Net.HybridNet.AISeedPair> seeds)
    {
        if (IsServer) return;

        Debug.Log($"[AI-SEED] 收到场景AI种子响应: sceneId={sceneId}, 种子数={seeds?.Count ?? 0}");

        if (seeds == null || seeds.Count == 0) return;

        foreach (var pair in seeds)
        {
            COOPManager.AIHandle.aiRootSeeds[pair.RootId] = pair.Seed;
        }

        Debug.Log($"[AI-SEED] 已应用场景{sceneId}的AI种子");
    }

    public void ClearSentSeedsForPeer(NetPeer peer)
    {
        if (!IsServer || peer == null) return;

        var keysToRemove = new List<string>();
        var peerEndPoint = peer.EndPoint.ToString();

        foreach (var key in _sceneSeedsSent.Keys)
        {
            if (key.StartsWith(peerEndPoint))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _sceneSeedsSent.Remove(key);
        }

        Debug.Log($"[AI-SEED] 清理断开连接玩家的种子记录: {peerEndPoint}");
    }

    public void Server_TryRebroadcastIconLater(int aiId, CharacterMainControl cmc)
    {
        if (!IsServer || aiId == 0 || !cmc) return;
        if (!AIName._iconRebroadcastScheduled.Add(aiId)) return;

        StartCoroutine(AIName.IconRebroadcastRoutine(aiId, cmc));
    }
}