















using System.Reflection;
using Duckov.Utilities;
using Duckov.Weathers;
using Object = UnityEngine.Object;
using EscapeFromDuckovCoopMod.Net;
using EscapeFromDuckovCoopMod.Utils;  

namespace EscapeFromDuckovCoopMod;

public class Weather
{
    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;

    private bool networkStarted => Service != null && Service.networkStarted;

    
    private static FieldInfo _fieldSeed;
    private static FieldInfo _fieldForceWeather;
    private static FieldInfo _fieldForceWeatherValue;
    private static bool _fieldsInitialized = false;

    
    public void Server_BroadcastEnvSync(NetPeer target = null)
    {
        if (!IsServer || netManager == null) return;

        
        var day = GameClock.Day; 
        var secOfDay = GameClock.TimeOfDay.TotalSeconds; 
        var timeScale = 60f;
        try
        {
            timeScale = GameClock.Instance.clockTimeScale;
        }
        catch
        {
        } 

        
        var wm = WeatherManager.Instance;
        var seed = -1;
        var forceWeather = false;
        var forceWeatherVal = (int)Duckov.Weathers.Weather.Sunny;
        var currentWeather = (int)Duckov.Weathers.Weather.Sunny;
        byte stormLevel = 0;

        if (wm != null)
        {
            
            if (!_fieldsInitialized)
            {
                var wmType = wm.GetType();
                try
                {
                    _fieldSeed = AccessTools.Field(wmType, "seed");
                }
                catch {  }

                try
                {
                    _fieldForceWeather = AccessTools.Field(wmType, "forceWeather");
                }
                catch {  }

                try
                {
                    _fieldForceWeatherValue = AccessTools.Field(wmType, "forceWeatherValue");
                }
                catch {  }

                _fieldsInitialized = true;
            }

            
            try
            {
                if (_fieldSeed != null)
                    seed = (int)_fieldSeed.GetValue(wm);
            }
            catch { }

            try
            {
                if (_fieldForceWeather != null)
                    forceWeather = (bool)_fieldForceWeather.GetValue(wm);
            }
            catch { }

            try
            {
                if (_fieldForceWeatherValue != null)
                    forceWeatherVal = (int)_fieldForceWeatherValue.GetValue(wm);
            }
            catch { }

            try
            {
                currentWeather = (int)WeatherManager.GetWeather();
            }
            catch
            {
            } 

            try
            {
                stormLevel = (byte)wm.Storm.GetStormLevel(GameClock.Now);
            }
            catch
            {
            } 
        }

        
        var w = new NetDataWriter();
        w.Put(day);
        w.Put(secOfDay);
        w.Put(timeScale);
        w.Put(seed);
        w.Put(forceWeather);
        w.Put(forceWeatherVal);
        w.Put(currentWeather);
        w.Put(stormLevel);

        try
        {
            
            IEnumerable<LootBoxLoader> loaders = GameObjectCacheManager.Instance != null
                ? GameObjectCacheManager.Instance.Environment.GetAllLoaders()
                : Object.FindObjectsOfType<LootBoxLoader>(true);

            
            var tmp = new List<(int k, bool on)>();
            foreach (var l in loaders)
            {
                if (!l || !l.gameObject) continue;
                var k = LootManager.Instance.ComputeLootKey(l.transform);
                var on = l.gameObject.activeSelf; 
                tmp.Add((k, on));
            }

            w.Put(tmp.Count);
            for (var i = 0; i < tmp.Count; ++i)
            {
                w.Put(tmp[i].k);
                w.Put(tmp[i].on);
            }
        }
        catch
        {
            
            w.Put(0);
        }

        

        var includeDoors = target != null;
        if (includeDoors)
        {
            
            IEnumerable<global::Door> doors = GameObjectCacheManager.Instance != null
                ? GameObjectCacheManager.Instance.Environment.GetAllDoors()
                : Object.FindObjectsOfType<global::Door>(true);
            var tmp = new List<(int key, bool closed)>();

            foreach (var d in doors)
            {
                if (!d) continue;
                var k = 0;
                try
                {
                    k = (int)AccessTools.Field(typeof(global::Door), "doorClosedDataKeyCached").GetValue(d);
                }
                catch
                {
                }

                if (k == 0) k = COOPManager.Door.ComputeDoorKey(d.transform);

                bool closed;
                try
                {
                    closed = !d.IsOpen;
                }
                catch
                {
                    closed = true;
                } 

                tmp.Add((k, closed));
            }

            w.Put(tmp.Count);
            for (var i = 0; i < tmp.Count; ++i)
            {
                w.Put(tmp[i].key);
                w.Put(tmp[i].closed);
            }
        }
        else
        {
            w.Put(0); 
        }

        w.Put(COOPManager.destructible._deadDestructibleIds.Count);
        foreach (var id in COOPManager.destructible._deadDestructibleIds) w.Put(id);

        var msg = new Net.HybridNet.EnvironmentSyncStateMessage { StateData = w.Data };
        if (target != null) Net.HybridNet.HybridNetCore.Send(msg, target);
        else Net.HybridNet.HybridNetCore.Send(msg);
    }


    
    public void Client_RequestEnvSync()
    {
        if (IsServer || connectedPeer == null) return;
        var msg = new Net.HybridNet.EnvironmentSyncRequestMessage();
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }

    
    public void Client_ApplyEnvSync(long day, double secOfDay, float timeScale, int seed, bool forceWeather, int forceWeatherVal, int currentWeather ,
        byte stormLevel )
    {
        
        try
        {
            var inst = GameClock.Instance;
            if (inst != null)
            {
                AccessTools.Field(inst.GetType(), "days")?.SetValue(inst, day);
                AccessTools.Field(inst.GetType(), "secondsOfDay")?.SetValue(inst, secOfDay);
                try
                {
                    inst.clockTimeScale = timeScale;
                }
                catch
                {
                }

                
                typeof(GameClock).GetMethod("Step", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, new object[] { 0f });
            }
        }
        catch
        {
        }

        
        try
        {
            var wm = WeatherManager.Instance;
            if (wm != null && seed != -1)
            {
                AccessTools.Field(wm.GetType(), "seed")?.SetValue(wm, seed); 
                wm.GetType().GetMethod("SetupModules", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(wm, null); 
                AccessTools.Field(wm.GetType(), "_weatherDirty")?.SetValue(wm, true); 
            }
        }
        catch
        {
        }

        
        try
        {
            WeatherManager.SetForceWeather(forceWeather, (Duckov.Weathers.Weather)forceWeatherVal); 
        }
        catch
        {
        }

        
    }
}