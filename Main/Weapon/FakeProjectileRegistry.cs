















using System;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod;

public static class FakeProjectileRegistry
{
    private static readonly HashSet<Projectile> _fakes = new();

    [ThreadStatic]
    private static Projectile _current;

    public static void Register(Projectile proj)
    {
        if (proj == null) return;
        _fakes.Add(proj);
    }

    public static void Unregister(Projectile proj)
    {
        if (proj == null) return;
        _fakes.Remove(proj);
        if (_current == proj) _current = null;
    }

    public static bool IsFake(Projectile proj)
    {
        return proj != null && _fakes.Contains(proj);
    }

    public static void BeginFrame(Projectile proj)
    {
        _current = IsFake(proj) ? proj : null;
    }

    public static void EndFrame(Projectile proj)
    {
        if (_current == proj)
            _current = null;
    }

    public static bool IsCurrentFake => _current != null && IsFake(_current);
}
