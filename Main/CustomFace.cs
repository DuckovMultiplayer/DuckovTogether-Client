















using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public static class CustomFace
{
    
    public static readonly Dictionary<string, string> _cliPendingFace = new();
    private static NetService Service => NetService.Instance;

    
    public static void Client_ApplyFaceIfAvailable(string playerId, GameObject instance, string faceOverride = null)
    {
        try
        {
            
            var face = faceOverride;
            if (string.IsNullOrEmpty(face))
            {
                if (_cliPendingFace.TryGetValue(playerId, out var pf) && !string.IsNullOrEmpty(pf))
                    face = pf;
                else if (NetService.Instance.clientPlayerStatuses.TryGetValue(playerId, out var st) && !string.IsNullOrEmpty(st.CustomFaceJson))
                    face = st.CustomFaceJson;
            }

            
            if (string.IsNullOrEmpty(face))
                return;

            
            var data = JsonUtility.FromJson<CustomFaceSettingData>(face);

            
            var cm = instance != null ? instance.GetComponentInChildren<CharacterModel>(true) : null;
            var cf = cm != null ? cm.CustomFace : null;
            if (cf != null)
            {
                HardApplyCustomFace(cf, data);
                _cliPendingFace[playerId] = face; 
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[COOP][FACE] Apply failed for {playerId}: {e}");
        }
    }

    public static void HardApplyCustomFace(CustomFaceInstance cf, in CustomFaceSettingData data)
    {
        if (cf == null) return;
        try
        {
            StripAllCustomFaceParts(cf.gameObject);
        }
        catch
        {
        }

        try
        {
            cf.LoadFromData(data);
        }
        catch
        {
        }

        try
        {
            cf.RefreshAll();
        }
        catch
        {
        }
    }

    public static void StripAllCustomFaceParts(GameObject root)
    {
        try
        {
            var all = root.GetComponentsInChildren<CustomFacePart>(true);
            var n = 0;
            foreach (var p in all)
            {
                if (!p) continue;
                n++;
                Object.Destroy(p.gameObject);
            }

            Debug.Log($"[COOP][FACE] stripped {n} CustomFacePart");
        }
        catch
        {
        }
    }

    public static string LoadLocalCustomFaceJson()
    {
        try
        {
            string json = null;

            
            var lm = LevelManager.Instance;
            if (lm != null && lm.CustomFaceManager != null)
                try
                {
                    var data1 = lm.CustomFaceManager.LoadMainCharacterSetting(); 
                    json = JsonUtility.ToJson(data1);
                }
                catch
                {
                }

            
            if (string.IsNullOrEmpty(json) || json == "{}")
                try
                {
                    var main = CharacterMainControl.Main;
                    var model = main != null ? main.characterModel : null;
                    var cf = model != null ? model.CustomFace : null;
                    if (cf != null)
                    {
                        var data2 = cf.ConvertToSaveData(); 
                        var j2 = JsonUtility.ToJson(data2);
                        if (!string.IsNullOrEmpty(j2) && j2 != "{}")
                            json = j2;
                    }
                }
                catch
                {
                }

            
            if (!string.IsNullOrEmpty(json) && json != "{}")
                LocalPlayerManager.Instance._lastGoodFaceJson = json;

            
            return !string.IsNullOrEmpty(json) && json != "{}" ? json : LocalPlayerManager.Instance._lastGoodFaceJson ?? "";
        }
        catch
        {
            return LocalPlayerManager.Instance._lastGoodFaceJson ?? "";
        }
    }

    
    public static void ApplyFaceJsonToModel(CharacterModel model, string faceJson)
    {
        if (model == null || string.IsNullOrEmpty(faceJson)) return;
        try
        {
            CustomFaceSettingData data;
            var ok = CustomFaceSettingData.JsonToData(faceJson, out data);
            if (!ok) data = JsonUtility.FromJson<CustomFaceSettingData>(faceJson);
            model.SetFaceFromData(data);
        }
        catch
        {
            
        }
    }
}