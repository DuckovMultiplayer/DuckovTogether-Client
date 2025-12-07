using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.Main.UI;

public class VersionLabel : MonoBehaviour
{
    private static VersionLabel _instance;
    
    public static void Create()
    {
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
        }
        
        var go = new GameObject("VersionLabel");
        DontDestroyOnLoad(go);
        
        _instance = go.AddComponent<VersionLabel>();
        _instance.Initialize();
    }
    
    private void Initialize()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        
        var canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        
        gameObject.AddComponent<GraphicRaycaster>();
        
        var textGO = new GameObject("VersionText");
        textGO.transform.SetParent(transform, false);
        
        var rectTransform = textGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(1, 1);
        rectTransform.anchoredPosition = new Vector2(-15, -15);
        rectTransform.sizeDelta = new Vector2(450, 30);
        
        var text = textGO.AddComponent<Text>();
        string versionString = $"逃离鸭科夫联机Mod v{BuildInfo.ModVersion} ({BuildInfo.CommitHash})";
        text.text = versionString;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 13;
        text.color = new Color(0.65f, 0.65f, 0.65f, 0.55f);
        text.alignment = TextAnchor.UpperRight;
        text.raycastTarget = false;
        
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.4f);
        outline.effectDistance = new Vector2(1, -1);
    }
}
