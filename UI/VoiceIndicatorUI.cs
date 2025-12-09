using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.UI;

public class VoiceIndicatorUI : MonoBehaviour
{
    public static VoiceIndicatorUI Instance { get; private set; }
    
    private Canvas _canvas;
    private GameObject _container;
    private readonly Dictionary<string, GameObject> _speakerEntries = new();
    
    private ModBehaviourF Service => ModBehaviourF.Instance;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        CreateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Service == null || !Service.networkStarted)
        {
            ClearAllEntries();
            return;
        }
        
        UpdateSpeakerList();
    }

    private void CreateUI()
    {
        var canvasGO = new GameObject("VoiceIndicatorCanvas");
        canvasGO.transform.SetParent(transform);
        
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 900;
        
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        _container = new GameObject("SpeakerContainer");
        _container.transform.SetParent(canvasGO.transform, false);
        
        var rect = _container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(20, 0);
        rect.sizeDelta = new Vector2(250, 400);
        
        var layout = _container.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        
        var fitter = _container.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void UpdateSpeakerList()
    {
        var activeSpeakers = new HashSet<string>();
        
        if (Service.localPlayerStatus != null && Service.localPlayerStatus.IsSpeaking)
        {
            var id = "local";
            activeSpeakers.Add(id);
            EnsureSpeakerEntry(id, "You", true);
        }
        
        if (Service.clientPlayerStatuses != null)
        {
            foreach (var kvp in Service.clientPlayerStatuses)
            {
                if (kvp.Value != null && kvp.Value.IsSpeaking)
                {
                    activeSpeakers.Add(kvp.Key);
                    EnsureSpeakerEntry(kvp.Key, kvp.Value.PlayerName ?? "Unknown", false);
                }
            }
        }
        
        var toRemove = _speakerEntries.Keys.Where(k => !activeSpeakers.Contains(k)).ToList();
        foreach (var key in toRemove)
        {
            if (_speakerEntries.TryGetValue(key, out var entry))
            {
                if (entry != null) Destroy(entry);
                _speakerEntries.Remove(key);
            }
        }
    }

    private void EnsureSpeakerEntry(string id, string name, bool isLocal)
    {
        if (_speakerEntries.ContainsKey(id)) return;
        
        var entry = new GameObject($"Speaker_{id}");
        entry.transform.SetParent(_container.transform, false);
        
        var layout = entry.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(8, 8, 5, 5);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        
        var layoutElement = entry.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 35;
        
        var bg = entry.AddComponent<Image>();
        bg.color = isLocal ? new Color(0.2f, 0.5f, 0.3f, 0.85f) : new Color(0.15f, 0.15f, 0.2f, 0.85f);
        
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(entry.transform, false);
        var iconLayout = iconGO.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 24;
        iconLayout.preferredHeight = 24;
        
        var icon = iconGO.AddComponent<Image>();
        icon.color = new Color(0.4f, 1f, 0.5f, 1f);
        
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(entry.transform, false);
        
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text = name;
        nameTmp.fontSize = 16;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = Color.white;
        
        _speakerEntries[id] = entry;
    }

    private void ClearAllEntries()
    {
        foreach (var entry in _speakerEntries.Values)
        {
            if (entry != null) Destroy(entry);
        }
        _speakerEntries.Clear();
    }
}
