using UnityEngine;

namespace EscapeFromDuckovCoopMod;

public class AISceneVisibilityGuard : MonoBehaviour
{
    private string _aiSceneId;
    private CharacterMainControl _cmc;
    private bool _initialized = false;
    
    private void Start()
    {
        _cmc = GetComponent<CharacterMainControl>();
        if (_cmc != null)
        {
            var root = _cmc.GetComponentInParent<CharacterSpawnerRoot>();
            if (root != null)
            {
                _aiSceneId = GetRootSceneId(root);
                _initialized = true;
            }
        }
    }
    
    private void Update()
    {
        if (!_initialized || _cmc == null) return;
        
        var service = ModBehaviourF.Instance;
        if (service == null || !service.networkStarted || service.IsServer) return;
        
        var localPlayerManager = LocalPlayerManager.Instance;
        if (localPlayerManager == null) return;
        
        if (!localPlayerManager.ComputeIsInGame(out var playerSceneId))
        {
            SetAIVisible(false);
            return;
        }
        
        if (string.IsNullOrEmpty(_aiSceneId))
        {
            SetAIVisible(true);
            return;
        }
        
        bool shouldBeVisible = string.Equals(_aiSceneId, playerSceneId, System.StringComparison.Ordinal);
        SetAIVisible(shouldBeVisible);
    }
    
    private void SetAIVisible(bool visible)
    {
        if (_cmc == null) return;
        
        if (_cmc.gameObject.activeSelf != visible)
        {
            _cmc.gameObject.SetActive(visible);
        }
        
        var renderer = _cmc.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.enabled != visible)
        {
            renderer.enabled = visible;
        }
    }
    
    private string GetRootSceneId(CharacterSpawnerRoot r)
    {
        if (r == null) return string.Empty;
        
        try
        {
            var fi = typeof(CharacterSpawnerRoot).GetField("relatedScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var sceneIndex = (int)fi.GetValue(r);
                if (sceneIndex >= 0)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex(sceneIndex);
                    if (scene.IsValid())
                    {
                        return scene.name;
                    }
                }
            }
        }
        catch
        {
        }
        
        return string.Empty;
    }
}
