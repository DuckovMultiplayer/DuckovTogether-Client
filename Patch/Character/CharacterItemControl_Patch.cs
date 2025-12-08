namespace EscapeFromDuckovCoopMod;

//给 CharacterItemControl.PickupItem 打前后钩，围出一个方法域
[HarmonyPatch(typeof(CharacterItemControl), nameof(CharacterItemControl.PickupItem))]
internal static class Patch_CharacterItemControl_PickupItem
{
    private static void Prefix()
    {
        NetSilenceGuards.InPickupItem = true;
    }

    private static void Finalizer()
    {
        NetSilenceGuards.InPickupItem = false;
    }
}

[HarmonyPatch(typeof(CharacterAnimationControl_MagicBlend), "Update")]
internal static class Patch_MagicBlend_Update_ForRemote
{
    private static bool Prefix(CharacterAnimationControl_MagicBlend __instance)
    {
        // 远端实体：禁用本地“写Animator参数”的逻辑，避免覆盖网络同步
        if (__instance && __instance.GetComponentInParent<RemoteReplicaTag>() != null)
            return false;
        return true;
    }
}