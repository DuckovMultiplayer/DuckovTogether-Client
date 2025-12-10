















using EscapeFromDuckovCoopMod.Net;  

namespace EscapeFromDuckovCoopMod;

public class SendLocalPlayerStatus : MonoBehaviour
{
    public static SendLocalPlayerStatus Instance;

    private ModBehaviourF Service => ModBehaviourF.Instance;
    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;

    public void Init()
    {
        Instance = this;
    }

    public void SendPlayerStatusUpdate()
    {
        if (!DedicatedServerMode.ShouldBroadcastState()) return;

        var statuses = new List<PlayerStatus> { localPlayerStatus };
        foreach (var kvp in playerStatuses) statuses.Add(kvp.Value);

        
        var statusMsg = new Net.HybridNet.PlayerStatusUpdateMessage
        {
            PlayerId = localPlayerStatus.EndPoint
        };
        Net.HybridNet.HybridNetCore.Send(statusMsg);
    }


    public void SendPositionUpdate()
    {
        if (localPlayerStatus == null || !networkStarted) return;

        var main = CharacterMainControl.Main;
        if (!main) return;

        var tr = main.transform;
        var mr = main.modelRoot ? main.modelRoot.transform : null;

        var pos = tr.position;
        var fwd = mr ? mr.forward : tr.forward;
        if (fwd.sqrMagnitude < 1e-12f) fwd = Vector3.forward;


        
        var posMsg = new Net.HybridNet.PlayerPositionMessage
        {
            PlayerId = localPlayerStatus.EndPoint,
            PosX = pos.x,
            PosY = pos.y,
            PosZ = pos.z,
            DirX = fwd.x,
            DirY = fwd.y,
            DirZ = fwd.z
        };
        Net.HybridNet.HybridNetCore.Send(posMsg);
    }

    public void SendEquipmentUpdate(EquipmentSyncData equipmentData)
    {
        if (localPlayerStatus == null || !networkStarted) return;

        
        var equipMsg = new Net.HybridNet.PlayerEquipmentUpdateMessage
        {
            PlayerId = localPlayerStatus.EndPoint,
            SlotHash = equipmentData.SlotHash,
            ItemId = equipmentData.ItemId ?? ""
        };
        Net.HybridNet.HybridNetCore.Send(equipMsg);
    }


    public void SendWeaponUpdate(WeaponSyncData weaponSyncData)
    {
        if (localPlayerStatus == null || !networkStarted) return;

        
        var weaponMsg = new Net.HybridNet.PlayerWeaponUpdateMessage
        {
            PlayerId = localPlayerStatus.EndPoint,
            SlotHash = weaponSyncData.SlotHash,
            ItemId = weaponSyncData.ItemId ?? ""
        };
        Net.HybridNet.HybridNetCore.Send(weaponMsg);
    }

    public void SendAnimationStatus()
    {
        if (!networkStarted) return;

        var mainControl = CharacterMainControl.Main;
        if (mainControl == null || mainControl.modelRoot == null) return;

        var model = mainControl.modelRoot.Find("0_CharacterModel_Custom_Template(Clone)");
        if (model == null) return;

        var animCtrl = model.GetComponent<CharacterAnimationControl_MagicBlend>();
        if (animCtrl == null || animCtrl.animator == null) return;

        var anim = animCtrl.animator;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        var stateHash = state.shortNameHash;
        var normTime = state.normalizedTime;

        
        var animMsg = new Net.HybridNet.PlayerAnimationMessage
        {
            PlayerId = localPlayerStatus.EndPoint,
            MoveSpeed = anim.GetFloat("MoveSpeed"),
            MoveDirX = anim.GetFloat("MoveDirX"),
            MoveDirY = anim.GetFloat("MoveDirY"),
            IsDashing = anim.GetBool("Dashing"),
            IsAttacking = anim.GetBool("Attack"),
            HandState = anim.GetInteger("HandState"),
            GunReady = anim.GetBool("GunReady"),
            StateHash = stateHash,
            NormTime = normTime
        };
        Net.HybridNet.HybridNetCore.Send(animMsg);
    }


    public void Net_ReportPlayerDeadTree(CharacterMainControl who)
    {
        
        if (!networkStarted || IsServer || connectedPeer == null || who == null) return;

        var item = who.CharacterItem; 
        if (item == null) return;

        
        var pos = who.transform.position;
        var rot = who.characterModel ? who.characterModel.transform.rotation : who.transform.rotation;

        
        var deadMsg = new Net.HybridNet.PlayerDeadTreeMessage
        {
            PlayerId = localPlayerStatus.EndPoint,
            Position = pos,
            Rotation = rot
        };
        Net.HybridNet.HybridNetCore.Send(deadMsg, connectedPeer);
    }
}