















namespace EscapeFromDuckovCoopMod;

public sealed class NetAiVisibilityGuard : MonoBehaviour
{
    private bool _inited;
    private Light[] _lights;
    private ParticleSystem[] _particles;
    private Renderer[] _renderers;

    private void EnsureCache()
    {
        if (_inited) return;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _lights = GetComponentsInChildren<Light>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _inited = true;
    }

    public void SetVisible(bool v)
    {
        EnsureCache();
        if (_renderers != null)
            foreach (var r in _renderers)
                if (r)
                    r.enabled = v;
        if (_lights != null)
            foreach (var l in _lights)
                if (l)
                    l.enabled = v;
        if (_particles != null)
            foreach (var ps in _particles)
            {
                if (!ps) continue;
                var em = ps.emission;
                em.enabled = v;
            }
        
    }
}