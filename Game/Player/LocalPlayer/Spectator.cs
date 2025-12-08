















namespace EscapeFromDuckovCoopMod;

public class Spectator : MonoBehaviour
{
    public static Spectator Instance;
    public bool _spectatorEndOnVotePending;

    public bool _skipSpectatorForNextClosure;

    public bool _spectatorActive;
    public List<CharacterMainControl> _spectateList = new();
    public int _spectateIdx = -1;
    public float _spectateNextSwitchTime;
    public DamageInfo _lastDeathInfo;
    private ModBehaviourF Service => ModBehaviourF.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public void Init()
    {
        Instance = this;
    }

    public bool TryEnterSpectatorOnDeath(DamageInfo dmgInfo)
    {
        var main = CharacterMainControl.Main;
        if (!LevelManager.LevelInited || main == null) return false;

        BuildSpectateList(main);
        Debug.Log("观战: " + _spectateList.Count);

        if (_spectateList.Count <= 0) return false; 

        _lastDeathInfo = dmgInfo;
        _spectatorActive = true;
        _spectateIdx = 0;

        if (GameCamera.Instance) GameCamera.Instance.SetTarget(_spectateList[_spectateIdx]);

        if (SceneNet.Instance.sceneVoteActive)
            _spectatorEndOnVotePending = true;

        return true; 
    }

    public void BuildSpectateList(CharacterMainControl exclude)
    {
        _spectateList.Clear();

        var mySceneId = localPlayerStatus != null ? localPlayerStatus.SceneId : null;
        if (string.IsNullOrEmpty(mySceneId))
            LocalPlayerManager.Instance.ComputeIsInGame(out mySceneId);
        var myK = CanonicalizeSceneId(mySceneId);

        int cand = 0, kept = 0;

        if (IsServer)
            foreach (var kv in remoteCharacters)
            {
                var go = kv.Value;
                var cmc = go ? go.GetComponent<CharacterMainControl>() : null;
                if (!LocalPlayerManager.Instance.IsAlive(cmc) || cmc == exclude) continue;
                cand++;

                string peerScene = null;
                if (!SceneM._srvPeerScene.TryGetValue(kv.Key, out peerScene) && playerStatuses.TryGetValue(kv.Key, out var st))
                    peerScene = st?.SceneId;

                if (AreSameMap(mySceneId, peerScene))
                {
                    _spectateList.Add(cmc);
                    kept++;
                }
            }
        else
            foreach (var kv in clientRemoteCharacters)
            {
                var go = kv.Value;
                var cmc = go ? go.GetComponent<CharacterMainControl>() : null;
                if (!LocalPlayerManager.Instance.IsAlive(cmc) || cmc == exclude) continue;
                cand++;

                
                string peerScene = null;
                if (ModBehaviourF.Instance.clientPlayerStatuses.TryGetValue(kv.Key, out var st))
                    peerScene = st?.SceneId;

                
                if (string.IsNullOrEmpty(peerScene))
                    SceneNet.Instance._cliLastSceneIdByPlayer.TryGetValue(kv.Key, out peerScene);

                if (AreSameMap(mySceneId, peerScene))
                {
                    _spectateList.Add(cmc);
                    kept++;
                }
            }

        Debug.Log($"[SPECTATE] 候选={cand}, 同图保留={kept}, mySceneId={mySceneId} (canon={myK})");
    }

    
    public static string CanonicalizeSceneId(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        var s = id.Trim().ToLowerInvariant();

        
        string[] suffixes = { "_main", "_gameplay", "_core", "_scene", "_lod0", "_lod", "_client", "_server" };
        bool removed;
        do
        {
            removed = false;
            foreach (var suf in suffixes)
                if (s.EndsWith(suf))
                {
                    s = s.Substring(0, s.Length - suf.Length);
                    removed = true;
                }
        } while (removed);

        while (s.Contains("__")) s = s.Replace("__", "_");

        var parts = s.Split('_');
        if (parts.Length >= 2 && parts[0] == "level")
            s = parts[0] + "_" + parts[1];

        if (s == "base" || s.StartsWith("base_")) s = "base";
        return s;
    }

    public static bool AreSameMap(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return a == b;
        return CanonicalizeSceneId(a) == CanonicalizeSceneId(b);
    }

    
    public bool IsInSameScene(CharacterMainControl cmc)
    {
        if (!cmc) return false;
        var mySceneId = localPlayerStatus != null ? localPlayerStatus.SceneId : null;
        if (string.IsNullOrEmpty(mySceneId)) return true; 

        if (IsServer)
            foreach (var kv in remoteCharacters)
            {
                if (kv.Value == null) continue;
                var v = kv.Value.GetComponent<CharacterMainControl>();
                if (v == cmc)
                {
                    if (playerStatuses.TryGetValue(kv.Key, out var st) && st != null)
                        return st.SceneId == mySceneId;
                    return false;
                }
            }
        else
            foreach (var kv in clientRemoteCharacters)
            {
                if (kv.Value == null) continue;
                var v = kv.Value.GetComponent<CharacterMainControl>();
                if (v == cmc)
                {
                    if (ModBehaviourF.Instance.clientPlayerStatuses.TryGetValue(kv.Key, out var st) && st != null)
                        return st.SceneId == mySceneId;
                    return false;
                }
            }

        return false;
    }

    public void SpectateNext()
    {
        if (_spectateList.Count == 0) return;
        _spectateIdx = (_spectateIdx + 1) % _spectateList.Count;
        if (GameCamera.Instance) GameCamera.Instance.SetTarget(_spectateList[_spectateIdx]);
    }

    public void SpectatePrev()
    {
        if (_spectateList.Count == 0) return;
        _spectateIdx = (_spectateIdx - 1 + _spectateList.Count) % _spectateList.Count;
        if (GameCamera.Instance) GameCamera.Instance.SetTarget(_spectateList[_spectateIdx]);
    }

    public void EndSpectatorAndShowClosure()
    {
        _spectatorEndOnVotePending = false;

        if (!_spectatorActive) return;
        _spectatorActive = false;
        _skipSpectatorForNextClosure = true;

        try
        {
            var t = AccessTools.TypeByName("Duckov.UI.ClosureView");
            var mi = AccessTools.Method(t, "ShowAndReturnTask", new[] { typeof(DamageInfo), typeof(float) });
            if (mi != null) ((UniTask)mi.Invoke(null, new object[] { _lastDeathInfo, 0.5f })).Forget();
        }
        catch
        {
        }
    }
}