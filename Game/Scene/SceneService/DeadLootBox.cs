















using System.Collections;
using ItemStatsSystem;
using UnityEngine.SceneManagement;
using EscapeFromDuckovCoopMod.Net;
using Object = UnityEngine.Object;  

namespace EscapeFromDuckovCoopMod;

public class DeadLootBox : MonoBehaviour
{
    
    public const bool EAGER_BROADCAST_LOOT_STATE_ON_SPAWN = true;
    public static DeadLootBox Instance;

    private ModBehaviourF Service => ModBehaviourF.Instance;
    private bool networkStarted => Service != null && Service.networkStarted;


    public void Init()
    {
        Instance = this;
    }

    public void SpawnDeadLootboxAt(int aiId, int lootUid, Vector3 pos, Quaternion rot)
    {
        
        StartCoroutine(SpawnDeadLootboxAtAsync(aiId, lootUid, pos, rot));
    }

    
    private IEnumerator SpawnDeadLootboxAtAsync(int aiId, int lootUid, Vector3 pos, Quaternion rot)
    {
        
        
        
        AITool.TryClientRemoveNearestAICorpse(pos, 2.5f);
        yield return null;

        
        var prefab = GetDeadLootPrefabOnClient(aiId);
        if (!prefab) yield break;

        var go = Instantiate(prefab, pos, rot);
        var box = go ? go.GetComponent<InteractableLootbox>() : null;
        if (!box) yield break;

        var inv = box.Inventory;
        if (!inv) yield break;

        WorldLootPrime.PrimeIfClient(box);
        yield return null;

        
        var dict = InteractableLootbox.Inventories;
        if (dict != null)
        {
            var correctKey = LootManager.ComputeLootKeyFromPos(pos);
            var wrongKey = -1;
            foreach (var kv in dict)
                if (kv.Value == inv && kv.Key != correctKey)
                {
                    wrongKey = kv.Key;
                    break;
                }

            if (wrongKey != -1) dict.Remove(wrongKey);
            dict[correctKey] = inv;
        }

        if (lootUid >= 0)
        {
            LootManager.Instance._cliLootByUid[lootUid] = inv;
            
            var registry = Utils.LootContainerRegistry.Instance;
            if (registry != null)
            {
                registry.RegisterContainerWithLootUid(box, lootUid);
                Debug.Log($"[DeadLootBox] 已注册AI掉落箱到注册表: lootUid={lootUid}, pos={pos}");
            }
        }
        yield return null;

        
        if (lootUid >= 0 && LootManager.Instance._pendingLootStatesByUid.TryGetValue(lootUid, out var pack))
        {
            LootManager.Instance._pendingLootStatesByUid.Remove(lootUid);

            COOPManager.LootNet._applyingLootState = true;
            try
            {
                var cap = Mathf.Clamp(pack.capacity, 1, 128);
                inv.Loading = true;
                inv.SetCapacity(cap);

                for (var i = inv.Content.Count - 1; i >= 0; --i)
                {
                    Item removed;
                    inv.RemoveAt(i, out removed);
                    if (removed != null)
                    {
                        try { Destroy(removed.gameObject); }
                        catch {  }
                    }
                }

                foreach (var (p, snap) in pack.Item2)
                {
                    var item = ItemTool.BuildItemFromSnapshot(snap);
                    if (item && !InventoryPlacementUtil.TryPlaceItemExact(inv, item, p))
                    {
                        try
                        {
                            Object.Destroy(item.gameObject);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                inv.Loading = false;
                COOPManager.LootNet._applyingLootState = false;
            }

            WorldLootPrime.PrimeIfClient(box);
            yield break;
        }
        yield return null;

        
        COOPManager.LootNet.Client_RequestLootState(inv);
        StartCoroutine(LootManager.Instance.ClearLootLoadingTimeout(inv, 1.5f));
    }


    private GameObject GetDeadLootPrefabOnClient(int aiId)
    {
        
        try
        {
            if (aiId > 0 && AITool.aiById.TryGetValue(aiId, out var cmc) && cmc)
            {
                
                
                

                if (cmc != null)
                {
                    var obj = cmc.deadLootBoxPrefab.gameObject;
                    if (obj) return obj;
                }
                
                
            }
        }
        catch
        {
        }

        
        try
        {
            var main = CharacterMainControl.Main;
            if (main)
            {
                var obj = main.deadLootBoxPrefab.gameObject;
                if (obj) return obj;
            }
        }
        catch
        {
        }

        try
        {
            var any = FindObjectOfType<CharacterMainControl>();
            if (any)
            {
                var obj = any.deadLootBoxPrefab.gameObject;
                if (obj) return obj;
            }
        }
        catch
        {
        }

        return null;
    }
}