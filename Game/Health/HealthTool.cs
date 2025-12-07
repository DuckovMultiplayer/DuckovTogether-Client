















using System.Reflection;
using Duckov.UI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EscapeFromDuckovCoopMod;

public static class HealthTool
{
    public static bool _cliHookedSelf;
    public static UnityAction<Health> _cbSelfHpChanged, _cbSelfMaxChanged;
    public static UnityAction<DamageInfo> _cbSelfHurt, _cbSelfDead;
    public static float _cliNextSendHp = 0f;
    public static (float max, float cur) _cliLastSentHp = (0f, 0f);

    
    public static readonly Dictionary<Health, NetPeer> _srvHealthOwner = new();
    public static readonly HashSet<Health> _srvHooked = new();
    public static float _cliLastSelfHurtAt = -999f; 
    public static float _cliLastSelfHpLocal = -1f; 
    public static bool _cliInitHpReported = false;

    public static readonly Dictionary<NetPeer, (float max, float cur)> _srvPendingHp = new();


    
    public static readonly FieldInfo FI_defaultMax =
        typeof(Health).GetField("defaultMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    public static readonly FieldInfo FI_lastMax =
        typeof(Health).GetField("lastMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    public static readonly FieldInfo FI__current =
        typeof(Health).GetField("_currentHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    public static readonly FieldInfo FI_characterCached =
        typeof(Health).GetField("characterCached", BindingFlags.NonPublic | BindingFlags.Instance);

    public static readonly FieldInfo FI_hasCharacter =
        typeof(Health).GetField("hasCharacter", BindingFlags.NonPublic | BindingFlags.Instance);

    private static NetService Service => NetService.Instance;
    private static Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;


    
    public static void TryShowDamageBarUI(Health h, float damage)
    {
        if (h == null || damage <= 0f) return;

        try
        {
            
            var hbm = HealthBarManager.Instance;
            if (hbm == null) return;

            var miGet = AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
            var hb = miGet?.Invoke(hbm, new object[] { h });
            if (hb == null) return;

            
            var fiFill = AccessTools.Field(typeof(HealthBar), "fill");
            var fillImg = fiFill?.GetValue(hb) as Image;
            var width = 0f;
            if (fillImg != null)
                
                width = fillImg.rectTransform.rect.width;

            
            
            
            const float minPixels = 2f;
            const float minPercent = 0.0015f; 

            var maxHp = Mathf.Max(1f, h.MaxHealth);
            var minByPixels = width > 0f ? minPixels / width * maxHp : 0f;
            var minByPercent = minPercent * maxHp;
            var minDamageToShow = Mathf.Max(minByPixels, minByPercent);

            
            var visualDamage = Mathf.Max(damage, minDamageToShow);

            
            var miShow = AccessTools.DeclaredMethod(typeof(HealthBar), "ShowDamageBar", new[] { typeof(float) });
            miShow?.Invoke(hb, new object[] { visualDamage });
        }
        catch
        {
            
        }
    }

    public static NetPeer Server_FindOwnerPeerByHealth(Health h)
    {
        if (h == null) return null;
        CharacterMainControl cmc = null;
        try
        {
            cmc = h.TryGetCharacter();
        }
        catch
        {
        }

        if (!cmc)
            try
            {
                cmc = h.GetComponentInParent<CharacterMainControl>();
            }
            catch
            {
            }

        if (!cmc) return null;

        foreach (var kv in remoteCharacters) 
            if (kv.Value == cmc.gameObject)
                return kv.Key;
        return null;
    }


    public static void Server_HookOneHealth(NetPeer peer, GameObject instance)
    {
        if (!instance) return;

        var h = instance.GetComponentInChildren<Health>(true);
        var cmc = instance.GetComponent<CharacterMainControl>();
        if (!h) return;

        try
        {
            h.autoInit = false;
        }
        catch
        {
        }

        BindHealthToCharacter(h, cmc); 

        
        _srvHealthOwner[h] = peer; 
        if (!_srvHooked.Contains(h))
        {
            h.OnHealthChange.AddListener(_ => HealthM.Instance.Server_OnHealthChanged(peer, h));
            h.OnMaxHealthChange.AddListener(_ => HealthM.Instance.Server_OnHealthChanged(peer, h));
            _srvHooked.Add(h);
        }

        
        if (peer != null && _srvPendingHp.TryGetValue(peer, out var snap))
        {
            HealthM.Instance.ApplyHealthAndEnsureBar(instance, snap.max, snap.cur);
            _srvPendingHp.Remove(peer);
            HealthM.Instance.Server_OnHealthChanged(peer, h);
            return;
        }

        
        float max = 0f, cur = 0f;
        try
        {
            max = h.MaxHealth;
        }
        catch
        {
        }

        try
        {
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        if (max <= 0f)
        {
            max = 40f;
            if (cur <= 0f) cur = max;
        }

        HealthM.Instance.ApplyHealthAndEnsureBar(instance, max, cur); 
        HealthM.Instance.Server_OnHealthChanged(peer, h); 
    }


    public static void Client_HookSelfHealth()
    {
        if (_cliHookedSelf) return;
        var main = CharacterMainControl.Main;
        var h = main ? main.GetComponentInChildren<Health>(true) : null;
        if (!h) return;

        _cbSelfHpChanged = _ => HealthM.Instance.Client_SendSelfHealth(h, false);
        _cbSelfMaxChanged = _ => HealthM.Instance.Client_SendSelfHealth(h, true);
        _cbSelfHurt = di =>
        {
            _cliLastSelfHurtAt = Time.time; 
            try
            {
                _cliLastSelfHpLocal = h.CurrentHealth;
            }
            catch
            {
            }

            HealthM.Instance.Client_SendSelfHealth(h, true); 
        };
        _cbSelfDead = _ => HealthM.Instance.Client_SendSelfHealth(h, true);

        h.OnHealthChange.AddListener(_cbSelfHpChanged);
        h.OnMaxHealthChange.AddListener(_cbSelfMaxChanged);
        h.OnHurtEvent.AddListener(_cbSelfHurt);
        h.OnDeadEvent.AddListener(_cbSelfDead);

        _cliHookedSelf = true;

        
        HealthM.Instance.Client_SendSelfHealth(h, true);
    }

    public static void Client_UnhookSelfHealth()
    {
        if (!_cliHookedSelf) return;
        var main = CharacterMainControl.Main;
        var h = main ? main.GetComponentInChildren<Health>(true) : null;
        if (h)
        {
            if (_cbSelfHpChanged != null) h.OnHealthChange.RemoveListener(_cbSelfHpChanged);
            if (_cbSelfMaxChanged != null) h.OnMaxHealthChange.RemoveListener(_cbSelfMaxChanged);
            if (_cbSelfHurt != null) h.OnHurtEvent.RemoveListener(_cbSelfHurt);
            if (_cbSelfDead != null) h.OnDeadEvent.RemoveListener(_cbSelfDead);
        }

        _cliHookedSelf = false;
        _cbSelfHpChanged = _cbSelfMaxChanged = null;
        _cbSelfHurt = _cbSelfDead = null;
    }

    
    public static void BindHealthToCharacter(Health h, CharacterMainControl cmc)
    {
        try
        {
            FI_characterCached?.SetValue(h, cmc);
            FI_hasCharacter?.SetValue(h, true);
        }
        catch
        {
        }
    }
}