using Duckov.Buffs;
using Duckov.Utilities;
using HarmonyLib;

namespace EscapeFromDuckovCoopMod;

[HarmonyPatch(typeof(Projectile), "UpdateMoveAndCheck")]
internal static class Patch_Projectile_UpdateMoveAndCheck_Fake
{
    private static void Prefix(Projectile __instance)
    {
        FakeProjectileRegistry.BeginFrame(__instance);
    }

    private static void Postfix(Projectile __instance)
    {
        FakeProjectileRegistry.EndFrame(__instance);
    }
}

[HarmonyPatch(typeof(Projectile), "Release")]
internal static class Patch_Projectile_Release_Fake
{
    private static void Postfix(Projectile __instance)
    {
        FakeProjectileRegistry.Unregister(__instance);
    }
}

[HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.AddBuff), typeof(Buff), typeof(CharacterMainControl))]
internal static class Patch_DamageReceiver_AddBuff_Fake
{
    private static bool Prefix(ref bool __result, Buff buffPfb)
    {
        if (buffPfb == GameplayDataSettings.Buffs.Pain && FakeProjectileRegistry.IsCurrentFake)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
