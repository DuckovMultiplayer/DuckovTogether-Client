















using System.Reflection;
using Duckov.Utilities;
using FX;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public static class LocalHitKillFx
{
    private static FieldInfo _fiHurtVisual; 
    private static MethodInfo _miHvOnHurt, _miHvOnDead; 
    private static MethodInfo _miHmOnHit, _miHmOnKill; 

    
    private static Action<DamageInfo> _cachedHvOnHurt;
    private static Action<DamageInfo> _cachedHvOnDead;
    private static Action<DamageInfo> _cachedHmOnHit;
    private static Action<DamageInfo> _cachedHmOnKill;

    
    private static object _cachedHitMarker;
    private static bool _hitMarkerSearched;

    private static float _lastBaseDamageForPop;

    private static void EnsureHurtVisualBindings(object characterModel, object hv)
    {
        if (_fiHurtVisual == null && characterModel != null)
            _fiHurtVisual = characterModel.GetType()
                .GetField("hurtVisual", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (hv != null)
        {
            var t = hv.GetType();
            if (_miHvOnHurt == null)
                _miHvOnHurt = t.GetMethod("OnHurt", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (_miHvOnDead == null)
                _miHvOnDead = t.GetMethod("OnDead", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            
            if (_cachedHvOnHurt == null && _miHvOnHurt != null)
            {
                try
                {
                    _cachedHvOnHurt = (Action<DamageInfo>)Delegate.CreateDelegate(typeof(Action<DamageInfo>), hv, _miHvOnHurt);
                }
                catch
                {
                    
                }
            }

            if (_cachedHvOnDead == null && _miHvOnDead != null)
            {
                try
                {
                    _cachedHvOnDead = (Action<DamageInfo>)Delegate.CreateDelegate(typeof(Action<DamageInfo>), hv, _miHvOnDead);
                }
                catch
                {
                    
                }
            }
        }
    }

    public static void RememberLastBaseDamage(float v)
    {
        if (v > 0.01f) _lastBaseDamageForPop = v;
    }

    private static object FindHurtVisualOn(CharacterMainControl cmc)
    {
        if (!cmc) return null;
        var model = cmc.characterModel; 
        if (model == null) return null;

        object hv = null;
        try
        {
            EnsureHurtVisualBindings(model, null);
            if (_fiHurtVisual != null)
                hv = _fiHurtVisual.GetValue(model);
        }
        catch
        {
        }

        
        if (hv == null)
            try
            {
                hv = model.GetComponentInChildren(typeof(HurtVisual), true);
            }
            catch
            {
            }

        return hv;
    }

    private static object FindHitMarkerSingleton()
    {
        
        if (!_hitMarkerSearched)
        {
            _hitMarkerSearched = true;
            try
            {
                _cachedHitMarker = Object.FindObjectOfType(typeof(HitMarker), true);
            }
            catch
            {
                _cachedHitMarker = null;
            }
        }

        return _cachedHitMarker;
    }

    private static void PlayHurtVisual(object hv, DamageInfo di, bool predictedDead)
    {
        if (hv == null) return;
        EnsureHurtVisualBindings(null, hv);

        
        try
        {
            if (_cachedHvOnHurt != null)
                _cachedHvOnHurt(di);
            else
                _miHvOnHurt?.Invoke(hv, new object[] { di });
        }
        catch
        {
        }

        if (predictedDead)
            try
            {
                if (_cachedHvOnDead != null)
                    _cachedHvOnDead(di);
                else
                    _miHvOnDead?.Invoke(hv, new object[] { di });
            }
            catch
            {
            }
    }

    public static void PopDamageText(Vector3 hintPos, DamageInfo di)
    {
        try
        {
            if (PopText.instance)
            {
                var look = GameplayDataSettings.UIStyle.GetElementDamagePopTextLook(ElementTypes.physics);
                var size = di.crit > 0 ? look.critSize : look.normalSize;
                var sprite = di.crit > 0 ? GameplayDataSettings.UIStyle.CritPopSprite : null;
                
                
                var _display = di.damageValue;
                
                if (_display <= 1.001f && _lastBaseDamageForPop > 0f)
                {
                    var critMul = di.crit > 0 && di.critDamageFactor > 0f ? di.critDamageFactor : 1f;
                    _display = Mathf.Max(_display, _lastBaseDamageForPop * critMul);
                }

                var text = _display > 0f ? _display.ToString("F1") : "HIT";
                PopText.Pop(text, hintPos, look.color, size, sprite);
            }
        }
        catch
        {
        }
    }

    
    private static void PlayUiHitKill(DamageInfo di, bool predictedDead, bool forceLocalMain)
    {
        var hm = FindHitMarkerSingleton();
        if (hm == null) return;

        if (_miHmOnHit == null)
            _miHmOnHit = hm.GetType().GetMethod("OnHit", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_miHmOnKill == null)
            _miHmOnKill = hm.GetType().GetMethod("OnKill", BindingFlags.Instance | BindingFlags.NonPublic);

        
        if (_cachedHmOnHit == null && _miHmOnHit != null)
        {
            try
            {
                _cachedHmOnHit = (Action<DamageInfo>)Delegate.CreateDelegate(typeof(Action<DamageInfo>), hm, _miHmOnHit);
            }
            catch
            {
                
            }
        }

        if (_cachedHmOnKill == null && _miHmOnKill != null)
        {
            try
            {
                _cachedHmOnKill = (Action<DamageInfo>)Delegate.CreateDelegate(typeof(Action<DamageInfo>), hm, _miHmOnKill);
            }
            catch
            {
                
            }
        }

        if (forceLocalMain)
            try
            {
                if (di.fromCharacter == null || di.fromCharacter != CharacterMainControl.Main)
                    di.fromCharacter = CharacterMainControl.Main;
            }
            catch
            {
            }

        
        try
        {
            if (_cachedHmOnHit != null)
                _cachedHmOnHit(di);
            else
                _miHmOnHit?.Invoke(hm, new object[] { di });
        }
        catch
        {
        }

        if (predictedDead)
            try
            {
                if (_cachedHmOnKill != null)
                    _cachedHmOnKill(di);
                else
                    _miHmOnKill?.Invoke(hm, new object[] { di });
            }
            catch
            {
            }
    }

    
    
    
    public static void ClientPlayForAI(CharacterMainControl victim, DamageInfo di, bool predictedDead)
    {
        
        var hv = FindHurtVisualOn(victim);
        PlayHurtVisual(hv, di, predictedDead);

        
        PlayUiHitKill(di, predictedDead, true);

        
        var pos = (di.damagePoint.sqrMagnitude > 1e-6f ? di.damagePoint : victim.transform.position) + Vector3.up * 2f;
        PopDamageText(pos, di);
    }

    
    
    
    public static void ClientPlayForDestructible(HealthSimpleBase hs, DamageInfo di, bool predictedDead)
    {
        
        PlayUiHitKill(di, predictedDead, true);

        
        var basePos = hs ? hs.transform.position : Vector3.zero;
        var pos = (di.damagePoint.sqrMagnitude > 1e-6f ? di.damagePoint : basePos) + Vector3.up * 2f;
        PopDamageText(pos, di);
    }
}