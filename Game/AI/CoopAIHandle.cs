















using EscapeFromDuckovCoopMod.Net;
using EscapeFromDuckovCoopMod.Utils;
using System.Reflection;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace EscapeFromDuckovCoopMod;

public class AIHandle
{
    private const byte AI_LOADOUT_VER = 5;


    
    private static readonly AccessTools.FieldRef<CharacterRandomPreset, bool>
        FR_UsePlayerPreset = AccessTools.FieldRefAccess<CharacterRandomPreset, bool>("usePlayerPreset");

    private static readonly AccessTools.FieldRef<CharacterRandomPreset, CustomFacePreset>
        FR_FacePreset = AccessTools.FieldRefAccess<CharacterRandomPreset, CustomFacePreset>("facePreset");

    private static readonly AccessTools.FieldRef<CharacterRandomPreset, CharacterModel>
        FR_CharacterModel = AccessTools.FieldRefAccess<CharacterRandomPreset, CharacterModel>("characterModel");

    
    private static readonly FieldInfo FI_defaultMax =
        typeof(Health).GetField("defaultMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_lastMax =
        typeof(Health).GetField("lastMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI__current =
        typeof(Health).GetField("_currentHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_characterCached =
        typeof(Health).GetField("characterCached", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_hasCharacter =
        typeof(Health).GetField("hasCharacter", BindingFlags.NonPublic | BindingFlags.Instance);

    public readonly Dictionary<Health, float> _cliAiMaxOverride = new();

    
    public readonly Dictionary<int, float> _cliPendingAiHealth = new();

    
    public readonly Dictionary<int, float> _cliPendingAiMax = new();

    
    public readonly Dictionary<int, AiAnimState> _pendingAiAnims = new();
    public readonly Dictionary<int, int> aiRootSeeds = new(); 

    public readonly Dictionary<int, (
        List<(int slot, int tid)> equips,
        List<(int slot, int tid)> weapons,
        string faceJson,
        string modelName,
        int iconType,
        bool showName,
        string displayName
        )> pendingAiLoadouts = new();

    public bool freezeAI = true; 
    public int sceneSeed;
    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;


    public void Server_SendAiSeeds(NetPeer target = null)
    {
        if (!DedicatedServerMode.ShouldBroadcastState()) return;

        aiRootSeeds.Clear();
        sceneSeed = Environment.TickCount ^ Random.Range(int.MinValue, int.MaxValue);

        var roots = Object.FindObjectsOfType<CharacterSpawnerRoot>(true);

        var pairs = new List<(int id, int seed)>(roots.Length * 2);
        foreach (var r in roots)
        {
            var idA = AITool.StableRootId(r);
            var idB = AITool.StableRootId_Alt(r);

            var seed = AITool.DeriveSeed(sceneSeed, idA);
            aiRootSeeds[idA] = seed;

            pairs.Add((idA, seed));
            if (idB != idA) pairs.Add((idB, seed));
        }

        AISeedMessage.Server_SendSeedSnapshot(pairs, sceneSeed, target);
    }


    public void HandleAiSeedSnapshot(NetDataReader r)
    {
        sceneSeed = r.GetInt();
        aiRootSeeds.Clear();
        var n = r.GetInt();
        for (var i = 0; i < n; i++)
        {
            var id = r.GetInt();
            var seed = r.GetInt();
            aiRootSeeds[id] = seed;
        }

        Debug.Log($"[AI-SEED] 收到 {n} 个 Root 的种子");
    }


    public void RegisterAi(int aiId, CharacterMainControl cmc)
    {
        if (!AITool.IsRealAI(cmc)) return;
        AITool.aiById[aiId] = cmc;

        float pendCur = -1f, pendMax = -1f;
        if (_cliPendingAiHealth.TryGetValue(aiId, out var pc))
        {
            pendCur = pc;
            _cliPendingAiHealth.Remove(aiId);
        }

        if (_cliPendingAiMax.TryGetValue(aiId, out var pm))
        {
            pendMax = pm;
            _cliPendingAiMax.Remove(aiId);
        }

        var h = cmc.Health;
        if (h)
        {
            if (pendMax > 0f)
            {
                _cliAiMaxOverride[h] = pendMax;
                try
                {
                    FI_defaultMax?.SetValue(h, Mathf.RoundToInt(pendMax));
                }
                catch
                {
                }

                try
                {
                    FI_lastMax?.SetValue(h, -12345f);
                }
                catch
                {
                }

                try
                {
                    h.OnMaxHealthChange?.Invoke(h);
                }
                catch
                {
                }
            }

            if (pendCur >= 0f || pendMax > 0f)
            {
                var applyMax = pendMax > 0f ? pendMax : h.MaxHealth;
                HealthM.Instance.ForceSetHealth(h, applyMax, Mathf.Max(0f, pendCur >= 0f ? pendCur : h.CurrentHealth));
            }
        }

        if (IsServer && cmc)
            Server_BroadcastAiLoadout(aiId, cmc);

        if (!IsServer && cmc)
        {
            var follower = cmc.GetComponent<NetAiFollower>();
            if (!follower) follower = cmc.gameObject.AddComponent<NetAiFollower>();

            if (!cmc.GetComponent<NetAiVisibilityGuard>())
                cmc.gameObject.AddComponent<NetAiVisibilityGuard>();

            if (!cmc.GetComponent<AISceneVisibilityGuard>())
                cmc.gameObject.AddComponent<AISceneVisibilityGuard>();

            try
            {
                var tag = cmc.GetComponent<NetAiTag>();
                if (tag == null) tag = cmc.gameObject.AddComponent<NetAiTag>();
                if (tag.aiId != aiId) tag.aiId = aiId;
            }
            catch
            {
            }

            if (!cmc.GetComponent<RemoteReplicaTag>()) cmc.gameObject.AddComponent<RemoteReplicaTag>();

            if (_pendingAiAnims.TryGetValue(aiId, out var st))
            {
                if (!follower) follower = cmc.gameObject.AddComponent<NetAiFollower>();
                follower.SetAnim(st.speed, st.dirX, st.dirY, st.hand, st.gunReady, st.dashing);
                _pendingAiAnims.Remove(aiId);
            }
        }

        
        if (pendingAiLoadouts.TryGetValue(aiId, out var data))
        {
            pendingAiLoadouts.Remove(aiId);
            Client_ApplyAiLoadout(aiId, data.equips, data.weapons, data.faceJson, data.modelName, data.iconType, data.showName, data.displayName).Forget();
        }
    }


    public void Server_BroadcastAiLoadout(int aiId, CharacterMainControl cmc)
    {
        if (!IsServer || cmc == null) return;

        writer.Reset();
        writer.Put(AI_LOADOUT_VER);
        writer.Put(aiId);

        var eqList = AITool.GetLocalAIEquipment(cmc);
        writer.Put(eqList.Count);
        foreach (var eq in eqList)
        {
            writer.Put(eq.SlotHash);

            
            var tid = 0;
            if (!string.IsNullOrEmpty(eq.ItemId))
                int.TryParse(eq.ItemId, out tid);

            writer.Put(tid);
        }

        
        var listW = new List<(int slot, int tid)>();
        var gun = cmc.GetGun();
        var melee = cmc.GetMeleeWeapon();
        if (gun != null) listW.Add(((int)gun.handheldSocket, gun.Item ? gun.Item.TypeID : 0));
        if (melee != null) listW.Add(((int)melee.handheldSocket, melee.Item ? melee.Item.TypeID : 0));
        writer.Put(listW.Count);
        foreach (var p in listW)
        {
            writer.Put(p.slot);
            writer.Put(p.tid);
        }

        
        string faceJson = null;
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        writer.Put(!string.IsNullOrEmpty(faceJson));
        if (!string.IsNullOrEmpty(faceJson)) writer.Put(faceJson);


        
        var modelName = AIName.NormalizePrefabName(cmc.characterModel ? cmc.characterModel.name : null);

        var iconType = 0;
        var showName = false;
        try
        {
            var pr = cmc.characterPreset;
            if (pr)
            {
                var e = (CharacterIconTypes)iconType;
                
                if (e == CharacterIconTypes.none && pr.GetCharacterIcon() != null)
                    iconType = (int)AIName.FR_IconType(pr);

                
                e = (CharacterIconTypes)iconType;
                if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                    showName = true;
            }
        }
        catch
        {
            
        }

        writer.Put(!string.IsNullOrEmpty(modelName));
        if (!string.IsNullOrEmpty(modelName)) writer.Put(modelName);
        writer.Put(iconType);
        writer.Put(showName); 

        
        string displayName = null;
        try
        {
            var preset = cmc.characterPreset;
            if (preset) displayName = preset.Name; 
        }
        catch
        {
        }

        writer.Put(!string.IsNullOrEmpty(displayName)); 
        if (!string.IsNullOrEmpty(displayName))
            writer.Put(displayName);


        Debug.Log($"[AI-SEND] ver={AI_LOADOUT_VER} aiId={aiId} model='{modelName}' icon={iconType} showName={showName}");

        CoopTool.BroadcastReliable(writer);

        if (iconType == (int)CharacterIconTypes.none)
            AIRequest.Instance.Server_TryRebroadcastIconLater(aiId, cmc);
    }

    public UniTask Client_ApplyAiLoadout(
        int aiId,
        List<(int slot, int tid)> equips,
        List<(int slot, int tid)> weapons,
        string faceJson,
        string modelName,
        int iconType,
        bool showName)
    {
        return Client_ApplyAiLoadout(
            aiId, equips, weapons, faceJson, modelName, iconType, showName, null);
    }

    public async UniTask Client_ApplyAiLoadout(
        int aiId,
        List<(int slot, int tid)> equips,
        List<(int slot, int tid)> weapons,
        string faceJson,
        string modelName,
        int iconType,
        bool showName,
        string displayNameFromHost)
    {
        if (!AITool.aiById.TryGetValue(aiId, out var cmc) || !cmc) return;

        
        CharacterModel prefab = null;
        if (!string.IsNullOrEmpty(modelName))
            prefab = AIName.FindCharacterModelByName_Any(modelName);
        if (!prefab)
            try
            {
                var pr = cmc.characterPreset;
                if (pr) prefab = FR_CharacterModel(pr);
            }
            catch
            {
            }

        try
        {
            var cur = cmc.characterModel;
            var curName = AIName.NormalizePrefabName(cur ? cur.name : null);
            var tgtName = AIName.NormalizePrefabName(prefab ? prefab.name : null);
            if (prefab && !string.Equals(curName, tgtName, StringComparison.OrdinalIgnoreCase))
            {
                var inst = Object.Instantiate(prefab);
                Debug.Log($"[AI-APPLY] aiId={aiId} SetCharacterModel -> '{tgtName}' (cur='{curName}')");
                cmc.SetCharacterModel(inst);
            }
        }
        catch
        {
        }

        
        var model = cmc.characterModel;
        var guard = 0;
        while (!model && guard++ < 120)
        {
            await UniTask.Yield();
            model = cmc.characterModel;
        }

        if (!model) return;

        
        try
        {
            
            var preset = cmc.characterPreset;
            if (preset)
            {
                try
                {
                    AIName.FR_IconType(preset) = (CharacterIconTypes)iconType;
                }
                catch
                {
                }

                try
                {
                    preset.showName = showName;
                }
                catch
                {
                }
            }

            
            var sprite = AIName.ResolveIconSprite(iconType);

            
            var tries = 0;
            while (sprite == null && tries++ < 5)
            {
                await UniTask.Yield();
                sprite = AIName.ResolveIconSprite(iconType);
            }

            
            var displayName = showName ? displayNameFromHost : null;

            await AIName.RefreshNameIconWithRetries(cmc, iconType, showName, displayNameFromHost);


            Debug.Log($"[AI-APPLY] aiId={aiId} icon={(CharacterIconTypes)iconType} showName={showName} name='{displayName ?? "(null)"}'");
            Debug.Log(
                $"[NOW AI] aiId={aiId} icon={Traverse.Create(cmc.characterPreset).Field<CharacterIconTypes>("characterIconType").Value} showName={showName} name='{Traverse.Create(cmc.characterPreset).Field<string>("nameKey").Value ?? "(null)"}'");
        }
        catch
        {
        }

        
        foreach (var (slotHash, typeId) in equips)
        {
            if (typeId <= 0) continue;

            var item = await COOPManager.GetItemAsync(typeId);
            if (!item) continue;

            if (slotHash == CharacterEquipmentController.armorHash || slotHash == 100)
                COOPManager.ChangeArmorModel(model, item);
            else if (slotHash == CharacterEquipmentController.helmatHash || slotHash == 200)
                COOPManager.ChangeHelmatModel(model, item);
            else if (slotHash == CharacterEquipmentController.faceMaskHash || slotHash == 300)
                COOPManager.ChangeFaceMaskModel(model, item);
            else if (slotHash == CharacterEquipmentController.backpackHash || slotHash == 400)
                COOPManager.ChangeBackpackModel(model, item);
            else if (slotHash == CharacterEquipmentController.headsetHash || slotHash == 500)
                COOPManager.ChangeHeadsetModel(model, item);
        }

        

        
        
        COOPManager.ChangeWeaponModel(model, null, HandheldSocketTypes.normalHandheld);
        COOPManager.ChangeWeaponModel(model, null, HandheldSocketTypes.meleeWeapon);
        COOPManager.ChangeWeaponModel(model, null, HandheldSocketTypes.leftHandSocket);
        await UniTask.NextFrame(); 

        foreach (var (slotHash, typeId) in weapons)
        {
            if (typeId <= 0) continue;

            var item = await COOPManager.GetItemAsync(typeId);
            if (!item) continue;

            
            var socket = Enum.IsDefined(typeof(HandheldSocketTypes), slotHash)
                ? (HandheldSocketTypes)slotHash
                : HandheldSocketTypes.normalHandheld;

            
            try
            {
                var ag = item.ActiveAgent;
                if (ag && ag.gameObject) Object.Destroy(ag.gameObject);
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

            
            COOPManager.ChangeWeaponModel(model, null, socket);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            
            try
            {
                COOPManager.ChangeWeaponModel(model, item, socket);
            }
            catch (Exception e)
            {
                var msg = e.Message ?? string.Empty;
                if (msg.Contains("已有agent") || msg.IndexOf("pickup agent", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    
                    try
                    {
                        var ag2 = item.ActiveAgent;
                        if (ag2 && ag2.gameObject) Object.Destroy(ag2.gameObject);
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

                    COOPManager.ChangeWeaponModel(model, null, socket);
                    await UniTask.NextFrame();

                    COOPManager.ChangeWeaponModel(model, item, socket);
                }
                else
                {
                    throw; 
                }
            }
        }

        AITool.EnsureMagicBlendBound(cmc);

        if (!string.IsNullOrEmpty(faceJson)) CustomFace.ApplyFaceJsonToModel(model, faceJson);
    }


    public void Server_BroadcastAiTransforms()
    {
        if (!IsServer || AITool.aiById.Count == 0) return;

        AITransformMessage.Server_BroadcastTransforms();
    }

    public void Server_BroadcastAiAnimations()
    {
        if (!IsServer || AITool.aiById == null || AITool.aiById.Count == 0) return;

        AIAnimationMessage.Server_BroadcastAnimations();
    }
}