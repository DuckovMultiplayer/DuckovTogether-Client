















using EscapeFromDuckovCoopMod.Net;  
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class Door
{
    [ThreadStatic] public static bool _applyingDoor; 
    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;


    
    public int ComputeDoorKey(Transform t)
    {
        if (!t) return 0;
        var p = t.position * 10f;
        var k = new Vector3Int(
            Mathf.RoundToInt(p.x),
            Mathf.RoundToInt(p.y),
            Mathf.RoundToInt(p.z)
        );
        return $"Door_{k}".GetHashCode();
    }

    
    public global::Door FindDoorByKey(int key)
    {
        if (key == 0) return null;

        
        if (Utils.GameObjectCacheManager.Instance != null)
        {
            var cachedDoor = Utils.GameObjectCacheManager.Instance.Environment.FindDoorByKey(key);
            if (cachedDoor) return cachedDoor;
        }

        
        var doors = Object.FindObjectsOfType<global::Door>(true);
        foreach (var d in doors)
        {
            if (!d) continue;

            
            var k = ComputeDoorKey(d.transform);
            if (k == key)
            {
                return d;
            }
        }

        return null;
    }

    
    public void Client_RequestDoorSetState(global::Door d, bool closed)
    {
        if (IsServer || connectedPeer == null || d == null) return;

        var key = 0;
        try
        {
            
            key = (int)AccessTools.Field(typeof(global::Door), "doorClosedDataKeyCached").GetValue(d);
        }
        catch
        {
        }

        if (key == 0) key = ComputeDoorKey(d.transform);
        if (key == 0) return;

        var msg = new Net.HybridNet.DoorRequestSetMessage { DoorKey = key, IsClosed = closed };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }

    
    public void Server_HandleDoorSetRequest(NetPeer peer, NetDataReader reader)
    {
        if (!DedicatedServerMode.ShouldRunHostLogic()) return;
        var key = reader.GetInt();
        var isClosed = reader.GetBool();

        var door = FindDoorByKey(key);
        if (!door) return;

        
        if (isClosed) door.Close();
        else door.Open();
        
        
    }

    
    public void Server_BroadcastDoorState(int key, bool closed)
    {
        if (!DedicatedServerMode.ShouldBroadcastState()) return;
        var msg = new Net.HybridNet.DoorStateMessage { DoorKey = key, IsClosed = closed };
        Net.HybridNet.HybridNetCore.Send(msg);
    }

    
    public void Client_ApplyDoorState(int key, bool closed)
    {
        if (IsServer) return;
        var door = FindDoorByKey(key);
        if (!door) return;

        try
        {
            _applyingDoor = true;

            var mSetClosed2 = AccessTools.Method(typeof(global::Door), "SetClosed",
                new[] { typeof(bool), typeof(bool) });
            if (mSetClosed2 != null)
            {
                mSetClosed2.Invoke(door, new object[] { closed, true });
            }
            else
            {
                if (closed)
                    AccessTools.Method(typeof(global::Door), "Close")?.Invoke(door, null);
                else
                    AccessTools.Method(typeof(global::Door), "Open")?.Invoke(door, null);
            }
        }
        finally
        {
            _applyingDoor = false;
        }
    }
}