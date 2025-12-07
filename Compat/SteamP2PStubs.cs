using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;

namespace EscapeFromDuckovCoopMod;

public class SteamLobbyOptions
{
    public string LobbyName { get; set; } = "";
    public string Password { get; set; } = "";
    public SteamLobbyVisibility Visibility { get; set; } = SteamLobbyVisibility.Public;
    public int MaxPlayers { get; set; } = 4;
    
    public static SteamLobbyOptions CreateDefault() => new SteamLobbyOptions();
}

public enum SteamLobbyVisibility
{
    Public,
    FriendsOnly,
    Private
}

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }
    public bool IsInLobby => false;
    public bool IsHost => false;
    public ulong CurrentLobbyId => 0;
    public List<LobbyInfo> AvailableLobbies { get; } = new();
    
    public event Action<IReadOnlyList<LobbyInfo>> LobbyListUpdated;
    public event Action LobbyJoined;
    
    public class LobbyInfo
    {
        public ulong LobbyId { get; set; }
        public string LobbyName { get; set; } = "";
        public string HostName { get; set; } = "";
        public int MemberCount { get; set; }
        public int MaxMembers { get; set; }
        public bool RequiresPassword { get; set; }
        public LobbyInfo Value => this;
    }
    
    public enum LobbyJoinError
    {
        None,
        SteamNotInitialized,
        LobbyMetadataUnavailable,
        IncorrectPassword
    }
    
    void Awake() { Instance = this; }
    
    public void RequestLobbyList() { LobbyListUpdated?.Invoke(AvailableLobbies); }
    public void LeaveLobby() { }
    public void UpdateLobbySettings(SteamLobbyOptions options) { }
    public void CreateLobby(SteamLobbyOptions options) { }
    public string GetCachedMemberName(Steamworks.CSteamID id) => "Player";
    public string GetCachedMemberName(ulong id) => "Player";
    public bool TryGetLobbyInfo(ulong id, out LobbyInfo info) { info = null; return false; }
    public bool TryJoinLobbyWithPassword(ulong lobbyId, string password, out LobbyJoinError error) { error = LobbyJoinError.SteamNotInitialized; return false; }
    public bool TryJoinLobbyWithPassword(Steamworks.CSteamID lobbyId, string password, out LobbyJoinError error) { error = LobbyJoinError.SteamNotInitialized; return false; }
    public void JoinLobby(ulong lobbyId) { }
    public void JoinLobby(Steamworks.CSteamID lobbyId) { }
}

public class SteamP2PLoader : MonoBehaviour
{
    public static SteamP2PLoader Instance { get; private set; }
    public bool UseSteamP2P { get; set; } = false;
    public bool _isOptimized { get; set; } = true;
    
    void Awake() { Instance = this; }
    public void Init() { Debug.Log("[SteamP2P] Disabled - Direct connection only"); }
}

public class SteamP2PManager : MonoBehaviour
{
    public static SteamP2PManager Instance { get; private set; }
    void Awake() { Instance = this; }
    public void ClearAcceptedSession(Steamworks.CSteamID steamId) { }
}

public class SteamEndPointMapper : MonoBehaviour
{
    public static SteamEndPointMapper Instance { get; private set; }
    void Awake() { Instance = this; }
    
    public bool TryGetSteamID(IPEndPoint endPoint, out Steamworks.CSteamID steamId) { steamId = default; return false; }
    public bool TryGetSteamID(NetPeer peer, out Steamworks.CSteamID steamId) { steamId = default; return false; }
    public bool TryGetEndPoint(Steamworks.CSteamID steamId, out IPEndPoint endPoint) { endPoint = null; return false; }
    public void RegisterMapping(Steamworks.CSteamID steamId, IPEndPoint endPoint) { }
    public void RemoveMapping(Steamworks.CSteamID steamId) { }
    public void RemoveMapping(IPEndPoint endPoint) { }
    public void UnregisterSteamID(Steamworks.CSteamID steamId) { }
}
