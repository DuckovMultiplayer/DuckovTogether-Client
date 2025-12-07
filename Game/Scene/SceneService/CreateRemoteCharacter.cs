















using Duckov.Utilities;
using ItemStatsSystem;
using Saves;
using UnityEngine.AI;

namespace EscapeFromDuckovCoopMod;

public static class CreateRemoteCharacter
{
    private static NetService Service => NetService.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;

    
    private static int _createRemoteLogCount = 0;
    private static System.DateTime _lastCreateRemoteLogTime = System.DateTime.MinValue;
    private const double CREATE_REMOTE_LOG_INTERVAL = 5.0;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;
    private static Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private static Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private static Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public static async UniTask<GameObject> CreateRemoteCharacterAsync(NetPeer peer, Vector3 position, Quaternion rotation, string customFaceJson)
    {
        if (remoteCharacters.ContainsKey(peer) && remoteCharacters[peer] != null) return null;

        var levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.MainCharacter == null) return null;

        var instance = GameObject.Instantiate(CharacterMainControl.Main.gameObject, position, rotation);
        
        var characterModel = instance.GetComponent<CharacterMainControl>();

        
        

        COOPManager.StripAllHandItems(characterModel);
        var itemLoaded = await ItemSavesUtilities.LoadItem(LevelManager.MainCharacterItemSaveKey);
        if (itemLoaded == null)
        {
            itemLoaded = await ItemAssetsCollection.InstantiateAsync(GameplayDataSettings.ItemAssets.DefaultCharacterItemTypeID);
            Debug.LogWarning("Item Loading failed");
        }

        Traverse.Create(characterModel).Field<Item>("characterItem").Value = itemLoaded;
        
        
        instance.transform.SetPositionAndRotation(position, rotation);

        MakeRemotePhysicsPassive(instance);

        CustomFace.StripAllCustomFaceParts(instance);

        if (characterModel?.characterModel.CustomFace != null && !string.IsNullOrEmpty(customFaceJson))
        {
            var customFaceData = JsonUtility.FromJson<CustomFaceSettingData>(customFaceJson);
            characterModel.characterModel.CustomFace.LoadFromData(customFaceData);
        }

        try
        {
            var cm = characterModel.characterModel;

            COOPManager.ChangeArmorModel(cm, null);
            COOPManager.ChangeHelmatModel(cm, null);
            COOPManager.ChangeFaceMaskModel(cm, null);
            COOPManager.ChangeBackpackModel(cm, null);
            COOPManager.ChangeHeadsetModel(cm, null);
        }
        catch
        {
        }


        instance.AddComponent<RemoteReplicaTag>();
        var anim = instance.GetComponentInChildren<Animator>(true);
        if (anim)
        {
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal;
        }

        var h = instance.GetComponentInChildren<Health>(true);
        if (h) h.autoInit = false; 
        instance.AddComponent<AutoRequestHealthBar>(); 
        
        HealthTool.Server_HookOneHealth(peer, instance);
        instance.AddComponent<HostForceHealthBar>();

        NetInterpUtil.Attach(instance)?.Push(position, rotation);
        AnimInterpUtil.Attach(instance); 
        characterModel.gameObject.SetActive(false);
        remoteCharacters[peer] = instance;
        characterModel.gameObject.SetActive(true);

        
        Service.MarkPlayerJoinedSuccessfully(peer);

        return instance;
    }

    public static async UniTask CreateRemoteCharacterForClient(string playerId, Vector3 position, Quaternion rotation, string customFaceJson)
    {
        if (NetService.Instance.IsSelfId(playerId)) return; 
        if (clientRemoteCharacters.ContainsKey(playerId) && clientRemoteCharacters[playerId] != null) return;

        
        _createRemoteLogCount++;
        var now = System.DateTime.Now;
        if ((now - _lastCreateRemoteLogTime).TotalSeconds >= CREATE_REMOTE_LOG_INTERVAL)
        {
            if (_createRemoteLogCount > 1)
            {
                Debug.Log($"[CreateRemote] 创建了 {_createRemoteLogCount} 个远程角色 (最后: {playerId})");
            }
            else
            {
                Debug.Log($"[CreateRemote] {playerId} CreateRemoteCharacterForClient");
            }
            _createRemoteLogCount = 0;
            _lastCreateRemoteLogTime = now;
        }

        var levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.MainCharacter == null) return;


        var instance = GameObject.Instantiate(CharacterMainControl.Main.gameObject, position, rotation);
        
        var characterModel = instance.GetComponent<CharacterMainControl>();

        var itemLoaded = await ItemSavesUtilities.LoadItem(LevelManager.MainCharacterItemSaveKey);
        if (itemLoaded == null) itemLoaded = await ItemAssetsCollection.InstantiateAsync(GameplayDataSettings.ItemAssets.DefaultCharacterItemTypeID);
        Traverse.Create(characterModel).Field<Item>("characterItem").Value = itemLoaded;

        COOPManager.StripAllHandItems(characterModel);

        instance.transform.SetPositionAndRotation(position, rotation);

        
        if (characterModel && characterModel.modelRoot)
        {
            var e = rotation.eulerAngles;
            characterModel.modelRoot.transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        }

        MakeRemotePhysicsPassive(instance);
        CustomFace.StripAllCustomFaceParts(instance);

        
        if (string.IsNullOrEmpty(customFaceJson))
        {
            if (NetService.Instance.clientPlayerStatuses.TryGetValue(playerId, out var st) && !string.IsNullOrEmpty(st.CustomFaceJson))
                customFaceJson = st.CustomFaceJson;
            else if (CustomFace._cliPendingFace.TryGetValue(playerId, out var pending) && !string.IsNullOrEmpty(pending))
                customFaceJson = pending;
        }


        CustomFace.Client_ApplyFaceIfAvailable(playerId, instance, customFaceJson);


        try
        {
            var cm = characterModel.characterModel;

            COOPManager.ChangeArmorModel(cm, null);
            COOPManager.ChangeHelmatModel(cm, null);
            COOPManager.ChangeFaceMaskModel(cm, null);
            COOPManager.ChangeBackpackModel(cm, null);
            COOPManager.ChangeHeadsetModel(cm, null);
        }
        catch
        {
        }

        instance.AddComponent<RemoteReplicaTag>();
        var anim = instance.GetComponentInChildren<Animator>(true);
        if (anim)
        {
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.updateMode = AnimatorUpdateMode.Normal;
        }

        var h = instance.GetComponentInChildren<Health>(true);
        if (h) h.autoInit = false;
        instance.AddComponent<AutoRequestHealthBar>();
        CoopTool.Client_ApplyPendingRemoteIfAny(playerId, instance);

        NetInterpUtil.Attach(instance)?.Push(position, rotation);
        AnimInterpUtil.Attach(instance);
        characterModel.gameObject.SetActive(false);
        clientRemoteCharacters[playerId] = instance;
        characterModel.gameObject.SetActive(true);
    }

    private static void MakeRemotePhysicsPassive(GameObject go)
    {
        if (!go) return;

        
        var ai = go.GetComponentInChildren<AICharacterController>(true);
        if (ai) ai.enabled = false;

        var nma = go.GetComponentInChildren<NavMeshAgent>(true);
        if (nma) nma.enabled = false;

        var cc = go.GetComponentInChildren<CharacterController>(true);
        if (cc) cc.enabled = false; 

        
        var rb = go.GetComponentInChildren<Rigidbody>(true);
        if (rb)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        
        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim) anim.applyRootMotion = false;

        
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!mb) continue;
            var n = mb.GetType().Name;
            
            if (n.Contains("Locomotion") || n.Contains("Movement") || n.Contains("Motor"))
            {
                var beh = mb as Behaviour;
                if (beh) beh.enabled = false;
            }
        }
    }
}