















using Duckov.Buffs;
using ItemStatsSystem;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class COOPManager
{
    public static HostPlayerApply HostPlayer_Apply;

    public static ClientPlayerApply ClientPlayer_Apply;
    public static LootNet LootNet;
    public static AIHandle AIHandle;
    public static Door Door;
    public static Destructible destructible;
    public static GrenadeM GrenadeM;
    public static HurtM HurtM;
    public static WeaponHandle WeaponHandle;
    public static Weather Weather;
    public static ClientHandle ClientHandle;
    public static PublicHandleUpdate PublicHandleUpdate;
    public static ItemHandle ItemHandle;
    public static AIHealth AIHealth;
    public static Buff_ Buff;
    public static WeaponRequest WeaponRequest;
    public static HostHandle Host_Handle;
    public static ItemRequest ItemRequest;
    private NetService Service => NetService.Instance;

    public static void InitManager()
    {
        HostPlayer_Apply = new HostPlayerApply();
        ClientPlayer_Apply = new ClientPlayerApply();
        LootNet = new LootNet();
        AIHandle = new AIHandle();
        Door = new Door();
        destructible = new Destructible();
        GrenadeM = new GrenadeM();
        HurtM = new HurtM();
        WeaponHandle = new WeaponHandle();
        Weather = new Weather();
        ClientHandle = new ClientHandle();
        PublicHandleUpdate = new PublicHandleUpdate();
        ItemHandle = new ItemHandle();
        AIHealth = new AIHealth();
        Buff = new Buff_();
        WeaponRequest = new WeaponRequest();
        Host_Handle = new HostHandle();
        ItemRequest = new ItemRequest();
    }


    public static async Task<Item> GetItemAsync(int itemId)
    {
        
        return await ItemAssetsCollection.InstantiateAsync(itemId);
    }

    public static void ChangeArmorModel(CharacterModel characterModel, Item item)
    {
        if (item != null)
        {
            var slot = characterModel.characterMainControl.CharacterItem.Slots["Armor"];
            Traverse.Create(slot).Field<Item>("content").Value = item;
        }

        if (item == null)
        {
            var socket = characterModel.ArmorSocket;
            for (var i = socket.childCount - 1; i >= 0; i--) Object.Destroy(socket.GetChild(i).gameObject);
            return;
        }

        var faceMaskSocket = characterModel.ArmorSocket;
        var itemAgent = item.AgentUtilities.CreateAgent(CharacterEquipmentController.equipmentModelHash, ItemAgent.AgentTypes.equipment);
        if (itemAgent == null)
        {
            Debug.LogError("生成的装备Item没有装备agent，Item名称：" + item.gameObject.name);
            return;
        }

        if (itemAgent != null)
        {
            itemAgent.transform.SetParent(faceMaskSocket, false);
            itemAgent.transform.localRotation = Quaternion.identity;
            itemAgent.transform.localPosition = Vector3.zero;
        }
    }


    public static void ChangeHelmatModel(CharacterModel characterModel, Item item)
    {
        
        if (characterModel == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHelmatModel: characterModel 为 null");
            return;
        }

        if (characterModel.characterMainControl == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHelmatModel: characterMainControl 为 null");
            return;
        }

        if (characterModel.characterMainControl.CharacterItem == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHelmatModel: CharacterItem 为 null");
            return;
        }

        if (item != null)
        {
            var slot = characterModel.characterMainControl.CharacterItem.Slots["Helmat"];
            Traverse.Create(slot).Field<Item>("content").Value = item;
        }

        if (item == null)
        {
            var socket = characterModel.HelmatSocket;
            if (socket != null)
            {
                for (var i = socket.childCount - 1; i >= 0; i--) Object.Destroy(socket.GetChild(i).gameObject);
            }

            if (characterModel.CustomFace != null)
            {
                if (characterModel.CustomFace.hairSocket != null)
                    characterModel.CustomFace.hairSocket.gameObject.SetActive(true);
                if (characterModel.CustomFace.mouthPart != null && characterModel.CustomFace.mouthPart.socket != null)
                    characterModel.CustomFace.mouthPart.socket.gameObject.SetActive(true);
            }
            return;
        }

        if (characterModel.CustomFace != null)
        {
            if (characterModel.CustomFace.hairSocket != null)
                characterModel.CustomFace.hairSocket.gameObject.SetActive(false);
            if (characterModel.CustomFace.mouthPart != null && characterModel.CustomFace.mouthPart.socket != null)
                characterModel.CustomFace.mouthPart.socket.gameObject.SetActive(false);
        }

        var faceMaskSocket = characterModel.HelmatSocket;
        if (faceMaskSocket == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHelmatModel: HelmatSocket 为 null");
            return;
        }

        var itemAgent = item.AgentUtilities.CreateAgent(CharacterEquipmentController.equipmentModelHash, ItemAgent.AgentTypes.equipment);
        if (itemAgent == null)
        {
            Debug.LogError("生成的装备Item没有装备agent，Item名称：" + item.gameObject.name);
            return;
        }

        if (itemAgent != null)
        {
            itemAgent.transform.SetParent(faceMaskSocket, false);
            itemAgent.transform.localRotation = Quaternion.identity;
            itemAgent.transform.localPosition = Vector3.zero;
        }
    }

    public static void ChangeHeadsetModel(CharacterModel characterModel, Item item)
    {
        
        if (characterModel == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHeadsetModel: characterModel 为 null");
            return;
        }

        if (characterModel.characterMainControl == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHeadsetModel: characterMainControl 为 null");
            return;
        }

        if (characterModel.characterMainControl.CharacterItem == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHeadsetModel: CharacterItem 为 null");
            return;
        }

        if (item != null)
        {
            var slot = characterModel.characterMainControl.CharacterItem.Slots["Headset"];
            Traverse.Create(slot).Field<Item>("content").Value = item;
        }

        if (item == null)
        {
            var socket = characterModel.HelmatSocket;
            if (socket != null)
            {
                for (var i = socket.childCount - 1; i >= 0; i--) Object.Destroy(socket.GetChild(i).gameObject);
            }

            if (characterModel.CustomFace != null)
            {
                if (characterModel.CustomFace.hairSocket != null)
                    characterModel.CustomFace.hairSocket.gameObject.SetActive(true);
                if (characterModel.CustomFace.mouthPart != null && characterModel.CustomFace.mouthPart.socket != null)
                    characterModel.CustomFace.mouthPart.socket.gameObject.SetActive(true);
            }
            return;
        }

        if (characterModel.CustomFace != null)
        {
            if (characterModel.CustomFace.hairSocket != null)
                characterModel.CustomFace.hairSocket.gameObject.SetActive(false);
            if (characterModel.CustomFace.mouthPart != null && characterModel.CustomFace.mouthPart.socket != null)
                characterModel.CustomFace.mouthPart.socket.gameObject.SetActive(false);
        }

        var faceMaskSocket = characterModel.HelmatSocket;
        if (faceMaskSocket == null)
        {
            Debug.LogWarning("[COOPManager] ChangeHeadsetModel: HelmatSocket 为 null");
            return;
        }

        var itemAgent = item.AgentUtilities.CreateAgent(CharacterEquipmentController.equipmentModelHash, ItemAgent.AgentTypes.equipment);
        if (itemAgent == null)
        {
            Debug.LogError("生成的装备Item没有装备agent，Item名称：" + item.gameObject.name);
            return;
        }

        if (itemAgent != null)
        {
            itemAgent.transform.SetParent(faceMaskSocket, false);
            itemAgent.transform.localRotation = Quaternion.identity;
            itemAgent.transform.localPosition = Vector3.zero;
        }
    }

    public static void ChangeBackpackModel(CharacterModel characterModel, Item item)
    {
        
        if (characterModel == null)
        {
            Debug.LogWarning("[COOPManager] ChangeBackpackModel: characterModel 为 null");
            return;
        }

        if (characterModel.characterMainControl == null)
        {
            Debug.LogWarning("[COOPManager] ChangeBackpackModel: characterMainControl 为 null");
            return;
        }

        if (characterModel.characterMainControl.CharacterItem == null)
        {
            Debug.LogWarning("[COOPManager] ChangeBackpackModel: CharacterItem 为 null");
            return;
        }

        if (item != null)
        {
            var slot = characterModel.characterMainControl.CharacterItem.Slots["Backpack"];
            Traverse.Create(slot).Field<Item>("content").Value = item;
        }

        if (item == null)
        {
            var socket = characterModel.BackpackSocket;
            if (socket != null)
            {
                for (var i = socket.childCount - 1; i >= 0; i--) Object.Destroy(socket.GetChild(i).gameObject);
            }
            return;
        }

        var faceMaskSocket = characterModel.BackpackSocket;
        if (faceMaskSocket == null)
        {
            Debug.LogWarning("[COOPManager] ChangeBackpackModel: BackpackSocket 为 null");
            return;
        }

        var itemAgent = item.AgentUtilities.CreateAgent(CharacterEquipmentController.equipmentModelHash, ItemAgent.AgentTypes.equipment);
        if (itemAgent == null)
        {
            Debug.LogError("生成的装备Item没有装备agent，Item名称：" + item.gameObject.name);
            return;
        }

        if (itemAgent != null)
        {
            itemAgent.transform.SetParent(faceMaskSocket, false);
            itemAgent.transform.localRotation = Quaternion.identity;
            itemAgent.transform.localPosition = Vector3.zero;
        }
    }


    public static void ChangeFaceMaskModel(CharacterModel characterModel, Item item)
    {
        
        if (characterModel == null)
        {
            Debug.LogWarning("[COOPManager] ChangeFaceMaskModel: characterModel 为 null");
            return;
        }

        if (characterModel.characterMainControl == null)
        {
            Debug.LogWarning("[COOPManager] ChangeFaceMaskModel: characterMainControl 为 null");
            return;
        }

        if (characterModel.characterMainControl.CharacterItem == null)
        {
            Debug.LogWarning("[COOPManager] ChangeFaceMaskModel: CharacterItem 为 null");
            return;
        }

        if (item != null)
        {
            var slot = characterModel.characterMainControl.CharacterItem.Slots["FaceMask"];
            Traverse.Create(slot).Field<Item>("content").Value = item;
        }

        if (item == null)
        {
            var socket = characterModel.FaceMaskSocket;
            if (socket != null)
            {
                for (var i = socket.childCount - 1; i >= 0; i--) Object.Destroy(socket.GetChild(i).gameObject);
            }
            return;
        }

        var faceMaskSocket = characterModel.FaceMaskSocket;
        if (faceMaskSocket == null)
        {
            Debug.LogWarning("[COOPManager] ChangeFaceMaskModel: FaceMaskSocket 为 null");
            return;
        }

        var itemAgent = item.AgentUtilities.CreateAgent(CharacterEquipmentController.equipmentModelHash, ItemAgent.AgentTypes.equipment);
        if (itemAgent == null)
        {
            Debug.LogError("生成的装备Item没有装备agent，Item名称：" + item.gameObject.name);
            return;
        }

        if (itemAgent != null)
        {
            itemAgent.transform.SetParent(faceMaskSocket, false);
            itemAgent.transform.localRotation = Quaternion.identity;
            itemAgent.transform.localPosition = Vector3.zero;
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    

    

    
    
    
    
    
    

    
    
    

    


    public static async Task<Grenade> GetGrenadePrefabByItemIdAsync(int itemId)
    {
        Item item = null;
        try
        {
            item = await GetItemAsync(itemId);
            if (item == null) return null;
            var skill = item.GetComponent<Skill_Grenade>();
            return skill != null ? skill.grenadePfb : null;
        }
        finally
        {
            if (item != null && item.gameObject)
                Object.Destroy(item.gameObject);
        }
    }

    public static Grenade GetGrenadePrefabByItemIdBlocking(int itemId)
    {
        return GetGrenadePrefabByItemIdAsync(itemId).GetAwaiter().GetResult();
    }

    public static void EnsureRemotePlayersHaveHealthBar()
    {
        foreach (var kv in NetService.Instance.remoteCharacters)
        {
            var go = kv.Value;
            if (!go) continue;
            if (!go.GetComponent<AutoRequestHealthBar>())
                go.AddComponent<AutoRequestHealthBar>(); 
        }
    }

    public static async UniTask<Buff> ResolveBuffAsync(int weaponTypeId, int buffId)
    {
        
        if (weaponTypeId > 0)
            try
            {
                var item = await ItemAssetsCollection.InstantiateAsync(weaponTypeId);
                var gunAgent = item?.AgentUtilities?.ActiveAgent as ItemAgent_Gun;
                var prefab = gunAgent?.GunItemSetting?.buff;
                if (prefab != null) return prefab;
            }
            catch
            {
            }

        
        try
        {
            foreach (var b in Resources.FindObjectsOfTypeAll<Buff>())
                if (b && b.ID == buffId)
                    return b;
        }
        catch
        {
        }

        return null;
    }

    public static void ChangeWeaponModel(CharacterModel characterModel, Item item, HandheldSocketTypes handheldSocket)
    {
        if (characterModel == null) return;

        
        var tSocket = ResolveHandheldSocket(characterModel, handheldSocket);
        if (tSocket == null) return;

        
        ClearChildren(tSocket);

        if (item == null) return;

        ItemAgent itemAgent = null;
        try
        {
            itemAgent = item.ActiveAgent;
        }
        catch
        {
        }

        if (itemAgent == null)
            try
            {
                itemAgent = item.CreateHandheldAgent();
            }
            catch (Exception e)
            {
                Debug.Log($"[COOP] CreateHandheldAgent 失败：{e.Message}");
                return;
            }

        if (itemAgent == null) return;

        
        var duck = itemAgent.GetComponent<DuckovItemAgent>();
        if (duck != null)
            duck.handheldSocket = handheldSocket;

        
        var tr = itemAgent.transform;
        tr.SetParent(tSocket, true);
        tr.localPosition = Vector3.zero;
        tr.localRotation = Quaternion.identity;
        tr.localScale = Vector3.one;

        var go = itemAgent.gameObject;
        if (go && !go.activeSelf) go.SetActive(true);
    }

    
    
    

    
    
    
    
    
    
    

    
    
    
    
    
    

    
    
    
    
    
    
    
    
    

    
    
    
    
    
    
    
    

    
    


    
    
    

    
    

    private static Transform ResolveHandheldSocket(CharacterModel model, HandheldSocketTypes socket)
    {
        switch (socket)
        {
            case HandheldSocketTypes.meleeWeapon:
                return model.MeleeWeaponSocket ? model.MeleeWeaponSocket
                    : model.RightHandSocket ? model.RightHandSocket : model.LefthandSocket;
            case HandheldSocketTypes.leftHandSocket:
                return model.LefthandSocket ? model.LefthandSocket
                    : model.RightHandSocket ? model.RightHandSocket : model.MeleeWeaponSocket;
            case HandheldSocketTypes.normalHandheld:
            default:
                return model.RightHandSocket ? model.RightHandSocket
                    : model.MeleeWeaponSocket ? model.MeleeWeaponSocket : model.LefthandSocket;
        }
    }

    private static void ClearChildren(Transform t)
    {
        if (!t) return;
        for (var i = t.childCount - 1; i >= 0; --i)
        {
            var c = t.GetChild(i);
            if (c) Object.Destroy(c.gameObject);
        }
    }

    private static Animator ResolveRemoteAnimator(GameObject remoteObj)
    {
        var cmc = remoteObj.GetComponent<CharacterMainControl>();
        if (cmc == null || cmc.characterModel == null) return null;
        var model = cmc.characterModel;

        var mb = model.GetComponent<CharacterAnimationControl_MagicBlend>();
        if (mb != null && mb.animator != null) return mb.animator;

        var cac = model.GetComponent<CharacterAnimationControl>();
        if (cac != null && cac.animator != null) return cac.animator;

        
        return model.GetComponent<Animator>();
    }

    public static void StripAllHandItems(CharacterMainControl cmc)
    {
        if (!cmc) return;
        var model = cmc.characterModel;
        if (!model) return;

        void KillChildren(Transform root)
        {
            if (!root) return;
            try
            {
                foreach (var g in root.GetComponentsInChildren<ItemAgent_Gun>(true))
                    if (g && g.gameObject)
                        Object.Destroy(g.gameObject);

                foreach (var m in root.GetComponentsInChildren<ItemAgent_MeleeWeapon>(true))
                    if (m && m.gameObject)
                        Object.Destroy(m.gameObject);

                foreach (var x in root.GetComponentsInChildren<DuckovItemAgent>(true))
                    if (x && x.gameObject)
                        Object.Destroy(x.gameObject);

                var baseType = typeof(Component).Assembly.GetType("ItemAgent");
                if (baseType != null)
                    foreach (var c in root.GetComponentsInChildren(baseType, true))
                        if (c is Component comp && comp.gameObject)
                            Object.Destroy(comp.gameObject);
            }
            catch
            {
            }
        }

        try
        {
            KillChildren(model.RightHandSocket);
        }
        catch
        {
        }

        try
        {
            KillChildren(model.LefthandSocket);
        }
        catch
        {
        }

        try
        {
            KillChildren(model.MeleeWeaponSocket);
        }
        catch
        {
        }
    }
}