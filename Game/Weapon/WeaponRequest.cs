















namespace EscapeFromDuckovCoopMod;

public class WeaponRequest
{
    private ModBehaviourF Service => ModBehaviourF.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;

    public void BroadcastMeleeSwing(string playerId, float dealDelay)
    {
        foreach (var p in netManager.ConnectedPeerList)
        {
            var msg = new Net.HybridNet.MeleeAttackSwingMessage { PlayerId = playerId, AnimDelay = dealDelay };
            Net.HybridNet.HybridNetCore.Send(msg, p);
        }
    }

    
    public void Net_OnClientShoot(ItemAgent_Gun gun, Vector3 muzzle, Vector3 baseDir, Vector3 firstCheckStart)
    {
        if (IsServer || connectedPeer == null || localPlayerStatus == null || writer == null || gun == null || gun.Item == null) return;

        if (baseDir.sqrMagnitude < 1e-8f)
        {
            var fallback = gun != null && gun.muzzle != null ? gun.muzzle.forward : Vector3.forward;
            baseDir = fallback.sqrMagnitude < 1e-8f ? Vector3.forward : fallback.normalized;
        }

        if (gun && gun.muzzle)
        {
            var weaponType = gun.Item != null ? gun.Item.TypeID : 0;
            FxManager.Client_PlayLocalShotFx(gun, gun.muzzle, weaponType);
        }

        writer.Reset();
        var msg = new Net.HybridNet.FireRequestMessage
        {
            ShooterId = localPlayerStatus.EndPoint,
            WeaponType = gun.Item.TypeID,
            Muzzle = muzzle,
            BaseDir = baseDir,
            FirstCheckStart = firstCheckStart,
        };

        
        var clientScatter = 0f;
        var ads01 = 0f;
        try
        {
            clientScatter = Mathf.Max(0f, gun.CurrentScatter); 
            ads01 = gun.IsInAds ? 1f : 0f;
        }
        catch
        {
        }

        msg.ClientScatter = new Vector2(clientScatter, clientScatter);
        msg.Ads01 = ads01 > 0.5f;

        
        var hint = new ProjectileContext();
        try
        {
            var hasBulletItem = gun.BulletItem != null;

            
            var charMul = gun.CharacterDamageMultiplier;
            var bulletMul = hasBulletItem ? Mathf.Max(0.0001f, gun.BulletDamageMultiplier) : 1f;
            var shots = Mathf.Max(1, gun.ShotCount);
            hint.damage = gun.Damage * bulletMul * charMul / shots;
            if (gun.Damage > 1f && hint.damage < 1f) hint.damage = 1f;

            
            var bulletCritRateGain = hasBulletItem ? gun.bulletCritRateGain : 0f;
            var bulletCritDmgGain = hasBulletItem ? gun.BulletCritDamageFactorGain : 0f;
            hint.critDamageFactor = (gun.CritDamageFactor + bulletCritDmgGain) * (1f + gun.CharacterGunCritDamageGain);
            hint.critRate = gun.CritRate * (1f + gun.CharacterGunCritRateGain + bulletCritRateGain);

            
            switch (gun.GunItemSetting.element)
            {
                case ElementTypes.physics: hint.element_Physics = 1f; break;
                case ElementTypes.fire: hint.element_Fire = 1f; break;
                case ElementTypes.poison: hint.element_Poison = 1f; break;
                case ElementTypes.electricity: hint.element_Electricity = 1f; break;
                case ElementTypes.space: hint.element_Space = 1f; break;
            }

            hint.armorPiercing = gun.ArmorPiercing + (hasBulletItem ? gun.BulletArmorPiercingGain : 0f);
            hint.armorBreak = gun.ArmorBreak + (hasBulletItem ? gun.BulletArmorBreakGain : 0f);
            hint.explosionRange = gun.BulletExplosionRange;
            hint.explosionDamage = gun.BulletExplosionDamage * gun.ExplosionDamageMultiplier;
            if (hasBulletItem)
            {
                hint.buffChance = gun.BulletBuffChanceMultiplier * gun.BuffChance;
                hint.bleedChance = gun.BulletBleedChance;
            }

            hint.penetrate = gun.Penetrate;
            hint.fromWeaponItemID = gun.Item != null ? gun.Item.TypeID : 0;
        }
        catch
        {
            
        }

        msg.ProjectilePayload = new byte[0];
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }

    
    public void Net_OnClientMeleeAttack(float dealDelay, Vector3 snapPos, Vector3 snapDir)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;
        var msg = new Net.HybridNet.MeleeAttackRequestMessage
        {
            AnimDelay = dealDelay,
            SnapPos = snapPos,
            SnapDir = snapDir,
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }
}