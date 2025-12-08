using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeFromDuckovCoopMod.Utils.Logger.Tools;

namespace EscapeFromDuckovCoopMod.Main.Teleport;

public class TeleportManager : MonoBehaviour
{
    public static TeleportManager Instance { get; private set; }
    
    private Canvas _blackScreenCanvas;
    private Image _blackScreenImage;
    private bool _isBlackScreenActive = false;
    
    private Canvas _hintCanvas;
    private TMP_Text _hintText;
    
    private bool _isMapOpen = false;
    private Vector3 _targetTeleportPosition = Vector3.zero;
    
    public bool IsTeleporting { get; private set; } = false;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        CreateBlackScreen();
        CreateHintUI();
    }
    
    private void CreateBlackScreen()
    {
        var go = new GameObject("TeleportBlackScreen");
        go.transform.SetParent(transform, false);
        
        _blackScreenCanvas = go.AddComponent<Canvas>();
        _blackScreenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _blackScreenCanvas.sortingOrder = 10000;
        
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        go.AddComponent<GraphicRaycaster>();
        
        var blackPanel = new GameObject("BlackPanel");
        blackPanel.transform.SetParent(go.transform, false);
        
        var rect = blackPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        
        _blackScreenImage = blackPanel.AddComponent<Image>();
        _blackScreenImage.color = new Color(0, 0, 0, 0);
        
        go.SetActive(false);
    }
    
    private void CreateHintUI()
    {
        var go = new GameObject("TeleportHintUI");
        go.transform.SetParent(transform, false);
        
        _hintCanvas = go.AddComponent<Canvas>();
        _hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hintCanvas.sortingOrder = 9999;
        
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        go.AddComponent<GraphicRaycaster>();
        
        var textObj = new GameObject("HintText");
        textObj.transform.SetParent(go.transform, false);
        
        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.1f);
        rect.anchorMax = new Vector2(0.5f, 0.1f);
        rect.sizeDelta = new Vector2(800, 100);
        rect.anchoredPosition = Vector2.zero;
        
        _hintText = textObj.AddComponent<TextMeshProUGUI>();
        _hintText.text = "按 T 键传送到鼠标指针位置 | Press T to teleport to cursor position";
        _hintText.fontSize = 24;
        _hintText.color = new Color(1f, 1f, 0f, 1f);
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.fontStyle = FontStyles.Bold;
        
        var shadow = textObj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
        
        go.SetActive(false);
    }
    
    private void Update()
    {
        CheckMapTeleport();
    }
    
    private void CheckMapTeleport()
    {
        if (IsTeleporting) return;
        if (CharacterMainControl.Main == null) return;
        
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            _isMapOpen = !_isMapOpen;
            LoggerHelper.Log($"[Teleport] 地图状态: {(_isMapOpen ? "打开" : "关闭")}");
            
            
            if (_hintCanvas != null)
            {
                _hintCanvas.gameObject.SetActive(_isMapOpen);
            }
        }
        
        
        if (_isMapOpen && Input.GetKeyDown(KeyCode.T))
        {
            
            if (TryGetWorldPositionFromMouse(out Vector3 worldPos))
            {
                _isMapOpen = false;
                
                
                if (_hintCanvas != null)
                {
                    _hintCanvas.gameObject.SetActive(false);
                }
                
                TeleportToPosition(worldPos);
            }
            else
            {
                CharacterMainControl.Main.PopText("无法传送到该位置", 2f);
            }
        }
    }
    
    private bool TryGetWorldPositionFromMouse(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            worldPos = hit.point;
            return true;
        }
        
        return false;
    }
    
    public void TeleportToPosition(Vector3 targetPos)
    {
        if (IsTeleporting) return;
        if (CharacterMainControl.Main == null)
        {
            LoggerHelper.LogWarning("[Teleport] 无法传送：MainCharacter为空");
            return;
        }
        
        StartCoroutine(TeleportWithBlackScreen(targetPos));
    }
    
    public void TeleportPlayerToPlayer(string targetPlayerId)
    {
        if (IsTeleporting) return;
        if (CharacterMainControl.Main == null) return;
        
        var targetPlayer = Utils.Database.PlayerInfoDatabase.Instance.GetPlayerBySteamId(targetPlayerId);
        if (targetPlayer == null)
        {
            LoggerHelper.LogWarning($"[Teleport] 找不到目标玩家: {targetPlayerId}");
            CharacterMainControl.Main.PopText("找不到目标玩家", 2f);
            return;
        }
        
        
        if (targetPlayer.IsLocalPlayer)
        {
            LoggerHelper.LogWarning("[Teleport] 无法传送到自己");
            CharacterMainControl.Main.PopText("无法传送到自己", 2f);
            return;
        }
        
        
        var netService = ModBehaviourF.Instance;
        if (netService == null || netService.netManager == null || !netService.netManager.IsRunning)
        {
            LoggerHelper.LogWarning("[Teleport] 网络未启动");
            CharacterMainControl.Main.PopText("网络未连接", 2f);
            return;
        }
        
        GameObject remoteChar = null;
        
        
        if (netService.clientRemoteCharacters != null)
        {
            foreach (var kvp in netService.clientRemoteCharacters)
            {
                if (kvp.Key == targetPlayerId || kvp.Key.Contains(targetPlayerId))
                {
                    remoteChar = kvp.Value;
                    break;
                }
            }
        }
        
        
        if (remoteChar == null && netService.remoteCharacters != null)
        {
            foreach (var kvp in netService.remoteCharacters)
            {
                
                
                remoteChar = kvp.Value;
                break;
            }
        }
        
        if (remoteChar != null)
        {
            TeleportToPosition(remoteChar.transform.position);
            LoggerHelper.Log($"[Teleport] 传送到玩家: {targetPlayer.PlayerName}");
        }
        else
        {
            LoggerHelper.LogWarning($"[Teleport] 找不到远程玩家实例: {targetPlayerId}");
            CharacterMainControl.Main.PopText("目标玩家不在场景中", 2f);
        }
    }
    
    private IEnumerator TeleportWithBlackScreen(Vector3 targetPos)
    {
        IsTeleporting = true;
        var mainChar = CharacterMainControl.Main;
        
        if (mainChar == null)
        {
            IsTeleporting = false;
            yield break;
        }
        
        LoggerHelper.Log($"[Teleport] 开始传送至: {targetPos}");
        
        
        yield return FadeBlackScreen(0f, 1f, 0.5f);
        
        
        yield return new WaitForSeconds(0.2f);
        
        
        if (TryGetFitPosition(targetPos, out Vector3 fitPos))
        {
            FixZoneTriggerExit(mainChar);
            mainChar.SetPosition(fitPos);
            LoggerHelper.Log($"[Teleport] 传送成功: {fitPos}");
            mainChar.PopText("传送成功", 2f);
        }
        else
        {
            LoggerHelper.LogWarning("[Teleport] 未找到落脚点");
            mainChar.PopText("未找到落脚点", 2f);
        }
        
        
        yield return null;
        
        
        yield return FadeBlackScreen(1f, 0f, 0.5f);
        
        IsTeleporting = false;
    }
    
    private IEnumerator FadeBlackScreen(float from, float to, float duration)
    {
        if (_blackScreenCanvas == null || _blackScreenImage == null)
        {
            yield break;
        }
        
        _blackScreenCanvas.gameObject.SetActive(true);
        _isBlackScreenActive = true;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            _blackScreenImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        
        _blackScreenImage.color = new Color(0, 0, 0, to);
        
        if (to <= 0f)
        {
            _blackScreenCanvas.gameObject.SetActive(false);
            _isBlackScreenActive = false;
        }
    }
    
    private bool TryGetFitPosition(Vector3 targetPos, out Vector3 currentPos)
    {
        currentPos = Vector3.zero;
        
        
        if (Physics.Raycast(new Vector3(targetPos.x, 1000f, targetPos.z), Vector3.down, out RaycastHit raycastHit, float.PositiveInfinity))
        {
            
            currentPos = new Vector3(targetPos.x, raycastHit.point.y + 0.5f, targetPos.z);
            return true;
        }
        
        return false;
    }
    
    private void FixZoneTriggerExit(CharacterMainControl character)
    {
        if (character == null) return;
        
        try
        {
            
            var colliders = Physics.OverlapSphere(character.transform.position, 5f);
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    col.enabled = false;
                    col.enabled = true;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.LogWarning($"[Teleport] FixZoneTriggerExit 失败: {ex.Message}");
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
