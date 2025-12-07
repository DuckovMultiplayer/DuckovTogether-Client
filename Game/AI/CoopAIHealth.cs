















using Duckov.UI;
using EscapeFromDuckovCoopMod.Net;  
using EscapeFromDuckovCoopMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EscapeFromDuckovCoopMod;

public class AIHealth
{
    
    private static readonly FieldInfo FI_defaultMax =
        typeof(Health).GetField("defaultMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_lastMax =
        typeof(Health).GetField("lastMaxHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI__current =
        typeof(Health).GetField("_currentHealth", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_characterCached =
        typeof(Health).GetField("characterCached", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_hasCharacter =
        typeof(Health).GetField("hasCharacter", BindingFlags.NonPublic | BindingFlags.Instance);

    
    private static readonly MethodInfo MI_GetActiveHealthBar;
    private static readonly MethodInfo MI_ReleaseHealthBar;

    
    static AIHealth()
    {
        try
        {
            MI_GetActiveHealthBar = AccessTools.DeclaredMethod(typeof(HealthBarManager), "GetActiveHealthBar", new[] { typeof(Health) });
        }
        catch
        {
            MI_GetActiveHealthBar = null;
        }

        try
        {
            MI_ReleaseHealthBar = AccessTools.DeclaredMethod(typeof(HealthBar), "Release", Type.EmptyTypes);
        }
        catch
        {
            MI_ReleaseHealthBar = null;
        }
    }

    private readonly Dictionary<int, float> _cliLastAiHp = new();
    public readonly Dictionary<int, float> _cliLastReportedHp = new();
    public readonly Dictionary<int, float> _cliNextReportAt = new();
    private readonly HashSet<int> _srvDeathHandled = new();

    
    private static int _pendingAiWarningCount = 0;
    private const int PENDING_AI_WARNING_INTERVAL = 200;  

    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private bool networkStarted => Service != null && Service.networkStarted;
    private Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;

    
    
    
    public void Server_BroadcastAiHealth(int aiId, float maxHealth, float currentHealth)
    {
        if (!networkStarted || !IsServer) return;
        var msg = new Net.HybridNet.AIHealthSyncMessage { AiId = aiId, MaxHealth = maxHealth, CurrentHealth = currentHealth };
        Net.HybridNet.HybridNetCore.Send(msg); 
    }


    public void Client_ReportAiHealth(int aiId, float max, float cur)
    {
        if (!networkStarted || IsServer || connectedPeer == null || aiId == 0) return;

        var now = Time.time;
        if (_cliNextReportAt.TryGetValue(aiId, out var next) && now < next)
        {
            if (_cliLastReportedHp.TryGetValue(aiId, out var last) && Mathf.Abs(last - cur) < 0.01f)
                return;
        }

        var w = new NetDataWriter();
        w.Put(aiId);
        w.Put(max);
        w.Put(cur);
        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);

        _cliNextReportAt[aiId] = now + 0.05f;
        _cliLastReportedHp[aiId] = cur;

        if (ModBehaviourF.LogAiHpDebug)
            Debug.Log($"[AI-HP][CLIENT] report aiId={aiId} max={max} cur={cur}");
    }

    public void HandleAiHealthReport(NetPeer sender, NetDataReader r)
    {
        if (!networkStarted || !IsServer) return;

        if (r.AvailableBytes < 12) return;

        var aiId = r.GetInt();
        var max = r.GetFloat();
        var cur = r.GetFloat();

        if (!AITool.aiById.TryGetValue(aiId, out var cmc) || !cmc)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.LogWarning($"[AI-HP][SERVER] report missing AI aiId={aiId} from={sender?.EndPoint}");
            return;
        }

        var h = cmc.Health;
        if (!h)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.LogWarning($"[AI-HP][SERVER] report aiId={aiId} has no Health");
            return;
        }

        var applyMax = max > 0f ? max : h.MaxHealth;
        var maxForClamp = applyMax > 0f ? applyMax : h.MaxHealth;
        var clampedCur = maxForClamp > 0f ? Mathf.Clamp(cur, 0f, maxForClamp) : Mathf.Max(0f, cur);

        var wasDead = false;
        try
        {
            wasDead = h.IsDead;
        }
        catch
        {
        }

        HealthM.Instance.ForceSetHealth(h, applyMax, clampedCur, false);

        if (ModBehaviourF.LogAiHpDebug)
            Debug.Log($"[AI-HP][SERVER] apply report aiId={aiId} max={applyMax} cur={clampedCur} from={sender?.EndPoint}");

        Server_BroadcastAiHealth(aiId, applyMax, clampedCur);

        DamageInfo deathInfo = new DamageInfo();
        if (clampedCur <= 0f && !wasDead)
        {
            if (ModBehaviourF.LogAiHpDebug)
                Debug.Log($"[AI-HP][SERVER] AI死亡触发 aiId={aiId}, 准备生成战利品盒子");

            deathInfo = new DamageInfo();

            try
            {
                deathInfo.damageValue = Mathf.Max(1f, applyMax > 0f ? applyMax : 1f);
            }
            catch
            {
            }

            try
            {
                deathInfo.finalDamage = deathInfo.damageValue;
            }
            catch
            {
            }

            try
            {
                deathInfo.damagePoint = cmc.transform.position;
            }
            catch
            {
            }

            try
            {
                deathInfo.damageNormal = Vector3.up;
            }
            catch
            {
            }

            try
            {
                deathInfo.toDamageReceiver = cmc.mainDamageReceiver;
            }
            catch
            {
            }

            try
            {
                if (playerStatuses != null && sender != null && playerStatuses.TryGetValue(sender, out var st) && st != null)
                    deathInfo.fromCharacter = CharacterMainControl.Main;
            }
            catch
            {
            }
        }

        if (clampedCur <= 0f)
        {
            Server_HandleAuthoritativeAiDeath(cmc, h, aiId, deathInfo, !wasDead);
        }
    }

    private int Server_GetDeathHandleKey(int aiId, CharacterMainControl cmc)
    {
        if (aiId != 0) return aiId;

        if (cmc != null)
        {
            try
            {
                var instId = cmc.GetInstanceID();
                if (instId != 0) return -Mathf.Abs(instId);
            }
            catch
            {
            }
        }

        return int.MinValue;
    }

    private bool Server_TryMarkDeathHandled(int aiId, CharacterMainControl cmc)
    {
        var key = Server_GetDeathHandleKey(aiId, cmc);
        if (key == int.MinValue) return true; 

        return _srvDeathHandled.Add(key);
    }

    private void Server_EnsureAiFullyDead(CharacterMainControl cmc, Health h, int aiId)
    {
        if (cmc == null || h == null) return;
        if (!Server_TryMarkDeathHandled(aiId, cmc)) return;

        Server_DisableAiAfterDeath(cmc, h);
    }

    private void Server_DisableAiAfterDeath(CharacterMainControl cmc, Health h)
    {
        if (cmc == null || h == null) return;

        try
        {
            var ai = cmc.GetComponent<AICharacterController>();
            if (ai) ai.enabled = false;
        }
        catch
        {
        }

        try
        {
            cmc.enabled = false;
        }
        catch
        {
        }

        UniTask.Void(async () =>
        {
            try
            {
                await UniTask.Delay(50);

                try
                {
                    var hb = MI_GetActiveHealthBar?.Invoke(HealthBarManager.Instance, new object[] { h }) as HealthBar;
                    if (hb != null)
                    {
                        if (MI_ReleaseHealthBar != null)
                            MI_ReleaseHealthBar.Invoke(hb, null);
                        else
                            hb.gameObject.SetActive(false);
                    }
                }
                catch
                {
                }

                try
                {
                    if (cmc != null)
                        cmc.gameObject.SetActive(false);
                }
                catch
                {
                }
            }
            catch
            {
            }
        });
    }


    public void Server_HandleAuthoritativeAiDeath(CharacterMainControl cmc, Health h, int aiId, DamageInfo di, bool triggerEvents)
    {
        if (!IsServer || cmc == null || h == null) return;

        if (aiId == 0)
        {
            var tag = ComponentCache.GetNetAiTag(cmc);
            if (tag != null) aiId = tag.aiId;

            if (aiId == 0)
            {
                foreach (var kv in AITool.aiById)
                    if (kv.Value == cmc)
                    {
                        aiId = kv.Key;
                        break;
                    }
            }
        }

        var firstHandle = Server_TryMarkDeathHandled(aiId, cmc);

        if (firstHandle && networkStarted)
        {
            float broadcastMax = 0f;
            float broadcastCur = 0f;

            try
            {
                broadcastMax = h.MaxHealth;
            }
            catch
            {
            }

            try
            {
                broadcastCur = Mathf.Max(0f, h.CurrentHealth);
            }
            catch
            {
            }

            if (broadcastCur <= 0f)
            {
                if (aiId == 0)
                {
                    var tag = ComponentCache.GetNetAiTag(cmc);
                    if (tag != null) aiId = tag.aiId;

                    if (aiId == 0)
                    {
                        foreach (var kv in AITool.aiById)
                            if (kv.Value == cmc)
                            {
                                aiId = kv.Key;
                                break;
                            }
                    }
                }

                if (aiId != 0)
                    Server_BroadcastAiHealth(aiId, broadcastMax, broadcastCur);
            }
        }

        if (triggerEvents && firstHandle)
        {
            var oldContext = DeadLootSpawnContext.InOnDead;
            DeadLootSpawnContext.InOnDead = cmc;

            try
            {
                if (ModBehaviourF.LogAiHpDebug)
                    Debug.Log($"[AI-HP][SERVER] 触发 OnDeadEvent for aiId={aiId} (authoritative)");

                h.OnDeadEvent?.Invoke(di);

                if (ModBehaviourF.LogAiHpDebug)
                    Debug.Log($"[AI-HP][SERVER] OnDeadEvent 触发完成 for aiId={aiId} (authoritative)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AI-HP][SERVER] OnDeadEvent.Invoke failed for aiId={aiId}: {e}");
            }
            finally
            {
                DeadLootSpawnContext.InOnDead = oldContext;
            }

            try
            {
                AITool.TryFireOnDead(h, di);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AI-HP][SERVER] TryFireOnDead failed for aiId={aiId}: {e}");
            }
        }

        if (firstHandle)
            Server_DisableAiAfterDeath(cmc, h);
    }


    public void Client_ApplyAiHealth(int aiId, float max, float cur)
    {
        if (IsServer) return;

        
        if (!AITool.aiById.TryGetValue(aiId, out var cmc) || !cmc)
        {
            COOPManager.AIHandle._cliPendingAiHealth[aiId] = cur;
            if (max > 0f) COOPManager.AIHandle._cliPendingAiMax[aiId] = max;

            
            _pendingAiWarningCount++;
            
            
            
            
            
            return;
        }

        var h = cmc.Health;
        if (!h) return;

        try
        {
            var prev = 0f;
            _cliLastAiHp.TryGetValue(aiId, out prev);
            _cliLastAiHp[aiId] = cur;

            var delta = prev - cur; 
            if (delta > 0.01f)
            {
                var pos = cmc.transform.position + Vector3.up * 1.1f;
                var di = new DamageInfo();
                di.damagePoint = pos;
                di.damageNormal = Vector3.up;
                di.damageValue = delta;
                
                try
                {
                    di.finalDamage = delta;
                }
                catch
                {
                }

                LocalHitKillFx.PopDamageText(pos, di);
            }
        }
        catch
        {
        }

        
        if (max > 0f)
        {
            COOPManager.AIHandle._cliAiMaxOverride[h] = max;
            
            try
            {
                FI_defaultMax?.SetValue(h, Mathf.RoundToInt(max));
            }
            catch
            {
            }

            try
            {
                FI_lastMax?.SetValue(h, -12345f);
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
        }

        
        var nowMax = 0f;
        try
        {
            nowMax = h.MaxHealth;
        }
        catch
        {
        }

        
        if (nowMax > 0f && cur > nowMax + 0.0001f)
        {
            try
            {
                FI__current?.SetValue(h, cur);
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
                h.SetHealth(Mathf.Max(0f, cur));
            }
            catch
            {
                try
                {
                    FI__current?.SetValue(h, Mathf.Max(0f, cur));
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

        
        if (cur <= 0f)
        {
            
            try
            {
                var ai = cmc.GetComponent<AICharacterController>();
                if (ai) ai.enabled = false;
            }
            catch
            {
            }

            
            UniTask.Void(async () =>
            {
                try
                {
                    await UniTask.Delay(50); 

                    
                    try
                    {
                        var hb = MI_GetActiveHealthBar?.Invoke(HealthBarManager.Instance, new object[] { h }) as HealthBar;
                        if (hb != null)
                        {
                            if (MI_ReleaseHealthBar != null)
                                MI_ReleaseHealthBar.Invoke(hb, null);
                            else
                                hb.gameObject.SetActive(false);
                        }
                    }
                    catch
                    {
                    }

                    
                    try
                    {
                        if (cmc != null)
                            cmc.gameObject.SetActive(false);
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    
                }
            });

            
            if (AITool._cliAiDeathFxOnce.Add(aiId))
                FxManager.Client_PlayAiDeathFxAndSfx(cmc);
        }
    }
}
