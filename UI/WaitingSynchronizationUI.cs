


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod;




public class WaitingSynchronizationUI : MonoBehaviour
{
    private static WaitingSynchronizationUI _instance;
    public static WaitingSynchronizationUI Instance => _instance;

    private Canvas _canvas;
    private GameObject _panel;
    private CanvasGroup _canvasGroup; 
    private TMP_Text _titleText;
    private TMP_Text _syncStatusText;
    private TMP_Text _syncPercentText;
    private TMP_Text _mapInfoText;
    private TMP_Text _timeInfoText;
    private TMP_Text _weatherInfoText;
    private GameObject _playerListContainer;
    private GameObject _loadingAnimation;
    private float _loadingRotation = 0f;

    
    private Dictionary<string, SyncTaskStatus> _syncTasks =
        new Dictionary<string, SyncTaskStatus>();
    private bool _allTasksCompleted = false;

    
    private Dictionary<ulong, Sprite> _steamAvatarCache = new Dictionary<ulong, Sprite>();

    
    private Coroutine _fadeOutCoroutine = null;

    
    private bool _autoProgressEnabled = false;
    private float _autoProgressPercent = 0f;
    private float _lastAutoProgressTime = 0f;

    
    private float _uiShowTime = 0f;
    private const float MAX_UI_DISPLAY_TIME = 90f; 
    private const float TASK_STUCK_TIMEOUT = 30f; 
    private Dictionary<string, float> _taskLastUpdateTime = new Dictionary<string, float>();

    
    private Queue<float> _fpsHistory = new Queue<float>(); 
    private const int FPS_HISTORY_SIZE = 60; 
    private const float MIN_STABLE_FPS = 30f; 
    private const float FPS_STABLE_DURATION = 3f; 
    private float _fpsStableStartTime = 0f; 
    private bool _fpsIsStable = false; 
    private bool _fpsCheckEnabled = false; 

    
    private Health _invincibilityTargetHealth = null;
    private bool? _originalInvincibleState = null;
    private Coroutine _invincibilityTimerCoroutine = null;
    private const float INVINCIBILITY_DURATION = 30f; 

    public class SyncTaskStatus
    {
        public string Name;
        public bool IsCompleted;
        public string Details;
    }

    private void Awake()
    {
        Debug.Log("[SYNC_UI] ========== Awake() ENTRY POINT ==========");
        try
        {
            Debug.Log("[SYNC_UI] Awake() START - inside try block");

            if (_instance != null && _instance != this)
            {
                Debug.Log("[SYNC_UI] 已存在实例，销毁当前对象");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SYNC_UI] 实例已创建并设置为DontDestroyOnLoad");

            CreateUI();
            Debug.Log("[SYNC_UI] UI创建完成");

            
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            Debug.Log("[SYNC_UI] 初始隐藏完成，Awake结束");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] Awake ERROR: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void CreateUI()
    {
        try
        {
            Debug.Log("[SYNC_UI] CreateUI START");
            
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

        var canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        
        _panel = new GameObject("SyncPanel");
        _panel.transform.SetParent(transform);

        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = _panel.AddComponent<Image>();

        
        _canvasGroup = _panel.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        
        LoadBackgroundImage(panelImage);

        
        var darkOverlay = new GameObject("DarkOverlay");
        darkOverlay.transform.SetParent(_panel.transform);
        var overlayRect = darkOverlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;

        var overlayImage = darkOverlay.AddComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0.94f);

        
        _titleText = CreateSimpleText("Title", _panel.transform, 56, FontStyles.Bold);
        _titleText.text = "正在加载场景...";
        _titleText.alignment = TextAlignmentOptions.Center;
        var titleRect = _titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.anchoredPosition = new Vector2(0, -80);
        titleRect.sizeDelta = new Vector2(0, 80);

        
        CreateCloseButton();

        
        _playerListContainer = new GameObject("PlayerListContainer");
        _playerListContainer.transform.SetParent(_panel.transform);
        var playerListRect = _playerListContainer.AddComponent<RectTransform>();
        playerListRect.anchorMin = new Vector2(0.05f, 0.20f);
        playerListRect.anchorMax = new Vector2(0.30f, 0.85f);
        playerListRect.sizeDelta = Vector2.zero;
        playerListRect.anchoredPosition = Vector2.zero;

        var playerListLayout = _playerListContainer.AddComponent<VerticalLayoutGroup>();
        playerListLayout.childAlignment = TextAnchor.UpperLeft;
        playerListLayout.spacing = 24; 
        playerListLayout.padding = new RectOffset(20, 20, 20, 20); 
        playerListLayout.childControlHeight = false;
        playerListLayout.childControlWidth = true;
        playerListLayout.childForceExpandHeight = false;
        playerListLayout.childForceExpandWidth = true;

        
        var infoPanel = new GameObject("InfoPanel");
        infoPanel.transform.SetParent(_panel.transform);
        var infoPanelRect = infoPanel.AddComponent<RectTransform>();
        infoPanelRect.anchorMin = new Vector2(0.70f, 0.30f);
        infoPanelRect.anchorMax = new Vector2(0.95f, 0.85f);
        infoPanelRect.sizeDelta = Vector2.zero;
        infoPanelRect.anchoredPosition = Vector2.zero;

        
        var infoBg = infoPanel.AddComponent<Image>();
        infoBg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        var infoLayout = infoPanel.AddComponent<VerticalLayoutGroup>();
        infoLayout.childAlignment = TextAnchor.UpperCenter;
        infoLayout.spacing = 20;
        infoLayout.padding = new RectOffset(20, 20, 20, 20);
        infoLayout.childControlHeight = false;
        infoLayout.childControlWidth = true;
        infoLayout.childForceExpandHeight = false;
        infoLayout.childForceExpandWidth = true;

        
        _mapInfoText = CreateSimpleText("MapInfo", infoPanel.transform, 28, FontStyles.Bold);
        _mapInfoText.text = "地图: 加载中...";
        _mapInfoText.alignment = TextAlignmentOptions.Center;
        _mapInfoText.color = new Color(0.9f, 0.9f, 0.5f, 1f);
        var mapRect = _mapInfoText.GetComponent<RectTransform>();
        mapRect.sizeDelta = new Vector2(0, 40);

        
        _timeInfoText = CreateSimpleText("TimeInfo", infoPanel.transform, 24, FontStyles.Normal);
        _timeInfoText.text = "时间: --:--";
        _timeInfoText.alignment = TextAlignmentOptions.Center;
        _timeInfoText.color = new Color(0.7f, 0.9f, 1f, 1f);
        var timeRect = _timeInfoText.GetComponent<RectTransform>();
        timeRect.sizeDelta = new Vector2(0, 35);

        
        _weatherInfoText = CreateSimpleText(
            "WeatherInfo",
            infoPanel.transform,
            24,
            FontStyles.Normal
        );
        _weatherInfoText.text = "天气: 未知";
        _weatherInfoText.alignment = TextAlignmentOptions.Center;
        _weatherInfoText.color = new Color(0.7f, 0.9f, 1f, 1f);
        var weatherRect = _weatherInfoText.GetComponent<RectTransform>();
        weatherRect.sizeDelta = new Vector2(0, 35);

        
        var bottomSyncPanel = new GameObject("BottomSyncPanel");
        bottomSyncPanel.transform.SetParent(_panel.transform);
        var bottomSyncRect = bottomSyncPanel.AddComponent<RectTransform>();
        bottomSyncRect.anchorMin = new Vector2(0.3f, 0);
        bottomSyncRect.anchorMax = new Vector2(0.7f, 0);
        bottomSyncRect.anchoredPosition = new Vector2(0, 100);
        bottomSyncRect.sizeDelta = new Vector2(0, 120);

        var bottomSyncLayout = bottomSyncPanel.AddComponent<HorizontalLayoutGroup>();
        bottomSyncLayout.childAlignment = TextAnchor.MiddleCenter;
        bottomSyncLayout.spacing = 20;
        bottomSyncLayout.childControlHeight = false;
        bottomSyncLayout.childControlWidth = false;
        bottomSyncLayout.childForceExpandHeight = false;
        bottomSyncLayout.childForceExpandWidth = false;

        
        _loadingAnimation = CreateLoadingAnimation(bottomSyncPanel.transform);
        var loadingRect = _loadingAnimation.GetComponent<RectTransform>();
        loadingRect.sizeDelta = new Vector2(40, 40);

        
        var textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(bottomSyncPanel.transform);
        var textContainerRect = textContainer.AddComponent<RectTransform>();
        textContainerRect.sizeDelta = new Vector2(500, 80);

        var textVerticalLayout = textContainer.AddComponent<VerticalLayoutGroup>();
        textVerticalLayout.childAlignment = TextAnchor.MiddleLeft;
        textVerticalLayout.spacing = 5;
        textVerticalLayout.childControlHeight = false;
        textVerticalLayout.childControlWidth = true;
        textVerticalLayout.childForceExpandHeight = false;
        textVerticalLayout.childForceExpandWidth = true;

        
        _syncStatusText = CreateSimpleText(
            "SyncStatus",
            textContainer.transform,
            28,
            FontStyles.Normal
        );
        _syncStatusText.text = "初始化中...";
        _syncStatusText.alignment = TextAlignmentOptions.Left;
        _syncStatusText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var statusRect = _syncStatusText.GetComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(0, 40);

        
        _syncPercentText = CreateSimpleText(
            "SyncPercent",
            textContainer.transform,
            32,
            FontStyles.Bold
        );
        _syncPercentText.text = "0%";
        _syncPercentText.alignment = TextAlignmentOptions.Left;
        _syncPercentText.color = new Color(0.5f, 1f, 0.5f, 1f);
        var percentRect = _syncPercentText.GetComponent<RectTransform>();
        percentRect.sizeDelta = new Vector2(0, 40);
            
            Debug.Log("[SYNC_UI] CreateUI COMPLETE");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] CreateUI ERROR: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void Update()
    {
        
        if (_panel != null && _panel.activeSelf && _fpsCheckEnabled)
        {
            UpdateFPSMonitor();
        }

        
        if (_panel != null && _panel.activeSelf)
        {
            float elapsedTime = Time.time - _uiShowTime;

            
            if (elapsedTime > MAX_UI_DISPLAY_TIME)
            {
                Debug.LogWarning(
                    $"[SYNC_UI] ⚠️ 超时保护触发！UI已显示 {elapsedTime:F1} 秒，强制关闭"
                );
                ForceClose("超时保护");
                return;
            }

            
            CheckStuckTasks();
        }

        
        if (_loadingAnimation != null && _loadingAnimation.activeSelf)
        {
            _loadingRotation += 360f * Time.deltaTime; 
            if (_loadingRotation >= 360f)
                _loadingRotation -= 360f;
            _loadingAnimation.transform.rotation = Quaternion.Euler(0, 0, -_loadingRotation);
        }

        
        UpdateProgressDisplay();

        
        UpdateMapAndWeatherInfo();

        
        if (!_allTasksCompleted && _panel != null && _panel.activeSelf)
        {
            CheckAndHideIfComplete();
        }
    }

    private void UpdateProgressDisplay()
    {
        if (_syncStatusText == null || _syncPercentText == null)
            return;
        if (!_panel.activeSelf)
            return;

        try
        {
            int completed = _syncTasks.Count(t => t.Value.IsCompleted);
            int total = _syncTasks.Count;

            if (total == 0)
            {
                _syncStatusText.text = "初始化中...";
                _syncPercentText.text = "0%";
                return;
            }

            
            float percent = (float)completed / total * 100f;

            
            if (percent >= 75f && !_autoProgressEnabled)
            {
                _autoProgressEnabled = true;
                _autoProgressPercent = percent;
                _lastAutoProgressTime = Time.time;
                Debug.Log($"[SYNC_UI] 启用自动进度增长，当前进度: {percent:F0}%");

                
                EnableCharacterInvincibility();
            }

            
            if (_autoProgressEnabled)
            {
                float timeSinceLastUpdate = Time.time - _lastAutoProgressTime;
                if (timeSinceLastUpdate >= 1f)
                {
                    
                    float increment;
                    if (_autoProgressPercent < 80f)
                    {
                        increment = 1f; 
                    }
                    else if (_autoProgressPercent < 90f)
                    {
                        increment = 0.5f; 
                    }
                    else
                    {
                        increment = 0.1f; 
                    }

                    _autoProgressPercent += increment;
                    _lastAutoProgressTime = Time.time;
                }

                
                percent = Mathf.Min(_autoProgressPercent, 100f);

                
                if (percent < 99.8f)
                {
                    
                    if (
                        _invincibilityTargetHealth != null
                        && !_invincibilityTargetHealth.Invincible
                    )
                    {
                        _invincibilityTargetHealth.SetInvincible(true);
                        Debug.Log($"[SYNC_UI] 重新启用无敌状态 (进度: {percent:F0}%)");
                    }
                }

                
                if (percent >= 100f)
                {
                    Debug.Log("[SYNC_UI] 进度达到100%，立即关闭UI");
                    _syncStatusText.text = "加载完成！";
                    Close(); 
                    return;
                }
            }

            _syncPercentText.text = $"{percent:F0}%";

            
            if (percent >= 100f)
            {
                _syncPercentText.color = new Color(0.5f, 1f, 0.5f, 1f); 
            }
            else if (percent >= 50f)
            {
                _syncPercentText.color = new Color(1f, 1f, 0.5f, 1f); 
            }
            else
            {
                _syncPercentText.color = new Color(1f, 0.7f, 0.5f, 1f); 
            }

            
            if (_autoProgressEnabled)
            {
                _syncStatusText.text = "即将完成...";
            }
            else
            {
                var currentTask = _syncTasks.FirstOrDefault(t => !t.Value.IsCompleted);
                if (currentTask.Value != null)
                {
                    string detail = string.IsNullOrEmpty(currentTask.Value.Details)
                        ? ""
                        : $" - {currentTask.Value.Details}";
                    _syncStatusText.text = $"{currentTask.Value.Name}{detail}";
                }
                else
                {
                    _syncStatusText.text = "同步完成！";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SYNC_UI] 更新进度显示失败: {ex.Message}");
        }
    }

    private void UpdateMapAndWeatherInfo()
    {
        if (_mapInfoText == null || _timeInfoText == null || _weatherInfoText == null)
            return;
        if (!_panel.activeSelf)
            return;

        try
        {
            
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            _mapInfoText.text = $"地图: {GetMapDisplayName(currentScene.name)}";

            
            try
            {
                var day = GameClock.Day;
                var timeOfDay = GameClock.TimeOfDay;
                var hours = timeOfDay.Hours;
                var minutes = timeOfDay.Minutes;
                _timeInfoText.text = $"时间: 第{day}天 {hours:D2}:{minutes:D2}";
            }
            catch
            {
                _timeInfoText.text = "时间: --:--";
            }

            
            try
            {
                if (TimeOfDayController.Instance != null)
                {
                    var currentWeather = TimeOfDayController.Instance.CurrentWeather;
                    var weatherName = TimeOfDayController.GetWeatherNameByWeather(currentWeather);
                    _weatherInfoText.text = $"天气: {weatherName}";
                }
                else
                {
                    _weatherInfoText.text = "天气: 未知";
                }
            }
            catch
            {
                _weatherInfoText.text = "天气: 未知";
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SYNC_UI] 更新地图天气信息失败: {ex.Message}");
        }
    }

    private string GetMapDisplayName(string sceneName)
    {
        
        
        string sceneId = ExtractSceneId(sceneName);

        
        return Utils.SceneNameMapper.GetDisplayName(sceneId);
    }

    
    
    
    
    private string ExtractSceneId(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return sceneName;

        
        if (sceneName.StartsWith("Base"))
            return "Base";

        
        if (sceneName.StartsWith("Level_"))
        {
            var parts = sceneName.Split('_');
            if (parts.Length >= 2)
                return parts[1]; 
        }

        
        return sceneName;
    }

    
    
    
    private void UpdateFPSMonitor()
    {
        
        float currentFPS = 1f / Time.unscaledDeltaTime;

        
        _fpsHistory.Enqueue(currentFPS);

        
        if (_fpsHistory.Count > FPS_HISTORY_SIZE)
        {
            _fpsHistory.Dequeue();
        }

        
        if (_fpsHistory.Count >= FPS_HISTORY_SIZE / 2) 
        {
            float avgFPS = _fpsHistory.Average();

            
            if (avgFPS >= MIN_STABLE_FPS)
            {
                if (!_fpsIsStable)
                {
                    
                    if (_fpsStableStartTime == 0f)
                    {
                        _fpsStableStartTime = Time.time;
                        Debug.Log($"[SYNC_UI_FPS] 📊 帧率开始恢复：当前平均 {avgFPS:F1} FPS");
                    }
                    
                    else if (Time.time - _fpsStableStartTime >= FPS_STABLE_DURATION)
                    {
                        _fpsIsStable = true;
                        Debug.Log($"[SYNC_UI_FPS] ✅ 帧率已稳定：平均 {avgFPS:F1} FPS（已维持 {FPS_STABLE_DURATION} 秒）");

                        
                        CheckAndHideIfComplete();
                    }
                }
            }
            else
            {
                
                if (_fpsStableStartTime != 0f)
                {
                    Debug.Log($"[SYNC_UI_FPS] ⚠️ 帧率波动：当前平均 {avgFPS:F1} FPS，重置稳定计时器");
                }
                _fpsStableStartTime = 0f;
                _fpsIsStable = false;
            }

            
            if ((int)Time.time % 3 == 0 && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[SYNC_UI_FPS] 📊 当前平均帧率: {avgFPS:F1} FPS，稳定状态: {(_fpsIsStable ? "已稳定" : "未稳定")}");
            }
        }
    }

    
    
    
    private void CheckStuckTasks()
    {
        if (_syncTasks.Count == 0)
            return;

        bool anyTaskStuck = false;
        foreach (var kv in _syncTasks.ToList()) 
        {
            if (kv.Value.IsCompleted)
                continue;

            
            if (_taskLastUpdateTime.TryGetValue(kv.Key, out float lastUpdate))
            {
                float timeSinceUpdate = Time.time - lastUpdate;
                if (timeSinceUpdate > TASK_STUCK_TIMEOUT)
                {
                    Debug.LogWarning(
                        $"[SYNC_UI] ⚠️ 任务卡住检测：{kv.Value.Name} 已 {timeSinceUpdate:F1} 秒未更新，自动标记为完成"
                    );
                    kv.Value.IsCompleted = true;
                    kv.Value.Details = "（超时自动完成）";
                    anyTaskStuck = true;
                }
            }
        }

        
        if (anyTaskStuck)
        {
            CheckAndHideIfComplete();
        }
    }

    private void CheckAndHideIfComplete()
    {
        if (_syncTasks.Count == 0)
            return;

        bool allComplete = _syncTasks.All(t => t.Value.IsCompleted);
        if (allComplete)
        {
            _allTasksCompleted = true;

            
            if (_fpsCheckEnabled && !_fpsIsStable)
            {
                Debug.Log("[SYNC_UI] ✅ 所有任务完成，但帧率未稳定（等待帧率恢复...）");
                
                if (_syncStatusText != null)
                {
                    _syncStatusText.text = "所有任务完成，等待性能优化完成...";
                }
                return; 
            }

            Debug.Log("[SYNC_UI] ✅ 所有任务完成且帧率稳定，1秒后隐藏");
            StartCoroutine(HideAfterDelay(1f)); 
        }
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        
        if (_panel != null && _panel.activeSelf)
        {
            
            if (_fpsCheckEnabled && !_fpsIsStable)
            {
                Debug.LogWarning("[SYNC_UI] ⚠️ 延迟隐藏被阻止：帧率未稳定");
                yield break; 
            }

            Hide();
        }
    }

    
    
    
    private void ForceClose(string reason)
    {
        Debug.LogWarning($"[SYNC_UI] 🔴 强制关闭UI：{reason}");

        try
        {
            
            if (_fadeOutCoroutine != null)
            {
                StopCoroutine(_fadeOutCoroutine);
                _fadeOutCoroutine = null;
            }
            StopAllCoroutines();

            
            DisableCharacterInvincibility();

            
            _fpsCheckEnabled = false;
            _fpsHistory.Clear();
            _fpsIsStable = false;
            _fpsStableStartTime = 0f;

            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            
            _allTasksCompleted = true;
            _autoProgressEnabled = false;

            Debug.Log($"[SYNC_UI] ✅ 强制关闭完成：{reason}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] 强制关闭失败: {ex.Message}");
        }
    }

    private void LoadBackgroundImage(Image targetImage)
    {
        try
        {
            
            var modPath = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location
            );
            var bgPath = Path.Combine(modPath, "Assets", "bg.png");

            Debug.Log($"[SYNC_UI] 尝试加载背景图片: {bgPath}");

            if (File.Exists(bgPath))
            {
                
                var fileData = File.ReadAllBytes(bgPath);

                
                var texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    
                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    
                    targetImage.sprite = sprite;
                    targetImage.color = Color.white; 
                    targetImage.type = Image.Type.Simple;
                    targetImage.preserveAspect = false; 

                    Debug.Log($"[SYNC_UI] 背景图片加载成功: {texture.width}x{texture.height}");
                }
                else
                {
                    Debug.LogWarning("[SYNC_UI] 无法解析图片数据，使用纯黑背景");
                    targetImage.color = Color.black;
                }
            }
            else
            {
                Debug.LogWarning($"[SYNC_UI] 背景图片不存在: {bgPath}，使用纯黑背景");
                targetImage.color = Color.black;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] 加载背景图片失败: {ex.Message}");
            targetImage.color = Color.black;
        }
    }

    private TMP_Text CreateSimpleText(
        string name,
        Transform parent,
        int fontSize,
        FontStyles fontStyle
    )
    {
        var textObj = new GameObject(name);
        textObj.transform.SetParent(parent);

        var rectTransform = textObj.AddComponent<RectTransform>();

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    private GameObject CreateButton(string name, Transform parent, string buttonText)
    {
        var buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent);

        var buttonRect = buttonObj.AddComponent<RectTransform>();
        var button = buttonObj.AddComponent<Button>();
        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.4f, 0.6f, 0.9f);

        
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = buttonText;
        text.fontSize = 24;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        
        
        
        
        
        
        
        

        
        var colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.4f, 0.6f, 0.9f);
        colors.highlightedColor = new Color(0.3f, 0.5f, 0.7f, 1f);
        colors.pressedColor = new Color(0.15f, 0.35f, 0.55f, 1f);
        button.colors = colors;

        return buttonObj;
    }

    private GameObject CreateLoadingAnimation(Transform parent)
    {
        var loadingObj = new GameObject("LoadingAnimation");
        loadingObj.transform.SetParent(parent);

        var rectTransform = loadingObj.AddComponent<RectTransform>();
        var image = loadingObj.AddComponent<Image>();

        
        var texture = new Texture2D(64, 64);
        var pixels = new Color[64 * 64];

        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dx = x - 32f;
                float dy = y - 32f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                
                if (distance > 22 && distance < 28)
                {
                    
                    float normalizedAngle = (angle + 180f) / 360f;
                    if (normalizedAngle < 0.75f) 
                    {
                        float alpha = Mathf.Clamp01(normalizedAngle / 0.75f);
                        pixels[y * 64 + x] = new Color(0.7f, 0.9f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * 64 + x] = Color.clear;
                    }
                }
                else
                {
                    pixels[y * 64 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        image.sprite = sprite;

        return loadingObj;
    }

    
    
    
    private void CreateCloseButton()
    {
        var closeButtonObj = new GameObject("CloseButton");
        closeButtonObj.transform.SetParent(_panel.transform);

        var buttonRect = closeButtonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 1);
        buttonRect.anchorMax = new Vector2(1, 1);
        buttonRect.pivot = new Vector2(1, 1);
        buttonRect.anchoredPosition = new Vector2(-30, -30); 
        buttonRect.sizeDelta = new Vector2(60, 60); 

        var button = closeButtonObj.AddComponent<Button>();
        var buttonImage = closeButtonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 0.8f); 

        
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(closeButtonObj.transform);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "X";
        text.fontSize = 40;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        
        button.onClick.AddListener(() =>
        {
            Debug.Log("[SYNC_UI] 用户点击关闭按钮");
            Close(); 
        });

        
        var colors = button.colors;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        button.colors = colors;
    }

    

    private GameObject CreatePlayerAvatar(ulong steamId = 0)
    {
        var avatarObj = new GameObject("Avatar");
        var rectTransform = avatarObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(96, 96); 

        var image = avatarObj.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f); 

        
        bool isSteamMode = ModBehaviourF.Instance?.TransportMode == NetworkTransportMode.SteamP2P;
        bool canLoadSteamAvatar = steamId > 0 && isSteamMode && SteamManager.Initialized;

        if (canLoadSteamAvatar)
        {
            Debug.Log($"[SYNC_UI] 准备加载Steam头像: SteamID={steamId}");

            
            if (_steamAvatarCache.TryGetValue(steamId, out var cachedSprite))
            {
                image.sprite = cachedSprite;
                image.color = Color.white;
                Debug.Log($"[SYNC_UI] ✓ 使用缓存的Steam头像: {steamId}");
            }
            else
            {
                
                Debug.Log($"[SYNC_UI] 开始异步加载Steam头像: {steamId}");
                StartCoroutine(LoadSteamAvatar(new CSteamID(steamId), image));
            }
        }
        else
        {
            
            if (!isSteamMode)
            {
                Debug.Log($"[SYNC_UI] 直连模式，使用默认头像");
            }
            else if (!SteamManager.Initialized)
            {
                Debug.LogWarning($"[SYNC_UI] Steam未初始化，使用默认头像");
            }
            else
            {
                Debug.LogWarning($"[SYNC_UI] SteamID无效({steamId})，使用默认头像");
            }

            
            var texture = new Texture2D(64, 64);
            var pixels = new Color[64 * 64];

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 32f;
                    float dy = y - 32f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    
                    if (distance < 30)
                    {
                        pixels[y * 64 + x] = new Color(0.3f, 0.5f, 0.7f, 1f);

                        
                        float headDx = dx;
                        float headDy = dy + 8;
                        float headDist = Mathf.Sqrt(headDx * headDx + headDy * headDy);
                        if (headDist < 10)
                        {
                            pixels[y * 64 + x] = new Color(0.9f, 0.9f, 0.9f, 1f);
                        }

                        
                        if (y < 28 && Mathf.Abs(dx) < 12)
                        {
                            pixels[y * 64 + x] = new Color(0.9f, 0.9f, 0.9f, 1f);
                        }
                    }
                    else
                    {
                        pixels[y * 64 + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            image.sprite = sprite;
        }

        return avatarObj;
    }

    private IEnumerator LoadSteamAvatar(CSteamID steamId, Image targetImage)
    {
        if (targetImage == null)
        {
            Debug.LogWarning($"[SYNC_UI] targetImage为null: {steamId}");
            yield break;
        }

        Debug.Log($"[SYNC_UI] 开始加载Steam头像: {steamId}");

        
        int avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);

        
        if (avatarHandle == -1)
        {
            avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);
        }

        
        int maxRetries = 10;
        int retryCount = 0;
        while (avatarHandle == -1 && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.1f);
            avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);
            if (avatarHandle == -1)
            {
                avatarHandle = SteamFriends.GetMediumFriendAvatar(steamId);
            }
            retryCount++;
        }

        if (avatarHandle <= 0)
        {
            Debug.LogWarning($"[SYNC_UI] 无法获取Steam头像句柄: {steamId}");
            yield break;
        }

        if (avatarHandle > 0)
        {
            uint width,
                height;
            if (SteamUtils.GetImageSize(avatarHandle, out width, out height))
            {
                Debug.Log($"[SYNC_UI] Steam头像尺寸: {width}x{height}");
                if (width > 0 && height > 0)
                {
                    byte[] imageData = new byte[width * height * 4];
                    if (SteamUtils.GetImageRGBA(avatarHandle, imageData, (int)(width * height * 4)))
                    {
                        Texture2D texture = new Texture2D(
                            (int)width,
                            (int)height,
                            TextureFormat.RGBA32,
                            false
                        );
                        texture.LoadRawTextureData(imageData);
                        texture.Apply();

                        
                        for (int y = 0; y < height / 2; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                Color temp = texture.GetPixel(x, y);
                                texture.SetPixel(x, y, texture.GetPixel(x, (int)height - 1 - y));
                                texture.SetPixel(x, (int)height - 1 - y, temp);
                            }
                        }
                        texture.Apply();

                        Sprite sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, width, height),
                            new Vector2(0.5f, 0.5f)
                        );
                        _steamAvatarCache[steamId.m_SteamID] = sprite;

                        if (targetImage != null)
                        {
                            targetImage.sprite = sprite;
                            targetImage.color = Color.white;
                        }
                        Debug.Log($"[SYNC_UI] Steam头像加载成功并缓存: {steamId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[SYNC_UI] GetImageRGBA 失败: {steamId}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[SYNC_UI] GetImageSize 失败: {steamId}");
            }
        }
    }

    private GameObject CreatePlayerEntry(string playerName, string playerEndPoint)
    {
        
        var entryObj = new GameObject($"Player_{playerName}");
        entryObj.transform.SetParent(_playerListContainer.transform);

        var entryRect = entryObj.AddComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(0, 112); 

        var horizontalLayout = entryObj.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
        horizontalLayout.spacing = 20; 
        horizontalLayout.padding = new RectOffset(8, 8, 8, 8); 
        horizontalLayout.childControlHeight = false;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childForceExpandHeight = false;
        horizontalLayout.childForceExpandWidth = false;

        
        string displayName = playerName;
        ulong steamId = 0;
        bool isSteamMode = ModBehaviourF.Instance?.TransportMode == NetworkTransportMode.SteamP2P;

        if (isSteamMode && SteamManager.Initialized)
        {
            try
            {
                
                steamId = GetSteamIdFromEndPoint(playerEndPoint);

                if (steamId > 0)
                {
                    
                    var cSteamId = new CSteamID(steamId);
                    string steamUsername = "Unknown";

                    
                    var lobbyManager = SteamLobbyManager.Instance;
                    if (lobbyManager != null && lobbyManager.IsInLobby)
                    {
                        steamUsername = lobbyManager.GetCachedMemberName(cSteamId);

                        
                        if (string.IsNullOrEmpty(steamUsername))
                        {
                            steamUsername = SteamFriends.GetFriendPersonaName(cSteamId);
                            if (string.IsNullOrEmpty(steamUsername) || steamUsername == "[unknown]")
                            {
                                steamUsername =
                                    $"Player_{steamId.ToString().Substring(Math.Max(0, steamId.ToString().Length - 4))}";
                            }
                        }

                        
                        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(
                            new CSteamID(lobbyManager.CurrentLobbyId)
                        );
                        bool isHost = (steamId == lobbyOwner.m_SteamID);
                        string prefix = isHost ? "HOST" : "CLIENT";
                        displayName = $"{prefix}_{steamUsername}";
                    }
                    else
                    {
                        
                        if (steamId == SteamUser.GetSteamID().m_SteamID)
                        {
                            steamUsername = SteamFriends.GetPersonaName();
                            bool isHost = ModBehaviourF.Instance?.IsServer ?? false;
                            string prefix = isHost ? "HOST" : "CLIENT";
                            displayName = $"{prefix}_{steamUsername}";
                        }
                        else
                        {
                            steamUsername = SteamFriends.GetFriendPersonaName(cSteamId);
                            if (
                                !string.IsNullOrEmpty(steamUsername)
                                && steamUsername != "[unknown]"
                            )
                            {
                                displayName = $"CLIENT_{steamUsername}";
                            }
                        }
                    }

                    Debug.Log($"[SYNC_UI] Steam 玩家: {displayName} (SteamID: {steamId})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SYNC_UI] 获取Steam用户名失败: {ex.Message}");
            }
        }

        
        var avatar = CreatePlayerAvatar(steamId);
        avatar.transform.SetParent(entryObj.transform);

        
        var playerText = CreateSimpleText("Name", entryObj.transform, 52, FontStyles.Normal); 
        playerText.text = displayName;
        playerText.alignment = TextAlignmentOptions.Left;
        playerText.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        var textRect = playerText.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(500, 96); 

        return entryObj;
    }

    private ulong GetSteamIdFromEndPoint(string endPoint)
    {
        if (string.IsNullOrEmpty(endPoint))
            return 0;

        
        
        try
        {
            var mod = ModBehaviourF.Instance;
            if (mod != null && mod.playerStatuses != null)
            {
                foreach (var kv in mod.playerStatuses)
                {
                    var status = kv.Value;
                    if (status?.EndPoint == endPoint)
                    {
                        
                        if (!string.IsNullOrEmpty(status.ClientReportedId))
                        {
                            
                            if (status.ClientReportedId.Contains(":"))
                            {
                                var parts = status.ClientReportedId.Split(':');
                                if (parts.Length > 1 && ulong.TryParse(parts[1], out ulong steamId))
                                {
                                    return steamId;
                                }
                            }
                            
                            if (ulong.TryParse(status.ClientReportedId, out ulong directSteamId))
                            {
                                return directSteamId;
                            }
                        }
                        break;
                    }
                }
            }

            
            if (endPoint.Contains(":"))
            {
                var parts = endPoint.Split(':');
                foreach (var part in parts)
                {
                    if (ulong.TryParse(part, out ulong steamId) && steamId > 76561197960265728) 
                    {
                        return steamId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SYNC_UI] GetSteamIdFromEndPoint异常: {ex.Message}");
        }

        return 0;
    }

    
    
    
    public void Show()
    {
        Debug.Log(
            $"[SYNC_UI] Show() 被调用，_panel={(_panel != null ? "存在" : "null")}, _canvas={(_canvas != null ? "存在" : "null")}"
        );

        
        if (_fadeOutCoroutine != null)
        {
            StopCoroutine(_fadeOutCoroutine);
            _fadeOutCoroutine = null;
            Debug.Log("[SYNC_UI] 停止淡出协程");
        }

        
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        if (_canvas != null)
        {
            _canvas.enabled = true;
            Debug.Log($"[SYNC_UI] Canvas已启用，sortingOrder={_canvas.sortingOrder}");
        }

        if (_panel != null)
        {
            _panel.SetActive(true);
            Debug.Log("[SYNC_UI] Panel已激活");
        }

        _syncTasks.Clear();
        _allTasksCompleted = false;

        
        _autoProgressEnabled = false;
        _autoProgressPercent = 0f;
        _lastAutoProgressTime = 0f;

        
        _uiShowTime = Time.time;
        _taskLastUpdateTime.Clear();
        Debug.Log($"[SYNC_UI] 超时保护已启动，最大显示时间: {MAX_UI_DISPLAY_TIME} 秒");

        
        _fpsCheckEnabled = ModBehaviourF.Instance != null && !ModBehaviourF.Instance.IsServer;
        _fpsHistory.Clear();
        _fpsIsStable = false;
        _fpsStableStartTime = 0f;

        if (_fpsCheckEnabled)
        {
            Debug.Log("[SYNC_UI_FPS] ✅ 帧率检测已启用（客户端模式）");
        }
        else
        {
            Debug.Log("[SYNC_UI_FPS] ⚠️ 帧率检测未启用（主机模式）");
        }

        
        EnableCharacterInvincibility();

        Debug.Log("[SYNC_UI] 显示同步等待界面完成");
    }

    
    
    
    public void Hide()
    {
        
        StartInvincibilityTimer();

        
        _fpsCheckEnabled = false;
        _fpsHistory.Clear();
        _fpsIsStable = false;
        _fpsStableStartTime = 0f;

        
        _fpsCheckEnabled = false;
        _fpsHistory.Clear();
        _fpsIsStable = false;
        _fpsStableStartTime = 0f;

        if (_panel != null && _panel.activeSelf)
        {
            
            if (_fadeOutCoroutine != null)
            {
                StopCoroutine(_fadeOutCoroutine);
            }

            
            _fadeOutCoroutine = StartCoroutine(FadeOut());
        }
        else if (_panel != null)
        {
            _panel.SetActive(false);
        }

        Debug.Log("[SYNC_UI] 开始淡出隐藏同步等待界面");
    }

    
    
    
    public void Close()
    {
        
        StartInvincibilityTimer();

        
        if (_fadeOutCoroutine != null)
        {
            StopCoroutine(_fadeOutCoroutine);
            _fadeOutCoroutine = null;
        }

        
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }

        if (_panel != null)
        {
            _panel.SetActive(false);
        }

        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        Debug.Log("[SYNC_UI] 立即关闭同步等待界面");
    }

    
    
    
    private IEnumerator FadeOut()
    {
        if (_canvasGroup == null)
        {
            Debug.LogWarning("[SYNC_UI] CanvasGroup为空，直接隐藏");
            _panel.SetActive(false);
            yield break;
        }

        float duration = 0.5f; 
        float elapsed = 0f;
        float startAlpha = _canvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, normalizedTime);
            yield return null;
        }

        
        _canvasGroup.alpha = 0f;

        
        _panel.SetActive(false);
        _fadeOutCoroutine = null;

        Debug.Log("[SYNC_UI] 淡出完成，隐藏同步等待界面");
    }

    
    
    
    public void RegisterTask(string taskId, string taskName)
    {
        if (!_syncTasks.ContainsKey(taskId))
        {
            _syncTasks[taskId] = new SyncTaskStatus
            {
                Name = taskName,
                IsCompleted = false,
                Details = "",
            };
            
            _taskLastUpdateTime[taskId] = Time.time;
            Debug.Log($"[SYNC_UI] 注册任务: {taskName}");
        }
    }

    
    
    
    public void UpdateTaskStatus(string taskId, bool isCompleted, string details = "")
    {
        if (_syncTasks.TryGetValue(taskId, out var task))
        {
            task.IsCompleted = isCompleted;
            task.Details = details;

            
            if (!isCompleted)
            {
                _taskLastUpdateTime[taskId] = Time.time;
            }

            Debug.Log(
                $"[SYNC_UI] 任务状态更新: {task.Name} - {(isCompleted ? "完成" : "进行中")} {details}"
            );
        }
    }

    
    
    
    public void CompleteTask(string taskId, string details = "")
    {
        UpdateTaskStatus(taskId, true, details);
    }

    
    
    
    public void ForceCloseIfVisible(string reason = "外部请求")
    {
        if (_panel != null && _panel.activeSelf)
        {
            ForceClose(reason);
        }
    }

    
    
    
    public void UpdatePlayerList()
    {
        if (_playerListContainer == null)
            return;

        try
        {
            
            foreach (Transform child in _playerListContainer.transform)
            {
                Destroy(child.gameObject);
            }

            
            var mod = ModBehaviourF.Instance;
            if (mod == null || mod.playerStatuses == null)
            {
                var emptyText = CreateSimpleText(
                    "Empty",
                    _playerListContainer.transform,
                    20,
                    FontStyles.Italic
                );
                emptyText.text = "正在获取玩家列表...";
                emptyText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                emptyText.alignment = TextAlignmentOptions.Center;
                var emptyRect = emptyText.GetComponent<RectTransform>();
                emptyRect.sizeDelta = new Vector2(0, 40);
                return;
            }

            
            if (mod.localPlayerStatus != null)
            {
                CreatePlayerEntry(mod.localPlayerStatus.PlayerName, mod.localPlayerStatus.EndPoint);
            }

            
            foreach (var kv in mod.playerStatuses)
            {
                var status = kv.Value;
                if (status != null)
                {
                    CreatePlayerEntry(status.PlayerName, status.EndPoint);
                }
            }

            Debug.Log($"[SYNC_UI] 更新玩家列表完成，共 {mod.playerStatuses.Count + 1} 名玩家");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SYNC_UI] 更新玩家列表失败: {ex.Message}");
        }
    }

    
    
    
    private void EnableCharacterInvincibility()
    {
        try
        {
            var character = CharacterMainControl.Main;
            if (character == null)
            {
                Debug.LogWarning("[SYNC_UI] 无法启用无敌：角色为空");
                return;
            }

            var health = character.Health;
            if (health == null)
            {
                Debug.LogWarning("[SYNC_UI] 无法启用无敌：Health组件为空");
                return;
            }

            
            if (_invincibilityTargetHealth != null && _invincibilityTargetHealth != health)
            {
                DisableCharacterInvincibility();
            }

            
            if (_originalInvincibleState == null)
            {
                _originalInvincibleState = health.Invincible;
                Debug.Log($"[SYNC_UI] 保存原始无敌状态: {_originalInvincibleState.Value}");
            }

            
            if (!health.Invincible)
            {
                health.SetInvincible(true);
                Debug.Log("[SYNC_UI] ✅ 已启用角色无敌");
            }
            else
            {
                Debug.Log("[SYNC_UI] 角色已处于无敌状态");
            }

            _invincibilityTargetHealth = health;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] 启用无敌失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private void DisableCharacterInvincibility()
    {
        try
        {
            if (_invincibilityTargetHealth != null && _originalInvincibleState != null)
            {
                
                _invincibilityTargetHealth.SetInvincible(_originalInvincibleState.Value);
                Debug.Log($"[SYNC_UI] ✅ 已恢复角色无敌状态为: {_originalInvincibleState.Value}");

                
                float maxHealth = _invincibilityTargetHealth.MaxHealth;
                _invincibilityTargetHealth.CurrentHealth = maxHealth;
                Debug.Log($"[SYNC_UI] ✅ 已恢复角色HP为最大值: {maxHealth}");
            }
            else if (_invincibilityTargetHealth == null && _originalInvincibleState != null)
            {
                Debug.LogWarning("[SYNC_UI] Health对象已失效，无法恢复无敌状态");
            }

            _invincibilityTargetHealth = null;
            _originalInvincibleState = null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] 解除无敌失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    
    
    
    private void StartInvincibilityTimer()
    {
        
        if (_invincibilityTimerCoroutine != null)
        {
            StopCoroutine(_invincibilityTimerCoroutine);
            _invincibilityTimerCoroutine = null;
        }

        
        _invincibilityTimerCoroutine = StartCoroutine(InvincibilityTimerCoroutine());
    }

    
    
    
    private IEnumerator InvincibilityTimerCoroutine()
    {
        float elapsed = 0f;

        var character = CharacterMainControl.Main;
        if (character == null)
        {
            Debug.LogWarning("[SYNC_UI] 无敌计时器：角色为空，提前结束");
            _invincibilityTimerCoroutine = null;
            yield break;
        }

        var health = character.Health;
        if (health == null)
        {
            Debug.LogWarning("[SYNC_UI] 无敌计时器：Health组件为空，提前结束");
            _invincibilityTimerCoroutine = null;
            yield break;
        }

        
        if (!health.Invincible)
        {
            health.SetInvincible(true);
        }

        
        while (elapsed < INVINCIBILITY_DURATION)
        {
            if (health == null)
            {
                Debug.LogWarning("[SYNC_UI] 无敌计时器：Health对象已失效，提前结束");
                _invincibilityTimerCoroutine = null;
                yield break;
            }

            try
            {
                
                health.CurrentHealth = health.MaxHealth;

                
                if (!health.Invincible)
                {
                    health.SetInvincible(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SYNC_UI] 无敌计时器帧更新异常: {ex.Message}");
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        
        Debug.Log($"[SYNC_UI] 🛡️ 无敌计时器结束（持续 {INVINCIBILITY_DURATION} 秒），解除无敌状态");
        
        try
        {
            DisableCharacterInvincibility();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SYNC_UI] 解除无敌异常: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _invincibilityTimerCoroutine = null;
        }
    }
}
