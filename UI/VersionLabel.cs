using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.UI;

public class VersionLabel : MonoBehaviour
{
    public static VersionLabel Instance { get; private set; }
    
    private const string VERSION = "1.0.0";
    private const string BUILD_TYPE = "Client";
    
    private Canvas _canvas;
    private TMP_Text _versionText;

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

    private void CreateUI()
    {
        var canvasGO = new GameObject("VersionCanvas");
        canvasGO.transform.SetParent(transform);
        
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        var labelGO = new GameObject("VersionText");
        labelGO.transform.SetParent(canvasGO.transform, false);
        
        var rect = labelGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-15, -10);
        rect.sizeDelta = new Vector2(300, 30);
        
        _versionText = labelGO.AddComponent<TextMeshProUGUI>();
        _versionText.text = $"Duckov Together {BUILD_TYPE} v{VERSION}";
        _versionText.fontSize = 14;
        _versionText.fontStyle = FontStyles.Normal;
        _versionText.color = new Color(1f, 1f, 1f, 0.5f);
        _versionText.alignment = TextAlignmentOptions.Right;
    }

    public void SetVersion(string version)
    {
        if (_versionText != null)
        {
            _versionText.text = $"Duckov Together {BUILD_TYPE} v{version}";
        }
    }
}
