















using Duckov.Scenes;
using Duckov.Utilities;
using ItemStatsSystem.Items;
using UnityEngine.SceneManagement;

namespace EscapeFromDuckovCoopMod;

public class LocalPlayerManager : MonoBehaviour
{
    public static LocalPlayerManager Instance;

    
    public string _lastGoodFaceJson;

    public readonly Dictionary<string, (ItemAgent_Gun gun, Transform muzzle)> _gunCacheByShooter = new();

    
    public readonly Dictionary<int, GameObject> _muzzleFxCacheByWeaponType = new();

    
    public readonly Dictionary<int, Projectile> _projCacheByWeaponType = new();

    
    internal bool _cliCorpseTreeReported;

    
    internal bool _cliInEnsureSelfDeathEmit;

    private bool _cliSelfDeathFired;

    private ModBehaviourF Service => ModBehaviourF.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;

    private NetPeer connectedPeer => Service?.connectedPeer;

    
    private bool networkStarted => Service != null && Service.networkStarted;
    private int port => Service?.port ?? 0;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public void Init()
    {
        Instance = this;
    }

    public void InitializeLocalPlayer()
    {
        var bool1 = ComputeIsInGame(out var ids);
        Service.localPlayerStatus = new PlayerStatus
        {
            EndPoint = IsServer ? $"Host:{port}" : $"Client:{Guid.NewGuid().ToString().Substring(0, 8)}",
            PlayerName = IsServer ? "Host" : "Client",
            Latency = 0,
            IsInGame = bool1,
            LastIsInGame = bool1,
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            SceneId = ids,
            CustomFaceJson = CustomFace.LoadLocalCustomFaceJson()
        };
    }

    public bool ComputeIsInGame(out string sceneId)
    {
        sceneId = null;

        
        var lm = LevelManager.Instance;
        if (lm == null || lm.MainCharacter == null)
        {
            return false;
        }

        
        
        try
        {
            var core = MultiSceneCore.Instance;
            if (core != null)
            {
                
                
                var active = SceneManager.GetActiveScene();
                if (active.IsValid())
                {
                    
                    var idFromBuild = SceneInfoCollection.GetSceneID(active.buildIndex);
                    if (!string.IsNullOrEmpty(idFromBuild))
                    {
                        sceneId = idFromBuild;
                    }
                    else
                    {
                        sceneId = active.name; 
                    }
                }
            }
        }
        catch
        {
            
        }

        
        if (string.IsNullOrEmpty(sceneId))
            
            
        {
            sceneId = SceneInfoCollection.BaseSceneID; 
        }

        
        return !string.IsNullOrEmpty(sceneId);
    }

    public List<EquipmentSyncData> GetLocalEquipment()
    {
        var equipmentList = new List<EquipmentSyncData>();
        var equipmentController = CharacterMainControl.Main?.EquipmentController;
        if (equipmentController == null)
        {
            return equipmentList;
        }

        var slotNames = new[] { "armorSlot", "helmatSlot", "faceMaskSlot", "backpackSlot", "headsetSlot" };
        var slotHashes = new[]
        {
            CharacterEquipmentController.armorHash, CharacterEquipmentController.helmatHash, CharacterEquipmentController.faceMaskHash,
            CharacterEquipmentController.backpackHash, CharacterEquipmentController.headsetHash
        };

        for (var i = 0; i < slotNames.Length; i++)
        {
            try
            {
                var slotField = Traverse.Create(equipmentController).Field<Slot>(slotNames[i]);
                if (slotField.Value == null)
                {
                    continue;
                }

                var slot = slotField.Value;
                var itemId = slot?.Content != null ? slot.Content.TypeID.ToString() : "";
                equipmentList.Add(new EquipmentSyncData { SlotHash = slotHashes[i], ItemId = itemId });
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取槽位 {slotNames[i]} 时发生错误: {ex.Message}");
            }
        }

        return equipmentList;
    }

    public List<WeaponSyncData> GetLocalWeapons()
    {
        var weaponList = new List<WeaponSyncData>();
        var mainControl = CharacterMainControl.Main;
        if (mainControl == null)
        {
            return weaponList;
        }

        try
        {
            var rangedWeapon = mainControl.GetGun();
            weaponList.Add(new WeaponSyncData
            {
                SlotHash = (int)HandheldSocketTypes.normalHandheld,
                ItemId = rangedWeapon != null ? rangedWeapon.Item.TypeID.ToString() : ""
            });

            var meleeWeapon = mainControl.GetMeleeWeapon();
            weaponList.Add(new WeaponSyncData
            {
                SlotHash = (int)HandheldSocketTypes.meleeWeapon,
                ItemId = meleeWeapon != null ? meleeWeapon.Item.TypeID.ToString() : ""
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取本地武器数据时发生错误: {ex.Message}");
        }

        return weaponList;
    }

    public void Main_OnHoldAgentChanged(DuckovItemAgent obj)
    {
        if (obj == null)
        {
            return;
        }

        var itemId = obj.Item?.TypeID.ToString() ?? "";
        var slotHash = obj.handheldSocket;

        
        var gunAgent = obj as ItemAgent_Gun;
        if (gunAgent != null)
        {
            int typeId;
            if (int.TryParse(itemId, out typeId))
            {
                
                var setting = gunAgent.GunItemSetting; 
                var pfb = setting != null && setting.bulletPfb != null
                    ? setting.bulletPfb
                    : GameplayDataSettings.Prefabs.DefaultBullet;

                _projCacheByWeaponType[typeId] = pfb;
                _muzzleFxCacheByWeaponType[typeId] = setting != null ? setting.muzzleFxPfb : null;
            }
        }

        
        var weaponData = new WeaponSyncData
        {
            SlotHash = (int)slotHash,
            ItemId = itemId
        };
        SendLocalPlayerStatus.Instance.SendWeaponUpdate(weaponData);
    }

    public void ModBehaviour_onSlotContentChanged(Slot obj)
    {
        if (!networkStarted || Service.localPlayerStatus == null || !Service.localPlayerStatus.IsInGame)
        {
            return;
        }

        if (obj == null)
        {
            return;
        }

        var itemId1 = "";
        if (obj.Content != null)
        {
            itemId1 = obj.Content.TypeID.ToString();
        }

        
        var slotHash1 = obj.GetHashCode();
        if (obj.Key == "Helmat")
        {
            slotHash1 = 200;
        }

        if (obj.Key == "Armor")
        {
            slotHash1 = 100;
        }

        if (obj.Key == "FaceMask")
        {
            slotHash1 = 300;
        }

        if (obj.Key == "Backpack")
        {
            slotHash1 = 400;
        }

        if (obj.Key == "Head")
        {
            slotHash1 = 500;
        }

        var equipmentData1 = new EquipmentSyncData { SlotHash = slotHash1, ItemId = itemId1 };
        SendLocalPlayerStatus.Instance.SendEquipmentUpdate(equipmentData1);
    }

    public void UpdatePlayerStatuses()
    {
        if (netManager == null || !netManager.IsRunning || Service.localPlayerStatus == null)
        {
            return;
        }

        var bool1 = ComputeIsInGame(out var ids);
        var currentIsInGame = bool1;
        var levelManager = LevelManager.Instance;

        if (Service.localPlayerStatus.IsInGame != currentIsInGame || Service.localPlayerStatus.SceneId != ids)
        {
            Service.localPlayerStatus.IsInGame = currentIsInGame;
            Service.localPlayerStatus.LastIsInGame = currentIsInGame;
            Service.localPlayerStatus.SceneId = ids;

            if (levelManager != null && levelManager.MainCharacter != null)
            {
                Service.localPlayerStatus.Position = levelManager.MainCharacter.transform.position;
                Service.localPlayerStatus.Rotation = levelManager.MainCharacter.modelRoot.transform.rotation;
                Service.localPlayerStatus.CustomFaceJson = CustomFace.LoadLocalCustomFaceJson();
            }

            if (currentIsInGame && levelManager != null)
                
            {
                SceneNet.Instance.TrySendSceneReadyOnce();
            }

            if (!IsServer)
            {
                Send_ClientStatus.Instance.SendClientStatusUpdate();
            }
            else
            {
                SendLocalPlayerStatus.Instance.SendPlayerStatusUpdate();
            }
        }
        else if (currentIsInGame && levelManager != null && levelManager.MainCharacter != null)
        {
            Service.localPlayerStatus.Position = levelManager.MainCharacter.transform.position;
            Service.localPlayerStatus.Rotation = levelManager.MainCharacter.modelRoot.transform.rotation;
            Service.localPlayerStatus.SceneId = ids;
        }

        if (currentIsInGame)
        {
            Service.localPlayerStatus.CustomFaceJson = CustomFace.LoadLocalCustomFaceJson();
            Service.localPlayerStatus.SceneId = ids;
        }
    }

    public bool IsAlive(CharacterMainControl cmc)
    {
        if (!cmc)
        {
            return false;
        }

        try
        {
            return cmc.Health != null && cmc.Health.CurrentHealth > 0.001f;
        }
        catch
        {
            return false;
        }
    }


    public void Client_EnsureSelfDeathEvent(Health h, CharacterMainControl cmc)
    {
        if (!h || !cmc)
        {
            return;
        }

        var cur = 1f;
        try
        {
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        
        if (cur > 1e-3f)
        {
            _cliSelfDeathFired = false;
            _cliCorpseTreeReported = false; 
            _cliInEnsureSelfDeathEmit = false; 
            return;
        }

        
        if (_cliSelfDeathFired)
        {
            return;
        }

        try
        {
            var di = new DamageInfo
            {
                isFromBuffOrEffect = false,
                damageValue = 0f,
                finalDamage = 0f,
                damagePoint = cmc.transform.position,
                damageNormal = Vector3.up,
                fromCharacter = null
            };

            
            _cliInEnsureSelfDeathEmit = true;

            
            h.OnDeadEvent?.Invoke(di);

            _cliSelfDeathFired = true;
        }
        finally
        {
            _cliInEnsureSelfDeathEmit = false; 
        }
    }

    public void UpdateRemoteCharacters()
    {
        if (IsServer)
        {
            foreach (var kvp in remoteCharacters)
            {
                var go = kvp.Value;
                if (!go)
                {
                    continue;
                }

                NetInterpUtil.Attach(go); 
            }
        }
        else
        {
            foreach (var kvp in clientRemoteCharacters)
            {
                var go = kvp.Value;
                if (!go)
                {
                    continue;
                }

                NetInterpUtil.Attach(go);
            }
        }
    }
}

public class PlayerStatus
{
    public string SceneId;
    private ModBehaviourF Service => ModBehaviourF.Instance;

    public int Latency { get; set; }
    public bool IsInGame { get; set; }
    public string EndPoint { get; set; }
    public string PlayerName { get; set; }
    public bool LastIsInGame { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public string CustomFaceJson { get; set; }
    public string ClientReportedId { get; set; }
    
    
    
    
    public string RealNetworkId { get; set; }
    
    public List<EquipmentSyncData> EquipmentList { get; set; } = new();
    public List<WeaponSyncData> WeaponList { get; set; } = new();
    
    public bool IsSpeaking { get; set; }
    public bool IsMuted { get; set; }
}