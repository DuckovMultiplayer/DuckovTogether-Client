















namespace EscapeFromDuckovCoopMod;

public class PublicHandleUpdate
{
    private ModBehaviourF Service => ModBehaviourF.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public void HandleEquipmentUpdate(NetPeer sender, NetDataReader reader)
    {
    }


    public void HandleWeaponUpdate(NetPeer sender, NetDataReader reader)
    {
    }

    
    public void HandleClientAnimationStatus(NetPeer sender, NetDataReader reader)
    {
        var moveSpeed = reader.GetFloat();
        var moveDirX = reader.GetFloat();
        var moveDirY = reader.GetFloat();
        var isDashing = reader.GetBool();
        var isAttacking = reader.GetBool();
        var handState = reader.GetInt();
        var gunReady = reader.GetBool();
        var stateHash = reader.GetInt();
        var normTime = reader.GetFloat();

        
        HandleRemoteAnimationStatus(sender, moveSpeed, moveDirX, moveDirY, isDashing, isAttacking, handState, gunReady, stateHash, normTime);

        var playerId = playerStatuses.TryGetValue(sender, out var st) && !string.IsNullOrEmpty(st.EndPoint)
            ? st.EndPoint
            : sender.EndPoint.ToString();

        foreach (var p in netManager.ConnectedPeerList)
        {
            if (p == sender) continue;
            var msg = new Net.HybridNet.PlayerAnimationMessage { PlayerId = playerId, MoveSpeed = moveSpeed, MoveDirX = moveDirX, MoveDirY = moveDirY, IsDashing = isDashing, IsAttacking = isAttacking, HandState = handState, GunReady = gunReady, StateHash = stateHash, NormTime = normTime };
            Net.HybridNet.HybridNetCore.Send(msg, p);
        }
    }

    
    private void HandleRemoteAnimationStatus(NetPeer peer, float moveSpeed, float moveDirX, float moveDirY,
        bool isDashing, bool isAttacking, int handState, bool gunReady,
        int stateHash, float normTime)
    {
        if (!remoteCharacters.TryGetValue(peer, out var remoteObj) || remoteObj == null) return;

        var ai = AnimInterpUtil.Attach(remoteObj);
        ai?.Push(new AnimSample
        {
            speed = moveSpeed,
            dirX = moveDirX,
            dirY = moveDirY,
            dashing = isDashing,
            attack = isAttacking,
            hand = handState,
            gunReady = gunReady,
            stateHash = stateHash,
            normTime = normTime
        });
    }

    public void HandlePositionUpdate(NetPeer sender, NetDataReader reader)
    {
        var endPoint = reader.GetString();
        var position = reader.GetV3cm(); 
        var dir = reader.GetDir();
        var rotation = Quaternion.LookRotation(dir, Vector3.up);

        foreach (var p in netManager.ConnectedPeerList)
        {
            if (p == sender) continue;
            var msg = new Net.HybridNet.PlayerPositionMessage { PlayerId = endPoint, PosX = position.x, PosY = position.y, PosZ = position.z, DirX = dir.x, DirY = dir.y, DirZ = dir.z };
            Net.HybridNet.HybridNetCore.Send(msg, p);
        }
    }


    public void HandlePositionUpdate_Q(NetPeer peer, string endPoint, Vector3 position, Quaternion rotation)
    {
        if (peer != null && playerStatuses.TryGetValue(peer, out var st))
        {
            st.Position = position;
            st.Rotation = rotation;

            if (remoteCharacters.TryGetValue(peer, out var go) && go != null)
            {
                var ni = NetInterpUtil.Attach(go);
                ni?.Push(position, rotation);
            }

            foreach (var p in netManager.ConnectedPeerList)
            {
                if (p == peer) continue;
                var fwd = rotation * Vector3.forward;
                var msg = new Net.HybridNet.PlayerPositionMessage { PlayerId = st.EndPoint ?? endPoint, PosX = position.x, PosY = position.y, PosZ = position.z, DirX = fwd.x, DirY = fwd.y, DirZ = fwd.z };
                Net.HybridNet.HybridNetCore.Send(msg, p);
            }
        }
    }
}