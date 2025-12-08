















namespace EscapeFromDuckovCoopMod;

public class Buff_
{
    private ModBehaviourF Service => ModBehaviourF.Instance;


    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    public void HandlePlayerBuffSelfApply(NetDataReader r)
    {
        var weaponTypeId = r.GetInt(); 
        var buffId = r.GetInt(); 
        ApplyBuffToSelf_Client(weaponTypeId, buffId).Forget();
    }

    public void HandleBuffProxyApply(NetDataReader r)
    {
        var hostId = r.GetString(); 
        var weaponTypeId = r.GetInt();
        var buffId = r.GetInt();
        ApplyBuffProxy_Client(hostId, weaponTypeId, buffId).Forget();
    }

    public async UniTask ApplyBuffToSelf_Client(int weaponTypeId, int buffId)
    {
        var me = LevelManager.Instance ? LevelManager.Instance.MainCharacter : null;
        if (!me) return;

        var buff = await COOPManager.ResolveBuffAsync(weaponTypeId, buffId);
        if (buff != null) me.AddBuff(buff, null, weaponTypeId);
    }

    public async UniTask ApplyBuffProxy_Client(string playerId, int weaponTypeId, int buffId)
    {
        if (ModBehaviourF.Instance.IsSelfId(playerId)) return; 
        if (!clientRemoteCharacters.TryGetValue(playerId, out var go) || go == null)
        {
            
            if (!CoopTool._cliPendingProxyBuffs.TryGetValue(playerId, out var list))
                list = CoopTool._cliPendingProxyBuffs[playerId] = new List<(int, int)>();
            list.Add((weaponTypeId, buffId));
            return;
        }

        var cmc = go.GetComponent<CharacterMainControl>();
        if (!cmc) return;

        var buff = await COOPManager.ResolveBuffAsync(weaponTypeId, buffId);
        if (buff != null) cmc.AddBuff(buff, null, weaponTypeId);
    }
}