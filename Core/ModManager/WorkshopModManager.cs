using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EscapeFromDuckovCoopMod.Main.ModManager
{
    public class WorkshopModManager : MonoBehaviour
    {
        public static WorkshopModManager Instance { get; private set; }
        
        private List<WorkshopModInfo> _allMods = new List<WorkshopModInfo>();
        private HashSet<string> _enabledModIds = new HashSet<string>();
        private string _workshopPath;
        private string _configPath;
        
        private const string GAME_APP_ID = "3167020";
        private const string CONFIG_FILE = "coop_mod_manager_config.json";
        
        public List<WorkshopModInfo> AllMods => _allMods;
        public event Action OnModListUpdated;
        
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
        
        public void Initialize()
        {
            _configPath = Path.Combine(Application.persistentDataPath, CONFIG_FILE);
            LoadConfig();
            DetectWorkshopPath();
            ScanWorkshopMods();
        }
        
        private void DetectWorkshopPath()
        {
            var possiblePaths = new List<string>();
            
            var commonDrives = new[] { "C", "D", "E", "F", "G" };
            foreach (var drive in commonDrives)
            {
                var steamLibPath = $"{drive}:\\SteamLibrary\\steamapps\\workshop\\content\\{GAME_APP_ID}";
                possiblePaths.Add(steamLibPath);
                
                var steamPath = $"{drive}:\\Program Files (x86)\\Steam\\steamapps\\workshop\\content\\{GAME_APP_ID}";
                possiblePaths.Add(steamPath);
                
                var steamPath2 = $"{drive}:\\Program Files\\Steam\\steamapps\\workshop\\content\\{GAME_APP_ID}";
                possiblePaths.Add(steamPath2);
                
                var steamPath3 = $"{drive}:\\Steam\\steamapps\\workshop\\content\\{GAME_APP_ID}";
                possiblePaths.Add(steamPath3);
            }
            
            var gameDataPath = Application.dataPath;
            var gameRoot = Directory.GetParent(gameDataPath)?.Parent?.FullName;
            if (!string.IsNullOrEmpty(gameRoot))
            {
                possiblePaths.Add(Path.Combine(gameRoot, "steamapps", "workshop", "content", GAME_APP_ID));
            }
            
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    _workshopPath = path;
                    LoggerHelper.Log($"[ModManager] 找到创意工坊路径: {_workshopPath}");
                    return;
                }
            }
            
            LoggerHelper.LogWarning("[ModManager] 未找到Steam创意工坊路径");
        }
        
        private void ScanWorkshopMods()
        {
            _allMods.Clear();
            
            if (string.IsNullOrEmpty(_workshopPath) || !Directory.Exists(_workshopPath))
            {
                LoggerHelper.LogWarning("[ModManager] 创意工坊路径无效，无法扫描模组");
                return;
            }
            
            try
            {
                var modDirs = Directory.GetDirectories(_workshopPath);
                LoggerHelper.Log($"[ModManager] 找到 {modDirs.Length} 个模组文件夹");
                
                foreach (var modDir in modDirs)
                {
                    try
                    {
                        var modInfo = ParseModInfo(modDir);
                        if (modInfo != null)
                        {
                            _allMods.Add(modInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogError($"[ModManager] 解析模组失败 {modDir}: {ex.Message}");
                    }
                }
                
                LoggerHelper.Log($"[ModManager] 成功加载 {_allMods.Count} 个模组");
                OnModListUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 扫描模组失败: {ex.Message}");
            }
        }
        
        private WorkshopModInfo ParseModInfo(string modPath)
        {
            var modId = Path.GetFileName(modPath);
            var modInfo = new WorkshopModInfo
            {
                ModId = modId,
                ModPath = modPath,
                IsEnabled = _enabledModIds.Contains(modId)
            };
            
            var infoPath = Path.Combine(modPath, "info.ini");
            if (File.Exists(infoPath))
            {
                ParseInfoIni(infoPath, modInfo);
            }
            
            var jsonPath = Path.Combine(modPath, "workshop.json");
            if (File.Exists(jsonPath))
            {
                ParseWorkshopJson(jsonPath, modInfo);
            }
            
            FindPreviewImage(modPath, modInfo);
            
            if (string.IsNullOrEmpty(modInfo.ModName))
            {
                modInfo.ModName = $"Mod {modId}";
            }
            
            CheckCompatibility(modInfo);
            
            return modInfo;
        }
        
        private void ParseInfoIni(string path, WorkshopModInfo modInfo)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;
                        
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    
                    var key = parts[0].Trim().ToLower();
                    var value = parts[1].Trim();
                    
                    switch (key)
                    {
                        case "name":
                        case "displayname":
                            modInfo.ModName = value;
                            break;
                        case "description":
                            modInfo.Description = value;
                            break;
                        case "author":
                            modInfo.Author = value;
                            break;
                        case "version":
                            modInfo.Version = value;
                            break;
                        case "publishedfileid":
                            if (!string.IsNullOrEmpty(value))
                            {
                                modInfo.ModId = value;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 解析info.ini失败: {ex.Message}");
            }
        }
        
        private void ParseWorkshopJson(string path, WorkshopModInfo modInfo)
        {
            try
            {
                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);
                
                if (obj["name"] != null)
                    modInfo.ModName = obj["name"].ToString();
                if (obj["description"] != null)
                    modInfo.Description = obj["description"].ToString();
                if (obj["author"] != null)
                    modInfo.Author = obj["author"].ToString();
                if (obj["version"] != null)
                    modInfo.Version = obj["version"].ToString();
                if (obj["tags"] != null && obj["tags"] is JArray tags)
                {
                    modInfo.Tags = tags.Select(t => t.ToString()).ToList();
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 解析workshop.json失败: {ex.Message}");
            }
        }
        
        private void FindPreviewImage(string modPath, WorkshopModInfo modInfo)
        {
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var imageNames = new[] { "preview", "icon", "thumbnail", "logo" };
            
            foreach (var name in imageNames)
            {
                foreach (var ext in imageExtensions)
                {
                    var imagePath = Path.Combine(modPath, name + ext);
                    if (File.Exists(imagePath))
                    {
                        modInfo.PreviewImagePath = imagePath;
                        return;
                    }
                }
            }
        }
        
        private void CheckCompatibility(WorkshopModInfo modInfo)
        {
            var compatibleMods = new HashSet<string>
            {
                "3591339491",
                "FirstPersonCamera"
            };
            
            modInfo.IsCompatible = compatibleMods.Contains(modInfo.ModId) || 
                                   (modInfo.ModName != null && compatibleMods.Any(id => modInfo.ModName.Contains(id)));
            
            if (modInfo.IsCompatible)
            {
                if (!modInfo.Tags.Contains("CoopMod兼容"))
                {
                    modInfo.Tags.Insert(0, "CoopMod兼容");
                }
            }
        }
        
        public void SetModEnabled(string modId, bool enabled)
        {
            if (enabled)
            {
                _enabledModIds.Add(modId);
            }
            else
            {
                _enabledModIds.Remove(modId);
            }
            
            var mod = _allMods.FirstOrDefault(m => m.ModId == modId);
            if (mod != null)
            {
                mod.IsEnabled = enabled;
            }
            
            SaveConfig();
            LoggerHelper.Log($"[ModManager] 模组 {modId} {(enabled ? "已启用" : "已禁用")}");
        }
        
        public bool IsModEnabled(string modId)
        {
            return _enabledModIds.Contains(modId);
        }
        
        public void RefreshModList()
        {
            ScanWorkshopMods();
        }
        
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonConvert.DeserializeObject<ModManagerConfig>(json);
                    _enabledModIds = new HashSet<string>(config.EnabledMods ?? new List<string>());
                    LoggerHelper.Log($"[ModManager] 加载配置: {_enabledModIds.Count} 个已启用模组");
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 加载配置失败: {ex.Message}");
            }
        }
        
        private void SaveConfig()
        {
            try
            {
                var config = new ModManagerConfig
                {
                    EnabledMods = _enabledModIds.ToList()
                };
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 保存配置失败: {ex.Message}");
            }
        }
        
        public Texture2D LoadModPreview(WorkshopModInfo modInfo)
        {
            if (modInfo.PreviewTexture != null)
                return modInfo.PreviewTexture;
                
            if (string.IsNullOrEmpty(modInfo.PreviewImagePath) || !File.Exists(modInfo.PreviewImagePath))
                return null;
                
            try
            {
                var bytes = File.ReadAllBytes(modInfo.PreviewImagePath);
                var texture = new Texture2D(2, 2);
                texture.LoadImage(bytes);
                modInfo.PreviewTexture = texture;
                return texture;
            }
            catch (Exception ex)
            {
                LoggerHelper.LogError($"[ModManager] 加载预览图失败: {ex.Message}");
                return null;
            }
        }
        
        [Serializable]
        private class ModManagerConfig
        {
            public List<string> EnabledMods;
        }
    }
}
