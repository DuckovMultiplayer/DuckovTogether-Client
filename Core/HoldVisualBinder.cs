















namespace EscapeFromDuckovCoopMod;

internal static class HoldVisualBinder
{
    public static void EnsureHeldVisuals(CharacterMainControl cmc)
    {
        if (!cmc) return;
        var model = cmc.characterModel;
        if (!model) return;

        try
        {
            
            var melee =
                (model.MeleeWeaponSocket ? model.MeleeWeaponSocket.GetComponentInChildren<ItemAgent_MeleeWeapon>(true) : null)
                ?? model.GetComponentInChildren<ItemAgent_MeleeWeapon>(true);
            if (melee && melee.Holder == null) melee.SetHolder(cmc);

            
            var rGun =
                (model.RightHandSocket ? model.RightHandSocket.GetComponentInChildren<ItemAgent_Gun>(true) : null)
                ?? model.GetComponentInChildren<ItemAgent_Gun>(true);
            if (rGun && rGun.Holder == null) rGun.SetHolder(cmc);

            
            var lGun = model.LefthandSocket ? model.LefthandSocket.GetComponentInChildren<ItemAgent_Gun>(true) : null;
            if (lGun && lGun.Holder == null) lGun.SetHolder(cmc);
        }
        catch
        {
            
        }
    }
}