using Duckov.Utilities;

namespace EscapeFromDuckovCoopMod;

[HarmonyPatch(typeof(LootSpawner), "Start")]
internal static class Patch_LootSpawner_Start_PrimeNeedInspect
{
    private static void Postfix(LootSpawner __instance)
    {
        var lb = __instance.GetComponent<InteractableLootbox>();
        WorldLootPrime.PrimeIfClient(lb);
    }
}

[HarmonyPatch(typeof(LootSpawner), "Setup")]
internal static class Patch_LootSpawner_Setup_PrimeNeedInspect
{
    private static void Postfix(LootSpawner __instance)
    {
        var lb = __instance.GetComponent<InteractableLootbox>();
        WorldLootPrime.PrimeIfClient(lb);
    }
}

[HarmonyPatch(typeof(LootBoxLoader), "Awake")]
internal static class Patch_LootBoxLoader_Awake_PrimeNeedInspect
{
    private static void Postfix(LootBoxLoader __instance)
    {
        var lb = __instance.GetComponent<InteractableLootbox>();
        WorldLootPrime.PrimeIfClient(lb);
    }
}