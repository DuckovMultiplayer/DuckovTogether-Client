using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.UI;

public class SyncStatusUI : MonoBehaviour
{
    public static SyncStatusUI Instance { get; private set; }
    
    private Canvas _canvas;
    private GameObject _panel;
    private CanvasGroup _canvasGroup;
    private TMP_Text _titleText;
    private TMP_Text _statusText;
    private TMP_Text _progressText;
    private Image _progressBar;
    private Image _progressBarBg;
    
    private readonly Dictionary<string, SyncTask> _tasks = new();
    private bool _isVisible;
    private float _targetProgress;
    private float _currentProgress;

    public class SyncTask
    {
        public string Name;
        public bool IsComplete;
        public string Status;
    }

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
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_isVisible) return;
        
        _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * 3f);
        
        if (_progressBar != null)
        {
            _progressBar.fillAmount = _currentProgress;
        }
        
        if (_progressText != null)
        {
            _progressText.text = $"{Mathf.RoundToInt(_currentProgress * 100)}%";
        }
        
        if (_currentProgress >= 0.99f && _tasks.All(t => t.Value.IsComplete))
        {
            StartCoroutine(HideAfterDelay(0.5f));
        }
    }

    private void CreateUI()
    {
        var canvasGO = new GameObject("SyncStatusCanvas");
        canvasGO.transform.SetParent(transform);
        
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 2000;
        
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        _panel = new GameObject("SyncPanel");
        _panel.transform.SetParent(canvasGO.transform, false);
        
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        
        var panelBg = _panel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.9f);
        
        _canvasGroup = _panel.AddComponent<CanvasGroup>();
        
        var contentContainer = new GameObject("Content");
        contentContainer.transform.SetParent(_panel.transform, false);
        var contentRect = contentContainer.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(500, 200);
        
        var layout = contentContainer.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        
        _titleText = CreateText("Title", contentContainer.transform, "Synchronizing...", 32, FontStyles.Bold);
        _statusText = CreateText("Status", contentContainer.transform, "Connecting to server...", 18, FontStyles.Normal);
        _statusText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        
        var progressContainer = new GameObject("ProgressContainer");
        progressContainer.transform.SetParent(contentContainer.transform, false);
        var progressLayout = progressContainer.AddComponent<LayoutElement>();
        progressLayout.preferredHeight = 30;
        progressLayout.preferredWidth = 400;
        
        _progressBarBg = new GameObject("ProgressBg").AddComponent<Image>();
        _progressBarBg.transform.SetParent(progressContainer.transform, false);
        _progressBarBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var bgRect = _progressBarBg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        _progressBar = new GameObject("ProgressFill").AddComponent<Image>();
        _progressBar.transform.SetParent(progressContainer.transform, false);
        _progressBar.color = new Color(0.3f, 0.7f, 0.4f, 1f);
        _progressBar.type = Image.Type.Filled;
        _progressBar.fillMethod = Image.FillMethod.Horizontal;
        _progressBar.fillAmount = 0;
        var fillRect = _progressBar.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        _progressText = CreateText("ProgressText", contentContainer.transform, "0%", 24, FontStyles.Bold);
        _progressText.color = new Color(0.5f, 1f, 0.6f, 1f);
    }

    private TMP_Text CreateText(string name, Transform parent, string text, int fontSize, FontStyles style)
    {
        var textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);
        
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        
        var layout = textGO.AddComponent<LayoutElement>();
        layout.preferredHeight = fontSize + 10;
        
        return tmp;
    }

    public void Show(string title = "Synchronizing...")
    {
        _isVisible = true;
        _currentProgress = 0;
        _targetProgress = 0;
        _tasks.Clear();
        
        if (_titleText != null) _titleText.text = title;
        if (_statusText != null) _statusText.text = "Preparing...";
        if (_panel != null) _panel.SetActive(true);
        if (_canvasGroup != null) _canvasGroup.alpha = 1;
    }

    public void Hide()
    {
        _isVisible = false;
        if (_panel != null) _panel.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        float fadeTime = 0.3f;
        float elapsed = 0;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1 - (elapsed / fadeTime);
            }
            yield return null;
        }
        
        Hide();
    }

    public void RegisterTask(string taskId, string taskName)
    {
        if (!_tasks.ContainsKey(taskId))
        {
            _tasks[taskId] = new SyncTask { Name = taskName, IsComplete = false, Status = "Pending" };
        }
        UpdateProgress();
    }

    public void UpdateTask(string taskId, string status, bool isComplete = false)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = status;
            task.IsComplete = isComplete;
        }
        
        if (_statusText != null && !string.IsNullOrEmpty(status))
        {
            _statusText.text = status;
        }
        
        UpdateProgress();
    }

    public void CompleteTask(string taskId)
    {
        UpdateTask(taskId, "Complete", true);
    }

    private void UpdateProgress()
    {
        if (_tasks.Count == 0)
        {
            _targetProgress = 0;
            return;
        }
        
        int completed = _tasks.Count(t => t.Value.IsComplete);
        _targetProgress = (float)completed / _tasks.Count;
    }

    public void SetProgress(float progress, string status = null)
    {
        _targetProgress = Mathf.Clamp01(progress);
        if (!string.IsNullOrEmpty(status) && _statusText != null)
        {
            _statusText.text = status;
        }
    }
}
