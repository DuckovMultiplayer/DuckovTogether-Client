















using Duckov.UI;

namespace EscapeFromDuckovCoopMod;


public sealed class HostForceHealthBar : MonoBehaviour
{
    private float _deadline;
    private Health _h;
    private int _tries;

    private void Update()
    {
        if (!_h || Time.time > _deadline)
        {
            enabled = false;
            return;
        }

        
        try
        {
            _h.showHealthBar = true;
        }
        catch
        {
        }

        try
        {
            _h.RequestHealthBar();
        }
        catch
        {
        }

        
        try
        {
            var miGet = AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
            var hb = miGet?.Invoke(HealthBarManager.Instance, new object[] { _h }) as HealthBar;
            if (hb != null)
            {
                enabled = false;
                return;
            }
        }
        catch
        {
        }

        _tries++;
    }

    private void OnEnable()
    {
        
        var m = ModBehaviourF.Instance;
        if (m == null || !m.networkStarted || !m.IsServer)
        {
            enabled = false;
            return;
        }

        _h = GetComponentInChildren<Health>(true);
        _deadline = Time.time + 5f; 
        _tries = 0;
    }
}