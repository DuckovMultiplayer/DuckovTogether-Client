















namespace EscapeFromDuckovCoopMod;

public class WeaponTool
{
    private void TryStartVisualRecoil(ItemAgent_Gun gun)
    {
        if (!gun) return;
        try
        {
            Traverse.Create(gun).Method("StartVisualRecoil").GetValue();
            return;
        }
        catch
        {
        }

        try
        {
            
            Traverse.Create(gun).Field<bool>("_recoilBack").Value = true;
        }
        catch
        {
        }
    }
}