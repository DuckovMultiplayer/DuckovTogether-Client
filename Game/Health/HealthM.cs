















using EscapeFromDuckovCoopMod.Utils.Logger.Tools;
using System.Collections;

namespace EscapeFromDuckovCoopMod;

public class HealthM : MonoBehaviour
{
    private const float SRV_HP_SEND_COOLDOWN = 0.05f; 
    public static HealthM Instance;
    private static (float max, float cur) _cliLastSentHp = HealthTool._cliLastSentHp;
    private static float _cliNextSendHp = HealthTool._cliNextSendHp;

    public bool _cliApplyingSelfSnap;
    public float _cliEchoMuteUntil;
    private readonly Dictionary<Health, NetPeer> _srvHealthOwner = HealthTool._srvHealthOwner;

    
    private readonly Dictionary<Health, (float max, float cur)> _srvLastSent = new();
    private readonly Dictionary<Health, float> _srvNextSend = new();

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

    public void Init()
    {
        Instance = this;
    }

    internal bool TryGetClientMaxOverride(Health h, out float v)
    {
        return COOPManager.AIHandle._cliAiMaxOverride.TryGetValue(h, out v);
    }


    
    public void Client_SendSelfHealth(Health h, bool force)
    {
        if (_cliApplyingSelfSnap || Time.time < _cliEchoMuteUntil) return;

        if (!networkStarted || IsServer || connectedPeer == null || h == null) return;

        float max = 0f, cur = 0f;
        try
        {
            max = h.MaxHealth;
        }
        catch
        {
        }

        try
        {
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        
        if (!force && Mathf.Approximately(max, _cliLastSentHp.max) && Mathf.Approximately(cur, _cliLastSentHp.cur))
            return;

        
        if (!force && Time.time < _cliNextSendHp) return;

        
        

        
        try
        {
            var debugData = new Dictionary<string, object>
            {
                ["event"] = "Client_SendSelfHealth_Debug",
                ["maxHealth"] = max,
                ["currentHealth"] = cur,
                ["force"] = force,
                ["time"] = Time.time
            };

            try
            {
                var defaultMax = HealthTool.FI_defaultMax?.GetValue(h);
                var lastMax = HealthTool.FI_lastMax?.GetValue(h);
                var _current = HealthTool.FI__current?.GetValue(h);

                debugData["defaultMaxHealth"] = defaultMax;
                debugData["lastMaxHealth"] = lastMax;
                debugData["_currentHealth"] = _current;
                debugData["autoInit"] = h.autoInit;
                debugData["gameObjectName"] = h.gameObject?.name ?? "null";
                debugData["gameObjectActive"] = h.gameObject?.activeSelf ?? false;
            }
            catch (Exception e)
            {
                debugData["reflectionError"] = e.Message;
            }

            
        }
        catch
        {
            
        }

        var msg = new Net.HybridNet.PlayerHealthReportMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? "",
            MaxHealth = max,
            CurrentHealth = cur
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);

        _cliLastSentHp = (max, cur);
        _cliNextSendHp = Time.time + 0.05f;
    }


    public void Server_ForceAuthSelf(Health h)
    {
        if (!networkStarted || !IsServer || h == null) return;
        if (!_srvHealthOwner.TryGetValue(h, out var ownerPeer) || ownerPeer == null) return;

        float max = 0f, cur = 0f;
        try
        {
            max = h.MaxHealth;
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        var msg = new Net.HybridNet.PlayerHealthAuthSelfMessage
        {
            MaxHealth = max,
            CurrentHealth = cur
        };
        Net.HybridNet.HybridNetCore.Send(msg, ownerPeer);
    }

    
    public void Server_ForwardHurtToOwner(NetPeer owner, DamageInfo di)
    {
        if (!IsServer || owner == null) return;

        var msg = new Net.HybridNet.PlayerHurtEventMessage
        {
            Damage = di.damageValue,
            HitPoint = di.damagePoint,
            HitNormal = di.damageNormal,
            CritRate = di.critRate,
            IsCrit = di.crit == 1, 
            WeaponItemId = di.fromWeaponItemID,
            BleedChance = di.bleedChance,
            IsExplosion = di.isExplosion
        };
        Net.HybridNet.HybridNetCore.Send(msg, owner);
    }


    public void Client_ApplySelfHurtFromServer(NetDataReader r)
    {
        try
        {
            
            var dmg = r.GetFloat();
            var ap = r.GetFloat();
            var cdf = r.GetFloat();
            var cr = r.GetFloat();
            var crit = r.GetInt();
            var hit = r.GetV3cm();
            var nrm = r.GetDir();
            var wid = r.GetInt();
            var bleed = r.GetFloat();
            var boom = r.GetBool();

            var main = LevelManager.Instance ? LevelManager.Instance.MainCharacter : null;
            if (!main || main.Health == null) return;

            
            var di = new DamageInfo(main)
            {
                damageValue = dmg,
                armorPiercing = ap,
                critDamageFactor = cdf,
                critRate = cr,
                crit = crit,
                damagePoint = hit,
                damageNormal = nrm,
                fromWeaponItemID = wid,
                bleedChance = bleed,
                isExplosion = boom
            };

            
            HealthTool._cliLastSelfHurtAt = Time.time;

            main.Health.Hurt(di);

            Client_ReportSelfHealth_IfReadyOnce();
        }
        catch (Exception e)
        {
            LoggerHelper.LogWarning("[CLIENT] apply self hurt from server failed: " + e);
        }
    }

    public void Client_ReportSelfHealth_IfReadyOnce()
    {
        if (_cliApplyingSelfSnap || Time.time < _cliEchoMuteUntil) return;
        if (IsServer || HealthTool._cliInitHpReported) return;
        if (connectedPeer == null || connectedPeer.ConnectionState != ConnectionState.Connected) return;

        var main = CharacterMainControl.Main;
        var h = main ? main.GetComponentInChildren<Health>(true) : null;
        if (!h) return;

        float max = 0f, cur = 0f;
        try
        {
            max = h.MaxHealth;
        }
        catch
        {
        }

        try
        {
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        
        var sceneId = "unknown";
        try
        {
            sceneId = localPlayerStatus?.SceneId ?? "null";
        }
        catch
        {
        }

        var logData = new Dictionary<string, object>
        {
            ["event"] = "Client_ReportSelfHealth_IfReadyOnce",
            ["maxHealth"] = max,
            ["currentHealth"] = cur,
            ["sceneId"] = sceneId,
            ["time"] = Time.time,
            ["isValid"] = max > 0f && cur > 0f
        };
        

        
        
        
        
        
        

        var msg = new Net.HybridNet.PlayerHealthReportMessage
        {
            PlayerId = localPlayerStatus?.EndPoint ?? "",
            MaxHealth = max,
            CurrentHealth = cur
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);

        HealthTool._cliInitHpReported = true;
        LoggerHelper.Log($"[HP_REPORT_INIT] ✓ 初始血量上报成功");
    }

    public void Server_OnHealthChanged(NetPeer ownerPeer, Health h)
    {
        if (!IsServer || !h) return;

        float max = 0f, cur = 0f;
        try
        {
            max = h.MaxHealth;
        }
        catch
        {
        }

        try
        {
            cur = h.CurrentHealth;
        }
        catch
        {
        }

        if (max <= 0f) return;
        
        if (_srvLastSent.TryGetValue(h, out var last))
            if (Mathf.Approximately(max, last.max) && Mathf.Approximately(cur, last.cur))
                return;

        var now = Time.time;
        if (_srvNextSend.TryGetValue(h, out var tNext) && now < tNext)
            return;

        _srvLastSent[h] = (max, cur);
        _srvNextSend[h] = now + SRV_HP_SEND_COOLDOWN;

        
        var pid = ModBehaviourF.Instance.GetPlayerId(ownerPeer);

        
        if (ownerPeer != null && ownerPeer.ConnectionState == ConnectionState.Connected)
        {
            var msgSelf = new Net.HybridNet.PlayerHealthAuthSelfMessage
            {
                MaxHealth = max,
                CurrentHealth = cur
            };
            Net.HybridNet.HybridNetCore.Send(msgSelf, ownerPeer);
        }

        var msgRemote = new Net.HybridNet.PlayerHealthAuthRemoteMessage
        {
            PlayerId = pid,
            MaxHealth = max,
            CurrentHealth = cur
        };

        foreach (var p in netManager.ConnectedPeerList)
        {
            if (p == ownerPeer) continue; 
            Net.HybridNet.HybridNetCore.Send(msgRemote, p);
        }
    }

    
    public void Server_EnsureAllHealthHooks()
    {
        if (!IsServer || !networkStarted) return;

        var hostMain = CharacterMainControl.Main;
        if (hostMain) HealthTool.Server_HookOneHealth(null, hostMain.gameObject);

        if (remoteCharacters != null)
            foreach (var kv in remoteCharacters)
            {
                var peer = kv.Key;
                var go = kv.Value;
                if (peer == null || !go) continue;
                HealthTool.Server_HookOneHealth(peer, go);
            }
    }


    
    private static IEnumerator EnsureBarRoutine(Health h, int attempts, float interval)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (h == null) yield break;
            try
            {
                h.showHealthBar = true;
            }
            catch
            {
            }

            try
            {
                h.RequestHealthBar();
            }
            catch
            {
            }

            try
            {
                h.OnMaxHealthChange?.Invoke(h);
            }
            catch
            {
            }

            try
            {
                h.OnHealthChange?.Invoke(h);
            }
            catch
            {
            }

            yield return new WaitForSeconds(interval);
        }
    }

    
    public void ForceSetHealth(Health h, float max, float cur, bool ensureBar = true)
    {
        if (!h) return;

        var nowMax = 0f;
        try
        {
            nowMax = h.MaxHealth;
        }
        catch
        {
        }

        var defMax = 0;
        try
        {
            defMax = (int)(HealthTool.FI_defaultMax?.GetValue(h) ?? 0);
        }
        catch
        {
        }

        
        if (max > 0f && (nowMax <= 0f || max > nowMax + 0.0001f || defMax <= 0))
            try
            {
                HealthTool.FI_defaultMax?.SetValue(h, Mathf.RoundToInt(max));
                HealthTool.FI_lastMax?.SetValue(h, -12345f);
                h.OnMaxHealthChange?.Invoke(h);
            }
            catch
            {
            }

        
        var effMax = 0f;
        try
        {
            effMax = h.MaxHealth;
        }
        catch
        {
        }

        if (effMax > 0f && cur > effMax + 0.0001f)
        {
            try
            {
                HealthTool.FI__current?.SetValue(h, cur);
            }
            catch
            {
            }

            try
            {
                h.OnHealthChange?.Invoke(h);
            }
            catch
            {
            }
        }
        else
        {
            try
            {
                h.SetHealth(cur);
            }
            catch
            {
                try
                {
                    HealthTool.FI__current?.SetValue(h, cur);
                }
                catch
                {
                }
            }

            try
            {
                h.OnHealthChange?.Invoke(h);
            }
            catch
            {
            }
        }

        if (ensureBar)
        {
            try
            {
                h.showHealthBar = true;
            }
            catch
            {
            }

            try
            {
                h.RequestHealthBar();
            }
            catch
            {
            }

            StartCoroutine(EnsureBarRoutine(h, 30, 0.1f));
        }
    }

    

    public void ApplyHealthAndEnsureBar(GameObject go, float max, float cur)
    {
        if (!go) return;

        var cmc = go.GetComponent<CharacterMainControl>();
        var h = go.GetComponentInChildren<Health>(true);
        if (!cmc || !h) return;

        try
        {
            h.autoInit = false;
        }
        catch
        {
        }

        
        HealthTool.BindHealthToCharacter(h, cmc);

        
        ForceSetHealth(h, max > 0 ? max : 40f, cur > 0 ? cur : max > 0 ? max : 40f, false);

        
        try
        {
            h.showHealthBar = true;
        }
        catch
        {
        }

        try
        {
            h.RequestHealthBar();
        }
        catch
        {
        }

        
        try
        {
            h.OnMaxHealthChange?.Invoke(h);
        }
        catch
        {
        }

        try
        {
            h.OnHealthChange?.Invoke(h);
        }
        catch
        {
        }

        
        StartCoroutine(EnsureBarRoutine(h, 8, 0.25f));
    }
}