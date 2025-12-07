namespace EscapeFromDuckovCoopMod;

public class VoteData
{
    public PlayerList playerList;
}

public class PlayerList
{
    public List<PlayerInfo> items = new();
}

public class PlayerInfo
{
    public string playerId;
    public string steamName;
    public string steamId;
}
