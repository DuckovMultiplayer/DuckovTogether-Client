















namespace EscapeFromDuckovCoopMod;

public static class NetPackProjectile
{
    public static void PutProjectilePayload(this NetDataWriter w, in ProjectileContext c)
    {
        w.Put(true); 
        
        w.Put(c.damage);
        w.Put(c.critRate);
        w.Put(c.critDamageFactor);
        w.Put(c.armorPiercing);
        w.Put(c.armorBreak);
        
        w.Put(c.element_Physics);
        w.Put(c.element_Fire);
        w.Put(c.element_Poison);
        w.Put(c.element_Electricity);
        w.Put(c.element_Space);
        
        w.Put(c.explosionRange);
        w.Put(c.explosionDamage);
        w.Put(c.buffChance);
        w.Put(c.bleedChance);
        
        w.Put(c.penetrate);
        w.Put(c.fromWeaponItemID);
    }

    
    public static bool TryGetProjectilePayload(NetDataReader r, ref ProjectileContext c)
    {
        if (r.AvailableBytes < 1) return false;
        if (!r.GetBool()) return false; 
        
        if (r.AvailableBytes < 64) return false;

        c.damage = r.GetFloat();
        c.critRate = r.GetFloat();
        c.critDamageFactor = r.GetFloat();
        c.armorPiercing = r.GetFloat();
        c.armorBreak = r.GetFloat();

        c.element_Physics = r.GetFloat();
        c.element_Fire = r.GetFloat();
        c.element_Poison = r.GetFloat();
        c.element_Electricity = r.GetFloat();
        c.element_Space = r.GetFloat();

        c.explosionRange = r.GetFloat();
        c.explosionDamage = r.GetFloat();
        c.buffChance = r.GetFloat();
        c.bleedChance = r.GetFloat();

        c.penetrate = r.GetInt();
        c.fromWeaponItemID = r.GetInt();
        return true;
    }
}