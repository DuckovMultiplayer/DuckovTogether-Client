















using System.Reflection;
using Duckov;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class MeleeFxStamp : MonoBehaviour
{
    public float lastFxTime;
}

public static class MeleeFx
{
    private static NetService Service => NetService.Instance;

    public static void SpawnSlashFx(CharacterModel ctrl)
    {
        if (!ctrl) return;

        
        ItemAgent_MeleeWeapon melee = null;

        
        Transform[] sockets =
        {
            ctrl.MeleeWeaponSocket,
            
            
            ctrl.GetType().GetField("RightHandSocket") != null ? (Transform)ctrl.GetType().GetField("RightHandSocket").GetValue(ctrl) : null,
            ctrl.GetType().GetField("LefthandSocket") != null ? (Transform)ctrl.GetType().GetField("LefthandSocket").GetValue(ctrl) : null
        };

        foreach (var s in sockets)
        {
            if (melee) break;
            if (!s) continue;
            melee = s.GetComponentInChildren<ItemAgent_MeleeWeapon>(true);
        }

        
        if (!melee)
            melee = ctrl.GetComponentInChildren<ItemAgent_MeleeWeapon>(true);

        if (!melee || !melee.slashFx) return;

        
        var stamp = ctrl.GetComponent<MeleeFxStamp>() ?? ctrl.gameObject.AddComponent<MeleeFxStamp>();
        if (Time.time - stamp.lastFxTime < 0.01f) return; 
        stamp.lastFxTime = Time.time;

        
        var delay = Mathf.Max(0f, melee.slashFxDelayTime);

        var t = ctrl.transform;
        var forward = Mathf.Clamp(melee.AttackRange * 0.6f, 0.2f, 2.5f);
        var pos = t.position + t.forward * forward + Vector3.up * 0.6f;
        var rot = Quaternion.LookRotation(t.forward, Vector3.up);

        UniTask.Void(async () =>
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay));
                Object.Instantiate(melee.slashFx, pos, rot);
            }
            catch
            {
            }
        });
    }
}

public static class FxManager
{
    private static readonly Dictionary<ItemAgent_Gun, GameObject> _muzzleFxByGun = new();
    private static readonly Dictionary<ItemAgent_Gun, ParticleSystem> _shellPsByGun = new();

    
    public static GameObject defaultMuzzleFx;


    
    private static readonly MethodInfo MI_StartVisualRecoil =
        AccessTools.Method(typeof(ItemAgent_Gun), "StartVisualRecoil");

    private static readonly FieldInfo FI_RecoilBack =
        AccessTools.Field(typeof(ItemAgent_Gun), "_recoilBack");

    private static readonly FieldInfo FI_ShellParticle =
        AccessTools.Field(typeof(ItemAgent_Gun), "shellParticle");

    
    private static readonly Dictionary<CharacterMainControl, object> _hvCache = new();

    
    private static Action<DamageInfo> _cachedOnDead;
    private static MethodInfo _miOnDead;

    
    private static bool? _killMarkerSoundExists;

    private static NetService Service => NetService.Instance;
    private static bool IsServer => Service != null && Service.IsServer;
    private static NetManager netManager => Service?.netManager;
    private static NetDataWriter writer => Service?.writer;
    private static NetPeer connectedPeer => Service?.connectedPeer;
    private static PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    private static bool networkStarted => Service != null && Service.networkStarted;
    private static Dictionary<NetPeer, GameObject> remoteCharacters => Service?.remoteCharacters;
    private static Dictionary<NetPeer, PlayerStatus> playerStatuses => Service?.playerStatuses;
    private static Dictionary<string, GameObject> clientRemoteCharacters => Service?.clientRemoteCharacters;


    public static void PlayMuzzleFxAndShell(string shooterId, int weaponType, Vector3 muzzlePos, Vector3 finalDir)
    {
        try
        {
            
            GameObject shooterGo = null;
            if (NetService.Instance.IsSelfId(shooterId))
            {
                var cmSelf = LevelManager.Instance?.MainCharacter?.GetComponent<CharacterMainControl>();
                if (cmSelf) shooterGo = cmSelf.gameObject;
            }
            else if (!string.IsNullOrEmpty(shooterId) && shooterId.StartsWith("AI:"))
            {
                if (int.TryParse(shooterId.Substring(3), out var aiId))
                    if (AITool.aiById.TryGetValue(aiId, out var cmc) && cmc)
                        shooterGo = cmc.gameObject;
            }
            else
            {
                if (IsServer)
                {
                    
                    NetPeer foundPeer = null;
                    foreach (var kv in playerStatuses)
                        if (kv.Value != null && kv.Value.EndPoint == shooterId)
                        {
                            foundPeer = kv.Key;
                            break;
                        }

                    if (foundPeer != null) remoteCharacters.TryGetValue(foundPeer, out shooterGo);
                }
                else
                {
                    
                    clientRemoteCharacters.TryGetValue(shooterId, out shooterGo);
                }
            }

            
            ItemAgent_Gun gun = null;
            Transform muzzleTf = null;
            if (!string.IsNullOrEmpty(shooterId))
                if (LocalPlayerManager.Instance._gunCacheByShooter.TryGetValue(shooterId, out var cached) && cached.gun)
                {
                    gun = cached.gun;
                    muzzleTf = cached.muzzle;
                }

            
            
            CharacterMainControl shooterCmc = null;
            if (shooterGo && (!gun || !muzzleTf))
            {
                shooterCmc = shooterGo.GetComponent<CharacterMainControl>();
                var model = shooterCmc ? shooterCmc.characterModel : null;

                if (!gun && model)
                {
                    if (model.RightHandSocket && !gun) gun = model.RightHandSocket.GetComponentInChildren<ItemAgent_Gun>(true);
                    if (model.LefthandSocket && !gun) gun = model.LefthandSocket.GetComponentInChildren<ItemAgent_Gun>(true);
                    if (model.MeleeWeaponSocket && !gun) gun = model.MeleeWeaponSocket.GetComponentInChildren<ItemAgent_Gun>(true);
                }

                if (!gun) gun = shooterCmc ? shooterCmc.CurrentHoldItemAgent as ItemAgent_Gun : null;

                if (gun && gun.muzzle && !muzzleTf) muzzleTf = gun.muzzle;

                if (!string.IsNullOrEmpty(shooterId) && gun) LocalPlayerManager.Instance._gunCacheByShooter[shooterId] = (gun, muzzleTf);
            }

            
            GameObject tmp = null;
            if (!muzzleTf)
            {
                tmp = new GameObject("TempMuzzleFX");
                tmp.transform.position = muzzlePos;
                tmp.transform.rotation = Quaternion.LookRotation(finalDir, Vector3.up);
                muzzleTf = tmp.transform;
            }

            
            Client_PlayLocalShotFx(gun, muzzleTf, weaponType);

            if (tmp) GameObject.Destroy(tmp, 0.2f);

            
            
            if (!IsServer && shooterGo)
            {
                if (shooterCmc == null) shooterCmc = shooterGo.GetComponent<CharacterMainControl>();
                var model = shooterCmc?.characterModel;
                var anim = model ? model.GetComponent<CharacterAnimationControl_MagicBlend>() : null;
                if (anim && anim.animator) anim.OnAttack();
            }
        }
        catch
        {
            
        }
    }

    public static void Client_PlayAiDeathFxAndSfx(CharacterMainControl cmc)
    {
        if (!cmc) return;
        var model = cmc.characterModel;
        if (!model) return;

        
        if (!_hvCache.TryGetValue(cmc, out var hv) || hv == null)
        {
            
            try
            {
                var fi = model.GetType().GetField("hurtVisual",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fi != null) hv = fi.GetValue(model);
            }
            catch
            {
            }

            if (hv == null)
                try
                {
                    hv = model.GetComponentInChildren(typeof(HurtVisual), true);
                }
                catch
                {
                }

            
            _hvCache[cmc] = hv;
        }

        if (hv != null)
        {
            
            if (_cachedOnDead == null && _miOnDead == null)
            {
                try
                {
                    _miOnDead = hv.GetType().GetMethod("OnDead",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (_miOnDead != null)
                    {
                        try
                        {
                            _cachedOnDead = (Action<DamageInfo>)Delegate.CreateDelegate(
                                typeof(Action<DamageInfo>), hv, _miOnDead);
                        }
                        catch
                        {
                            
                        }
                    }
                }
                catch
                {
                }
            }

            try
            {
                var di = new DamageInfo
                {
                    
                    damagePoint = cmc.transform.position,
                    damageNormal = Vector3.up
                };

                
                if (_cachedOnDead != null)
                    _cachedOnDead(di);
                else if (_miOnDead != null)
                    _miOnDead.Invoke(hv, new object[] { di });

                
                if (_killMarkerSoundExists == null)
                    _killMarkerSoundExists = FmodEventExists("event:/e_KillMarker");

                if (_killMarkerSoundExists == true)
                    AudioManager.Post("e_KillMarker");
            }
            catch
            {
            }
        }
    }

    public static void Client_PlayLocalShotFx(ItemAgent_Gun gun, Transform muzzleTf, int weaponType)
    {
        if (!muzzleTf) return;

        GameObject ResolveMuzzlePrefab()
        {
            GameObject fxPfb = null;
            LocalPlayerManager.Instance._muzzleFxCacheByWeaponType.TryGetValue(weaponType, out fxPfb);
            if (!fxPfb && gun && gun.GunItemSetting) fxPfb = gun.GunItemSetting.muzzleFxPfb;
            if (!fxPfb) fxPfb = defaultMuzzleFx;
            return fxPfb;
        }

        void PlayFxGameObject(GameObject go)
        {
            if (!go) return;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
            else
            {
                go.SetActive(false);
                go.SetActive(true);
            }
        }

        
        if (gun != null)
        {
            if (!_muzzleFxByGun.TryGetValue(gun, out var fxGo) || !fxGo)
            {
                var fxPfb = ResolveMuzzlePrefab();
                if (fxPfb)
                {
                    fxGo = GameObject.Instantiate(fxPfb, muzzleTf, false);
                    fxGo.transform.localPosition = Vector3.zero;
                    fxGo.transform.localRotation = Quaternion.identity;
                    _muzzleFxByGun[gun] = fxGo;
                }
            }

            PlayFxGameObject(fxGo);

            if (!_shellPsByGun.TryGetValue(gun, out var shellPs) || shellPs == null)
            {
                try
                {
                    shellPs = (ParticleSystem)FI_ShellParticle?.GetValue(gun);
                }
                catch
                {
                    shellPs = null;
                }

                _shellPsByGun[gun] = shellPs;
            }

            try
            {
                if (shellPs) shellPs.Emit(1);
            }
            catch
            {
            }

            TryStartVisualRecoil_NoAlloc(gun);
            return;
        }

        
        var pfb = ResolveMuzzlePrefab();
        if (pfb)
        {
            var tempFx = GameObject.Instantiate(pfb, muzzleTf, false);
            tempFx.transform.localPosition = Vector3.zero;
            tempFx.transform.localRotation = Quaternion.identity;

            var ps = tempFx.GetComponent<ParticleSystem>();
            if (ps)
            {
                ps.Play(true);
            }
            else
            {
                tempFx.SetActive(false);
                tempFx.SetActive(true);
            }

            GameObject.Destroy(tempFx, 0.5f);
        }
    }

    public static void TryStartVisualRecoil_NoAlloc(ItemAgent_Gun gun)
    {
        if (!gun) return;
        try
        {
            MI_StartVisualRecoil?.Invoke(gun, null);
            return;
        }
        catch
        {
        }

        try
        {
            FI_RecoilBack?.SetValue(gun, true);
        }
        catch
        {
        }
    }

    private static bool FmodEventExists(string path)
    {
        try
        {
            var sys = RuntimeManager.StudioSystem;
            if (!sys.isValid()) return false;
            EventDescription desc;
            var r = sys.getEvent(path, out desc);
            return r == RESULT.OK && desc.isValid();
        }
        catch
        {
            return false;
        }
    }
}