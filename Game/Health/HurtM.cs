















namespace EscapeFromDuckovCoopMod;

public class HurtM
{
    private static ModBehaviourF Service => ModBehaviourF.Instance;

    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;

    private static bool networkStarted => Service != null && Service.networkStarted;

    
    public void Server_HandleEnvHurtRequest(NetPeer sender, NetDataReader r)
    {
        var id = r.GetUInt();
        var payload = r.GetDamagePayload(); 

        var hs = COOPManager.destructible.FindDestructible(id);
        if (!hs) return;

        
        var info = new DamageInfo
        {
            damageValue = payload.dmg * ServerTuning.RemoteMeleeEnvScale, 
            armorPiercing = payload.ap,
            critDamageFactor = payload.cdf,
            critRate = payload.cr,
            crit = payload.crit,
            damagePoint = payload.point,
            damageNormal = payload.normal,
            fromWeaponItemID = payload.wid,
            bleedChance = payload.bleed,
            isExplosion = payload.boom,
            fromCharacter = null 
        };

        
        try
        {
            hs.dmgReceiver.Hurt(info);
        }
        catch
        {
        }
    }

    
    public void Client_RequestDestructibleHurt(uint id, DamageInfo dmg)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;

        var w = new NetDataWriter();
        w.Put((byte)41);
        w.Put(id);
        w.Put(dmg.damageValue);
        
        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
    }
}