















namespace EscapeFromDuckovCoopMod;

public class ClientHandle
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

    
    public void HandlePlayerPositionFromMessage(NetPeer peer, Net.HybridNet.PlayerPositionMessage msg)
    {
        
    }
    
    public void HandlePlayerAnimationFromMessage(NetPeer peer, Net.HybridNet.PlayerAnimationMessage msg)
    {
        
    }
    
    public void HandlePlayerEquipmentFromMessage(NetPeer peer, Net.HybridNet.PlayerEquipmentMessage msg)
    {
        
    }
    
    public void HandlePlayerHealthFromMessage(NetPeer peer, Net.HybridNet.PlayerStatusUpdateMessage msg)
    {
        
    }
    
    public void HandlePlayerInventoryFromMessage(NetPeer peer, Net.HybridNet.PlayerInventoryMessage msg)
    {
        
    }

    public void HandleClientStatusUpdate(NetPeer peer, NetDataReader reader)
    {
        var endPoint = reader.GetString();  
        var playerName = reader.GetString();
        var isInGame = reader.GetBool();
        var position = reader.GetVector3();
        var rotation = reader.GetQuaternion();
        var sceneId = reader.GetString();
        

        var equipmentCount = reader.GetInt();
        var equipmentList = new List<EquipmentSyncData>();
        for (var i = 0; i < equipmentCount; i++)
            equipmentList.Add(EquipmentSyncData.Deserialize(reader));

        var weaponCount = reader.GetInt();
        var weaponList = new List<WeaponSyncData>();
        for (var i = 0; i < weaponCount; i++)
            weaponList.Add(WeaponSyncData.Deserialize(reader));

        if (!playerStatuses.ContainsKey(peer))
            playerStatuses[peer] = new PlayerStatus();

        var st = playerStatuses[peer];
        
        
        if (string.IsNullOrEmpty(st.EndPoint))
            st.EndPoint = peer.EndPoint.ToString();  

        st.ClientReportedId = endPoint;
        st.PlayerName = playerName;
        st.Latency = peer.Ping;
        st.IsInGame = isInGame;
        st.LastIsInGame = isInGame;
        st.Position = position;
        st.Rotation = rotation;
        
        st.EquipmentList = equipmentList;
        st.WeaponList = weaponList;
        st.SceneId = sceneId;

        if (isInGame && !remoteCharacters.ContainsKey(peer))
        {
            // Create a new character
            var faceJson = st.CustomFaceJson ?? string.Empty;
            CreateRemoteCharacter.CreateRemoteCharacterAsync(peer, position, rotation, faceJson).Forget();
            var playerId = peer.EndPoint.ToString();
            foreach (var e in equipmentList) COOPManager.ClientPlayer_Apply.ApplyEquipmentUpdate_Client(playerId, e.SlotHash, e.ItemId).Forget();
            foreach (var w in weaponList) COOPManager.ClientPlayer_Apply.ApplyWeaponUpdate_Client(playerId, w.SlotHash, w.ItemId).Forget();
        }
        else if (isInGame)
        {
            if (remoteCharacters.TryGetValue(peer, out var go) && go != null)
            {
                go.transform.position = position;
                go.GetComponentInChildren<CharacterMainControl>().modelRoot.transform.rotation = rotation;
            }

            var playerId = peer.EndPoint.ToString();
            foreach (var e in equipmentList) COOPManager.ClientPlayer_Apply.ApplyEquipmentUpdate_Client(playerId, e.SlotHash, e.ItemId).Forget();
            foreach (var w in weaponList) COOPManager.ClientPlayer_Apply.ApplyWeaponUpdate_Client(playerId, w.SlotHash, w.ItemId).Forget();
        }

        playerStatuses[peer] = st;

        SendLocalPlayerStatus.Instance.SendPlayerStatusUpdate();
    }
}