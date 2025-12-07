















using System.Reflection;
using Duckov.UI;
using UnityEngine.EventSystems;

namespace EscapeFromDuckovCoopMod;

public static class SceneM
{
    public static readonly Dictionary<NetPeer, string> _srvPeerScene = new();
    private static NetService Service => NetService.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;
    private static Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private static Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private static Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;


    
    public static bool AllPlayersDead()
    {
        
        var mySceneId = localPlayerStatus != null ? localPlayerStatus.SceneId : null;
        if (string.IsNullOrEmpty(mySceneId))
            LocalPlayerManager.Instance.ComputeIsInGame(out mySceneId);

        
        if (string.IsNullOrEmpty(mySceneId))
        {
            var alive = 0;
            if (LocalPlayerManager.Instance.IsAlive(CharacterMainControl.Main)) alive++;
            if (IsServer)
                foreach (var kv in remoteCharacters)
                {
                    var cmc = kv.Value ? kv.Value.GetComponent<CharacterMainControl>() : null;
                    if (LocalPlayerManager.Instance.IsAlive(cmc)) alive++;
                }
            else
                foreach (var kv in clientRemoteCharacters)
                {
                    var cmc = kv.Value ? kv.Value.GetComponent<CharacterMainControl>() : null;
                    if (LocalPlayerManager.Instance.IsAlive(cmc)) alive++;
                }

            return alive == 0;
        }

        var aliveSameScene = 0;

        
        if (LocalPlayerManager.Instance.IsAlive(CharacterMainControl.Main)) aliveSameScene++;

        if (IsServer)
            foreach (var kv in remoteCharacters)
            {
                var cmc = kv.Value ? kv.Value.GetComponent<CharacterMainControl>() : null;
                if (!LocalPlayerManager.Instance.IsAlive(cmc)) continue;

                string peerScene = null;
                if (!_srvPeerScene.TryGetValue(kv.Key, out peerScene) && playerStatuses.TryGetValue(kv.Key, out var st))
                    peerScene = st?.SceneId;

                if (Spectator.AreSameMap(mySceneId, peerScene)) aliveSameScene++;
            }
        else
            foreach (var kv in clientRemoteCharacters)
            {
                var cmc = kv.Value ? kv.Value.GetComponent<CharacterMainControl>() : null;
                if (!LocalPlayerManager.Instance.IsAlive(cmc)) continue;

                var peerScene = NetService.Instance.clientPlayerStatuses.TryGetValue(kv.Key, out var st) ? st?.SceneId : null;
                if (Spectator.AreSameMap(mySceneId, peerScene)) aliveSameScene++;
            }

        var none = aliveSameScene <= 0;
        if (none)
            Debug.Log("[SPECTATE] 本地图无人存活 → 退出观战并触发结算");
        return none;
    }

    
    public static void Call_NotifyEntryClicked_ByInvoke(
        MapSelectionView view,
        MapSelectionEntry entry,
        PointerEventData evt 
    )
    {
        var mi = typeof(MapSelectionView).GetMethod(
            "NotifyEntryClicked",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(MapSelectionEntry), typeof(PointerEventData) },
            null
        );
        if (mi == null)
            throw new MissingMethodException("MapSelectionView.NotifyEntryClicked(MapSelectionEntry, PointerEventData) not found.");

        mi.Invoke(view, new object[] { entry, evt });
    }


    public static IEnumerable<NetPeer> Server_EnumPeersInSameSceneAsHost()
    {
        var hostSceneId = localPlayerStatus != null ? localPlayerStatus.SceneId : null;
        if (string.IsNullOrEmpty(hostSceneId))
            LocalPlayerManager.Instance.ComputeIsInGame(out hostSceneId);
        if (string.IsNullOrEmpty(hostSceneId)) yield break;

        foreach (var p in netManager.ConnectedPeerList)
        {
            string peerScene = null;
            if (!_srvPeerScene.TryGetValue(p, out peerScene) && playerStatuses.TryGetValue(p, out var st))
                peerScene = st.SceneId;

            if (!string.IsNullOrEmpty(peerScene) && peerScene == hostSceneId)
                yield return p;
        }
    }
}