using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EscapeFromDuckovCoopMod.Net;

public class ClientItemManager : MonoBehaviour
{
    public static ClientItemManager Instance { get; private set; }
    
    private readonly Dictionary<int, DroppedItemVisual> _droppedItems = new();
    private readonly Dictionary<int, ContainerState> _containerStates = new();
    
    public GameObject DroppedItemPrefab { get; set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        RegisterEvents();
    }
    
    private void RegisterEvents() { }
    private void UnregisterEvents() { }
    
    private void OnItemPickup(int playerId, int containerId, int slotIndex, int itemTypeId, int count)
    {
        var client = DuckovTogetherClient.Instance;
        if (client == null) return;
        
        if (playerId.ToString() == client.NetworkId)
        {
            return;
        }
        
        if (_containerStates.TryGetValue(containerId, out var container))
        {
            container.RemoveItem(slotIndex, count);
        }
        
        Debug.Log($"[ClientItem] Player {playerId} picked up {count}x item {itemTypeId} from container {containerId}");
    }
    
    private void OnItemDrop(int dropId, int playerId, int itemTypeId, int count, Vector3 position)
    {
        if (_droppedItems.ContainsKey(dropId))
        {
            return;
        }
        
        var visual = CreateDroppedItemVisual(dropId, itemTypeId, count, position);
        if (visual != null)
        {
            _droppedItems[dropId] = visual;
        }
        
        Debug.Log($"[ClientItem] Player {playerId} dropped {count}x item {itemTypeId} at {position}");
    }
    
    private void OnLootFullSync(string json)
    {
        try
        {
            var syncData = JsonConvert.DeserializeObject<LootFullSyncData>(json);
            if (syncData == null) return;
            
            foreach (var container in syncData.containers)
            {
                if (!_containerStates.TryGetValue(container.containerId, out var state))
                {
                    state = new ContainerState { ContainerId = container.containerId };
                    _containerStates[container.containerId] = state;
                }
                
                state.Items.Clear();
                if (container.items != null)
                {
                    foreach (var item in container.items)
                    {
                        state.Items[item.slot] = new ContainerItemState
                        {
                            ItemTypeId = item.itemTypeId,
                            Count = item.count,
                            Durability = item.durability
                        };
                    }
                }
            }
            
            ClearDroppedItems();
            
            if (syncData.droppedItems != null)
            {
                foreach (var item in syncData.droppedItems)
                {
                    var pos = new Vector3(item.position.x, item.position.y, item.position.z);
                    var visual = CreateDroppedItemVisual(item.dropId, item.itemTypeId, item.count, pos);
                    if (visual != null)
                    {
                        _droppedItems[item.dropId] = visual;
                    }
                }
            }
            
            Debug.Log($"[ClientItem] Full sync: {_containerStates.Count} containers, {_droppedItems.Count} dropped items");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientItem] Loot sync error: {ex.Message}");
        }
    }
    
    private void OnContainerContents(string json)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<ContainerContentsData>(json);
            if (data == null) return;
            
            if (!_containerStates.TryGetValue(data.containerId, out var state))
            {
                state = new ContainerState { ContainerId = data.containerId };
                _containerStates[data.containerId] = state;
            }
            
            state.Items.Clear();
            if (data.items != null)
            {
                foreach (var item in data.items)
                {
                    state.Items[item.slot] = new ContainerItemState
                    {
                        ItemTypeId = item.itemTypeId,
                        Count = item.count,
                        Durability = item.durability
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientItem] Container contents error: {ex.Message}");
        }
    }
    
    private DroppedItemVisual CreateDroppedItemVisual(int dropId, int itemTypeId, int count, Vector3 position)
    {
        GameObject itemObject = null;
        
        var itemPrefab = Resources.Load<GameObject>($"Items/Item_{itemTypeId}");
        if (itemPrefab != null)
        {
            itemObject = Instantiate(itemPrefab, position, Quaternion.identity);
        }
        else if (DroppedItemPrefab != null)
        {
            itemObject = Instantiate(DroppedItemPrefab, position, Quaternion.identity);
        }
        else
        {
            itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.transform.position = position;
            itemObject.transform.localScale = Vector3.one * 0.3f;
            
            var renderer = itemObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(1f, 0.8f, 0.2f);
                renderer.material = mat;
            }
        }
        
        itemObject.name = $"DroppedItem_{dropId}";
        
        var interactable = itemObject.AddComponent<DroppedItemInteractable>();
        interactable.DropId = dropId;
        interactable.ItemTypeId = itemTypeId;
        interactable.Count = count;
        
        return new DroppedItemVisual
        {
            DropId = dropId,
            ItemTypeId = itemTypeId,
            Count = count,
            Position = position,
            GameObject = itemObject
        };
    }
    
    public void ClearDroppedItems()
    {
        foreach (var kvp in _droppedItems)
        {
            if (kvp.Value.GameObject != null)
            {
                Destroy(kvp.Value.GameObject);
            }
        }
        _droppedItems.Clear();
    }
    
    public void RemoveDroppedItem(int dropId)
    {
        if (_droppedItems.TryGetValue(dropId, out var visual))
        {
            if (visual.GameObject != null)
            {
                Destroy(visual.GameObject);
            }
            _droppedItems.Remove(dropId);
        }
    }
    
    public ContainerState GetContainerState(int containerId)
    {
        _containerStates.TryGetValue(containerId, out var state);
        return state;
    }
    
    public void SendItemPickup(int containerId, int slotIndex, int itemTypeId, int count)
    {
        DuckovTogetherClient.Instance?.SendItemPickup(containerId, slotIndex, itemTypeId, count);
    }
    
    public void SendItemDrop(int itemTypeId, int count, Vector3 position)
    {
        DuckovTogetherClient.Instance?.SendItemDrop(itemTypeId, count, position);
    }
    
    public void RequestContainerContents(int containerId)
    {
        DuckovTogetherClient.Instance?.SendJson(new { type = "containerOpen", containerId = containerId });
    }
    
    private void OnDestroy()
    {
        UnregisterEvents();
        ClearDroppedItems();
        _containerStates.Clear();
        if (Instance == this) Instance = null;
    }
}

public class DroppedItemVisual
{
    public int DropId;
    public int ItemTypeId;
    public int Count;
    public Vector3 Position;
    public GameObject GameObject;
}

public class ContainerState
{
    public int ContainerId;
    public Dictionary<int, ContainerItemState> Items = new();
    
    public void RemoveItem(int slot, int count)
    {
        if (Items.TryGetValue(slot, out var item))
        {
            item.Count -= count;
            if (item.Count <= 0)
            {
                Items.Remove(slot);
            }
        }
    }
}

public class ContainerItemState
{
    public int ItemTypeId;
    public int Count;
    public float Durability;
}

public class DroppedItemInteractable : MonoBehaviour
{
    public int DropId;
    public int ItemTypeId;
    public int Count;
    
    private bool _canInteract = true;
    
    public void Interact()
    {
        if (!_canInteract) return;
        
        _canInteract = false;
        
        ClientItemManager.Instance?.SendItemPickup(0, 0, ItemTypeId, Count);
        ClientItemManager.Instance?.RemoveDroppedItem(DropId);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        var mainCharacter = CharacterMainControl.Main;
        if (mainCharacter != null && other.gameObject == mainCharacter.gameObject)
        {
        }
    }
}

public class LootFullSyncData
{
    public string type { get; set; }
    public List<ContainerSyncData> containers { get; set; }
    public List<DroppedItemSyncData> droppedItems { get; set; }
}

public class ContainerSyncData
{
    public int containerId { get; set; }
    public List<ContainerItemSyncData> items { get; set; }
}

public class ContainerItemSyncData
{
    public int slot { get; set; }
    public int itemTypeId { get; set; }
    public int count { get; set; }
    public float durability { get; set; }
}

public class DroppedItemSyncData
{
    public int dropId { get; set; }
    public int itemTypeId { get; set; }
    public int count { get; set; }
    public Vector3 position { get; set; }
}

public class ContainerContentsData
{
    public string type { get; set; }
    public int containerId { get; set; }
    public List<ContainerItemSyncData> items { get; set; }
}
