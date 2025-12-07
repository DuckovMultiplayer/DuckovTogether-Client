using System.IO;
using Newtonsoft.Json;
using ItemStatsSystem;
using UnityEngine.SceneManagement;

namespace EscapeFromDuckovCoopMod.Utils;

public static class GameDataExporter
{
    private static string ExportPath => Path.Combine(Application.persistentDataPath, "ServerData");
    
    public static void ExportAllData()
    {
        Directory.CreateDirectory(ExportPath);
        
        ExportItemDatabase();
        ExportCurrentSceneData();
        
        Debug.Log($"[DataExporter] Exported to: {ExportPath}");
    }
    
    public static void ExportItemDatabase()
    {
        try
        {
            var items = new List<ExportedItem>();
            
            var allItems = UnityEngine.Object.FindObjectsOfType<Item>(true);
            var seenIds = new HashSet<int>();
            
            foreach (var item in allItems)
            {
                if (item == null || seenIds.Contains(item.TypeID)) continue;
                seenIds.Add(item.TypeID);
                
                items.Add(new ExportedItem
                {
                    TypeId = item.TypeID,
                    Name = item.name,
                    DisplayName = item.DisplayName,
                    Category = "Unknown",
                    StackSize = item.StackCount,
                    Weight = 0.1f,
                    Value = 100,
                    Durability = item.MaxDurability
                });
            }
            
            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(Path.Combine(ExportPath, "items.json"), json);
            Debug.Log($"[DataExporter] Exported {items.Count} items");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataExporter] Failed to export items: {ex.Message}");
        }
    }
    
    public static void ExportCurrentSceneData()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            var sceneData = new ExportedScene
            {
                SceneId = scene.name,
                BuildIndex = scene.buildIndex
            };
            
            var spawners = UnityEngine.Object.FindObjectsOfType<CharacterSpawnerRoot>(true);
            foreach (var spawner in spawners)
            {
                var pos = spawner.transform.position;
                sceneData.AISpawns.Add(new ExportedAISpawn
                {
                    SpawnerId = AITool.StableRootId(spawner),
                    Position = new float[] { pos.x, pos.y, pos.z },
                    SpawnerName = spawner.name
                });
            }
            
            var lootContainers = UnityEngine.Object.FindObjectsOfType<Inventory>(true);
            foreach (var container in lootContainers)
            {
                if (container.GetComponentInParent<CharacterMainControl>() != null) continue;
                
                var pos = container.transform.position;
                var capacity = 10;
                try { capacity = container.Content?.Count ?? 10; } catch { }
                
                sceneData.LootSpawns.Add(new ExportedLootSpawn
                {
                    ContainerId = container.GetInstanceID(),
                    Position = new float[] { pos.x, pos.y, pos.z },
                    ContainerName = container.name,
                    Capacity = capacity
                });
            }
            
            var scenesDir = Path.Combine(ExportPath, "scenes");
            Directory.CreateDirectory(scenesDir);
            var json = JsonConvert.SerializeObject(sceneData, Formatting.Indented);
            File.WriteAllText(Path.Combine(scenesDir, $"{scene.name}.json"), json);
            Debug.Log($"[DataExporter] Exported scene: {scene.name} ({sceneData.AISpawns.Count} AI, {sceneData.LootSpawns.Count} loot)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataExporter] Failed to export scene: {ex.Message}");
        }
    }
    
    public static void ExportAIData()
    {
        try
        {
            var aiList = new List<ExportedAI>();
            
            foreach (var kv in AITool.aiById)
            {
                var cmc = kv.Value;
                if (cmc == null) continue;
                
                var health = cmc.Health;
                var pos = cmc.transform.position;
                
                aiList.Add(new ExportedAI
                {
                    AIId = kv.Key,
                    TypeName = cmc.name,
                    Position = new float[] { pos.x, pos.y, pos.z },
                    MaxHealth = health?.MaxHealth ?? 100f,
                    CurrentHealth = health?.CurrentHealth ?? 100f,
                    Team = cmc.Team.ToString()
                });
            }
            
            var json = JsonConvert.SerializeObject(aiList, Formatting.Indented);
            File.WriteAllText(Path.Combine(ExportPath, "current_ai.json"), json);
            Debug.Log($"[DataExporter] Exported {aiList.Count} AI entities");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataExporter] Failed to export AI: {ex.Message}");
        }
    }
}

public class ExportedItem
{
    public int TypeId { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; }
    public int StackSize { get; set; }
    public float Weight { get; set; }
    public int Value { get; set; }
    public float Durability { get; set; }
}

public class ExportedScene
{
    public string SceneId { get; set; }
    public int BuildIndex { get; set; }
    public List<ExportedAISpawn> AISpawns { get; set; } = new();
    public List<ExportedLootSpawn> LootSpawns { get; set; } = new();
    public List<float[]> PlayerSpawns { get; set; } = new();
}

public class ExportedAISpawn
{
    public int SpawnerId { get; set; }
    public float[] Position { get; set; }
    public string SpawnerName { get; set; }
}

public class ExportedLootSpawn
{
    public int ContainerId { get; set; }
    public float[] Position { get; set; }
    public string ContainerName { get; set; }
    public int Capacity { get; set; }
}

public class ExportedAI
{
    public int AIId { get; set; }
    public string TypeName { get; set; }
    public float[] Position { get; set; }
    public float MaxHealth { get; set; }
    public float CurrentHealth { get; set; }
    public string Team { get; set; }
}
