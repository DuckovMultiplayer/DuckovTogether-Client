using Duckov.UI;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod.Utils;

public sealed class HealthBarColorizer : MonoBehaviour
{
    private static readonly MethodInfo MI_GetActiveHealthBar = 
        AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
    
    private static readonly FieldInfo FI_Fill = 
        AccessTools.Field(typeof(HealthBar), "fill");
    
    private static readonly FieldInfo FI_NameText = 
        AccessTools.Field(typeof(HealthBar), "nameText");

    private Health _health;
    private string _playerId;
    private string _steamName;
    private Color _assignedColor;
    private bool _isLocal;
    private int _retryAttempts = 30;

    public void Initialize(string playerId, string steamName, bool isLocal)
    {
        _playerId = playerId;
        _steamName = steamName;
        _isLocal = isLocal;
        _health = GetComponentInChildren<Health>(true);

        if (PlayerColorManager.Instance != null)
        {
            _assignedColor = PlayerColorManager.Instance.GetOrAssignColor(playerId, isLocal);
        }
    }

    private void OnEnable()
    {
        if (_health != null && !string.IsNullOrEmpty(_playerId))
        {
            StartCoroutine(ApplyColorization());
        }
    }

    private IEnumerator ApplyColorization()
    {
        yield return null;
        yield return null;

        for (int i = 0; i < _retryAttempts; i++)
        {
            if (!_health) yield break;

            try
            {
                var healthBar = GetHealthBar(_health);
                if (healthBar != null)
                {
                    ApplyColor(healthBar, _assignedColor);
                    ApplySteamName(healthBar, _steamName);
                    yield break;
                }
            }
            catch
            {
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private HealthBar GetHealthBar(Health health)
    {
        try
        {
            var manager = HealthBarManager.Instance;
            if (manager == null) return null;

            return MI_GetActiveHealthBar?.Invoke(manager, new object[] { health }) as HealthBar;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyColor(HealthBar healthBar, Color color)
    {
        try
        {
            var fillImage = FI_Fill?.GetValue(healthBar) as Image;
            if (fillImage != null)
            {
                fillImage.color = color;
            }
        }
        catch
        {
        }
    }

    private void ApplySteamName(HealthBar healthBar, string steamName)
    {
        if (string.IsNullOrEmpty(steamName)) return;

        try
        {
            var nameText = FI_NameText?.GetValue(healthBar) as TextMeshProUGUI;
            if (nameText != null)
            {
                nameText.text = steamName;
                nameText.gameObject.SetActive(true);
            }
        }
        catch
        {
        }
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(_playerId) && PlayerColorManager.Instance != null && !_isLocal)
        {
            PlayerColorManager.Instance.ReleaseColor(_playerId);
        }
    }
}
