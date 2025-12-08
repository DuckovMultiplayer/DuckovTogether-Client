















using System.Collections;
using Duckov.UI;
using Duckov.Utilities;
using TMPro;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod;

public static class AIName
{
    private static readonly Dictionary<int, string> _aiFaceJsonById = new();


    public static readonly HashSet<int> _nameIconSealed = new();

    
    public static readonly HashSet<int> _iconRebroadcastScheduled = new();


    public static readonly AccessTools.FieldRef<CharacterRandomPreset, CharacterIconTypes>
        FR_IconType = AccessTools.FieldRefAccess<CharacterRandomPreset, CharacterIconTypes>("characterIconType");

    private static ModBehaviourF Service => ModBehaviourF.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;
    private static Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private static Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private static Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public static string NormalizePrefabName(string n)
    {
        if (string.IsNullOrEmpty(n)) return n;
        n = n.Trim();
        const string clone = "(Clone)";
        if (n.EndsWith(clone)) n = n.Substring(0, n.Length - clone.Length).Trim();
        return n;
    }

    public static CharacterModel FindCharacterModelByName_Any(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        name = NormalizePrefabName(name);

        
        
        foreach (var m in Resources.FindObjectsOfTypeAll<CharacterModel>())
        {
            if (!m) continue;
            if (m.gameObject.scene.IsValid()) continue; 
            if (string.Equals(NormalizePrefabName(m.name), name, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        
        try
        {
            foreach (var m in Resources.LoadAll<CharacterModel>(""))
            {
                if (!m) continue;
                if (string.Equals(NormalizePrefabName(m.name), name, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
        }
        catch
        {
            
        }

        
        foreach (var m in GameObject.FindObjectsOfType<CharacterModel>())
        {
            if (!m) continue;
            if (string.Equals(NormalizePrefabName(m.name), name, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        return null;
    }

    public static void ReapplyFaceIfKnown(CharacterMainControl cmc)
    {
        if (!cmc || IsServer) return;
        var aiId = -1;
        foreach (var kv in AITool.aiById)
            if (kv.Value == cmc)
            {
                aiId = kv.Key;
                break;
            }

        if (aiId < 0) return;

        if (_aiFaceJsonById.TryGetValue(aiId, out var json) && !string.IsNullOrEmpty(json))
            CustomFace.ApplyFaceJsonToModel(cmc.characterModel, json);
    }

    
    public static void Client_ResetNameIconSeal_OnLevelInit()
    {
        if (!IsServer) _nameIconSealed.Clear();
        if (IsServer) return;
        foreach (var tag in GameObject.FindObjectsOfType<NetAiTag>())
        {
            var cmc = tag ? tag.GetComponent<CharacterMainControl>() : null;
            if (!cmc)
            {
                GameObject.Destroy(tag);
                continue;
            }

            if (!AITool.IsRealAI(cmc)) GameObject.Destroy(tag);
        }
    }

    public static Sprite ResolveIconSprite(int iconType)
    {
        switch ((CharacterIconTypes)iconType)
        {
            case CharacterIconTypes.none: return null;
            case CharacterIconTypes.elete: return GameplayDataSettings.UIStyle.EleteCharacterIcon;
            case CharacterIconTypes.pmc: return GameplayDataSettings.UIStyle.PmcCharacterIcon;
            case CharacterIconTypes.boss: return GameplayDataSettings.UIStyle.BossCharacterIcon;
            case CharacterIconTypes.merchant: return GameplayDataSettings.UIStyle.MerchantCharacterIcon;
            case CharacterIconTypes.pet: return GameplayDataSettings.UIStyle.PetCharacterIcon;
            default: return null;
        }
    }

    
    public static async UniTask RefreshNameIconWithRetries(CharacterMainControl cmc, int iconType, bool showName, string displayNameFromHost)
    {
        if (!cmc) return;

        
        
        
        
        

        
        
        
        
        
        
        

        
        
        
        
        
        
        

        
        

        
        

        
        
        
        
        
        

        
        
        
        
        
        
        
        
        
        
        
        

        try
        {
            var preset = cmc.characterPreset;
            if (preset)
            {
                try
                {
                    FR_IconType(preset) = (CharacterIconTypes)iconType;
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

                try
                {
                    Traverse.Create(preset).Field<string>("nameKey").Value = displayNameFromHost ?? string.Empty;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        
        var h = cmc.Health;

        
        var miGet = AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
        var miRefresh = AccessTools.DeclaredMethod(typeof(HealthBar), "RefreshCharacterIcon", Type.EmptyTypes);

        HealthBar hb = null;
        for (var i = 0; i < 30; i++) 
            try
            {
                if (miGet != null && HealthBarManager.Instance != null && h != null)
                    hb = (HealthBar)miGet.Invoke(HealthBarManager.Instance, new object[] { h });

                Traverse.Create(hb).Field<Image>("levelIcon").Value.gameObject.SetActive(true);
                Traverse.Create(hb).Field<TextMeshProUGUI>("nameText").Value.gameObject.SetActive(true);

                Traverse.Create(hb).Field<Image>("levelIcon").Value.sprite = ResolveIconSprite(iconType);
                Traverse.Create(hb).Field<TextMeshProUGUI>("nameText").Value.text = displayNameFromHost;

                if (hb != null)
                {
                    var tag = cmc.GetComponent<NetAiTag>() ?? cmc.gameObject.AddComponent<NetAiTag>();
                    tag.iconTypeOverride = iconType;
                    tag.showNameOverride = showName
                                           || (CharacterIconTypes)iconType == CharacterIconTypes.boss
                                           || (CharacterIconTypes)iconType == CharacterIconTypes.elete;
                    tag.nameOverride = displayNameFromHost ?? string.Empty;

                    Debug.Log(
                        $"[AI_icon_Name 10s] {cmc.GetComponent<NetAiTag>().aiId} {cmc.characterPreset.Name} {cmc.characterPreset.GetCharacterIcon().name}");
                    break; 
                }
            }
            catch
            {
            }
    }


    public static IEnumerator IconRebroadcastRoutine(int aiId, CharacterMainControl cmc)
    {
        yield return new WaitForSeconds(0.6f); 

        try
        {
            if (!IsServer || !cmc) yield break;

            var pr = cmc.characterPreset;
            var iconType = 0;
            var showName = false;

            if (pr)
            {
                try
                {
                    iconType = (int)FR_IconType(pr);
                }
                catch
                {
                }

                try
                {
                    
                    if (iconType == 0 && pr.GetCharacterIcon() != null)
                        iconType = (int)FR_IconType(pr);
                }
                catch
                {
                }
            }

            var e = (CharacterIconTypes)iconType;
            if (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete)
                showName = true;

            
            if (iconType != 0 || showName)
                Server_BroadcastAiNameIcon(aiId, cmc);
        }
        finally
        {
            _iconRebroadcastScheduled.Remove(aiId);
        }
    }

    private static void Server_PeriodicNameIconSync()
    {
        foreach (var kv in AITool.aiById) 
        {
            var aiId = kv.Key;
            var cmc = kv.Value;
            if (!cmc) continue;

            var pr = cmc.characterPreset;
            if (!pr) continue;

            var iconType = 0;
            var showName = false;

            try
            {
                iconType = (int)FR_IconType(pr);
            }
            catch
            {
            }

            try
            {
                showName = pr.showName;
            }
            catch
            {
            }

            var e = (CharacterIconTypes)iconType;
            
            if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                showName = true;

            
            if (e != CharacterIconTypes.none || showName)
            {
                Debug.Log($"[AI-REBROADCAST-10s] aiId={aiId} icon={e} showName={showName}");
                COOPManager.AIHandle.Server_BroadcastAiLoadout(aiId, cmc); 
            }
        }
    }

    
    public static void Client_PeriodicNameIconRefresh()
    {
        foreach (var kv in AITool.aiById)
        {
            var cmc = kv.Value;
            if (!cmc) continue;

            var pr = cmc.characterPreset;
            if (!pr) continue;

            var iconType = 0;
            var showName = false;
            string displayName = null;

            try
            {
                iconType = (int)FR_IconType(pr);
            }
            catch
            {
            }

            try
            {
                showName = pr.showName;
            }
            catch
            {
            }

            try
            {
                displayName = pr.DisplayName;
            }
            catch
            {
            }

            var e = (CharacterIconTypes)iconType;
            if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                showName = true;

            
            if (e == CharacterIconTypes.none && !showName) continue;

            
            RefreshNameIconWithRetries(cmc, iconType, showName, displayName).Forget();
        }
    }

    public static void Server_BroadcastAiNameIcon(int aiId, CharacterMainControl cmc)
    {
        if (!networkStarted || !IsServer || aiId == 0 || !cmc) return;

        var iconType = 0;
        var showName = false;
        string displayName = null;

        try
        {
            var pr = cmc.characterPreset;
            if (pr)
            {
                
                try
                {
                    iconType = (int)FR_IconType(pr);
                }
                catch
                {
                }

                try
                {
                    if (iconType == 0 && pr.GetCharacterIcon() != null) 
                        iconType = (int)FR_IconType(pr);
                }
                catch
                {
                }

                
                try
                {
                    showName = pr.showName;
                }
                catch
                {
                }

                var e = (CharacterIconTypes)iconType;
                if (!showName && (e == CharacterIconTypes.boss || e == CharacterIconTypes.elete))
                    showName = true;

                
                try
                {
                    displayName = pr.Name;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        Debug.Log($"[Server AIIcon_Name 10s] AI:{aiId} {cmc.characterPreset.Name} Icon{FR_IconType(cmc.characterPreset)}");
        var msg = new Net.HybridNet.AINameIconMessage
        {
            AiId = aiId,
            IconType = iconType,
            ShowName = showName,
            DisplayName = displayName
        };
        Net.HybridNet.HybridNetCore.Send(msg);
    }
}