















using SodaCraft.Localizations;

namespace EscapeFromDuckovCoopMod
{
    
    
    
    
    public static class CoopLocalization
    {
        private static Dictionary<string, string> currentTranslations = new Dictionary<string, string>();
        private static string currentLanguageCode = "en-US";
        private static bool isInitialized = false;
        private static SystemLanguage lastSystemLanguage = SystemLanguage.Unknown;

        
        
        
        public static void Initialize()
        {
            if (isInitialized) return;

            
            DetectAndLoadLanguage();
            isInitialized = true;

            Debug.Log($"[CoopLocalization] Initialized with language: {currentLanguageCode}");
        }

        
        
        
        public static void CheckLanguageChange()
        {
            if (!isInitialized) return;

            var currentSystemLang = LocalizationManager.CurrentLanguage;
            if (currentSystemLang != lastSystemLanguage)
            {
                Debug.Log($"[CoopLocalization] Language changed from {lastSystemLanguage} to {currentSystemLang}, reloading translations...");
                DetectAndLoadLanguage();
            }
        }

        
        
        
        private static void DetectAndLoadLanguage()
        {
            var systemLang = LocalizationManager.CurrentLanguage;
            lastSystemLanguage = systemLang;

            switch (systemLang)
            {
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                case SystemLanguage.Chinese:
                    currentLanguageCode = "zh-CN";
                    break;
                case SystemLanguage.Korean:
                    currentLanguageCode = "ko-KR";
                    break;
                case SystemLanguage.Japanese:
                    currentLanguageCode = "ja-JP";
                    break;
                case SystemLanguage.English:
                default:
                    currentLanguageCode = "en-US";
                    break;
            }

            LoadTranslations(currentLanguageCode);
        }

        
        
        
        private static void LoadTranslations(string languageCode)
        {
            currentTranslations.Clear();

            try
            {
                
                string modPath = Path.GetDirectoryName(typeof(CoopLocalization).Assembly.Location);
                string localizationPath = Path.Combine(modPath, "Localization", $"{languageCode}.json");

                
                if (!File.Exists(localizationPath))
                {
                    Debug.LogWarning($"[CoopLocalization] Translation file not found: {localizationPath}, using fallback translations");
                    LoadFallbackTranslations();
                    return;
                }

                string json = File.ReadAllText(localizationPath);

                
                ParseJsonTranslations(json);

                if (currentTranslations.Count > 0)
                {
                    Debug.Log($"[CoopLocalization] Loaded {currentTranslations.Count} translations from {localizationPath}");
                }
                else
                {
                    Debug.LogWarning($"[CoopLocalization] Failed to parse translation file, using fallback");
                    LoadFallbackTranslations();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoopLocalization] Error loading translations: {e.Message}");
                LoadFallbackTranslations();
            }
        }

        
        
        
        private static void ParseJsonTranslations(string json)
        {
            try
            {
                
                int startIndex = json.IndexOf("\"translations\"");
                if (startIndex == -1) return;

                
                int arrayStart = json.IndexOf('[', startIndex);
                if (arrayStart == -1) return;

                
                int arrayEnd = json.LastIndexOf(']');
                if (arrayEnd == -1) return;

                
                string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

                
                int braceCount = 0;
                int entryStart = -1;

                for (int i = 0; i < arrayContent.Length; i++)
                {
                    char c = arrayContent[i];

                    if (c == '{')
                    {
                        if (braceCount == 0) entryStart = i;
                        braceCount++;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (braceCount == 0 && entryStart != -1)
                        {
                            
                            string entry = arrayContent.Substring(entryStart, i - entryStart + 1);
                            ParseSingleEntry(entry);
                            entryStart = -1;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CoopLocalization] JSON parsing error: {e.Message}");
            }
        }

        
        
        
        private static void ParseSingleEntry(string entry)
        {
            try
            {
                string key = null;
                string value = null;

                
                int keyIndex = entry.IndexOf("\"key\"");
                if (keyIndex != -1)
                {
                    int keyValueStart = entry.IndexOf(':', keyIndex);
                    if (keyValueStart != -1)
                    {
                        int keyQuoteStart = entry.IndexOf('\"', keyValueStart);
                        int keyQuoteEnd = entry.IndexOf('\"', keyQuoteStart + 1);
                        if (keyQuoteStart != -1 && keyQuoteEnd != -1)
                        {
                            key = entry.Substring(keyQuoteStart + 1, keyQuoteEnd - keyQuoteStart - 1);
                        }
                    }
                }

                
                int valueIndex = entry.IndexOf("\"value\"");
                if (valueIndex != -1)
                {
                    int valueValueStart = entry.IndexOf(':', valueIndex);
                    if (valueValueStart != -1)
                    {
                        int valueQuoteStart = entry.IndexOf('\"', valueValueStart);
                        int valueQuoteEnd = entry.IndexOf('\"', valueQuoteStart + 1);
                        if (valueQuoteStart != -1 && valueQuoteEnd != -1)
                        {
                            value = entry.Substring(valueQuoteStart + 1, valueQuoteEnd - valueQuoteStart - 1);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    currentTranslations[key] = value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoopLocalization] Entry parsing error: {e.Message}");
            }
        }

        
        
        
        private static void LoadFallbackTranslations()
        {
            currentTranslations.Clear();
            
            currentTranslations["ui.spectator.mode"] = currentLanguageCode == "zh-CN" 
                ? "观战模式：左键 ▶ 下一个 | 右键 ◀ 上一个  | 正在观战" 
                : "Spectator Mode: LMB ▶ Next | RMB ◀ Previous | Spectating";
        }

        
        
        
        
        
        
        public static string Get(string key, params object[] args)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (currentTranslations.TryGetValue(key, out string value))
            {
                if (args != null && args.Length > 0)
                {
                    try
                    {
                        return string.Format(value, args);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CoopLocalization] Format error for key '{key}': {e.Message}");
                        return value;
                    }
                }
                return value;
            }

            Debug.LogWarning($"[CoopLocalization] Missing translation for key: {key}");
            return $"[{key}]";
        }

        
        
        
        
        public static void SetLanguage(string languageCode)
        {
            if (currentLanguageCode == languageCode) return;

            currentLanguageCode = languageCode;
            LoadTranslations(languageCode);
            Debug.Log($"[CoopLocalization] Language changed to: {languageCode}");
        }

        
        
        
        public static string GetCurrentLanguage()
        {
            return currentLanguageCode;
        }
    }
}
