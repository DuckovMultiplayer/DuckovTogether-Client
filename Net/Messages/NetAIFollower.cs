















namespace EscapeFromDuckovCoopMod;

public sealed class NetAiFollower : MonoBehaviour
{
    
    private static readonly int hMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int hMoveDirX = Animator.StringToHash("MoveDirX");
    private static readonly int hMoveDirY = Animator.StringToHash("MoveDirY");
    private static readonly int hHandState = Animator.StringToHash("HandState");
    private static readonly int hGunReady = Animator.StringToHash("GunReady");
    private static readonly int hDashing = Animator.StringToHash("Dashing");
    private Animator _anim;

    private CharacterAnimationControl _animctl;
    private bool _cGunReady, _cDashing;
    private int _cHand;

    private CharacterMainControl _cmc;

    
    private float _cSpeed, _cDirX, _cDirY;
    private CharacterAnimationControl_MagicBlend _magic;
    private CharacterModel _model;
    private Vector3 _pos, _dir;
    private bool _tGunReady, _tDashing;
    private int _tHand;

    
    private float _tSpeed, _tDirX, _tDirY;


    private void Awake()
    {
        _cmc = GetComponentInParent<CharacterMainControl>(true);

        if (ModBehaviourF.Instance && !AITool.IsRealAI(_cmc))
        {
            Destroy(this);
            return;
        }

        HookModel(_cmc ? _cmc.characterModel : null);
        TryResolveAnimator(true);
    }

    private void Update()
    {
        if (_cmc == null) return;

        
        if (_anim == null || !_anim.isActiveAndEnabled || !_anim.gameObject.activeInHierarchy)
        {
            TryResolveAnimator(true);
            if (_anim == null || !_anim.isActiveAndEnabled || !_anim.gameObject.activeInHierarchy) return;
        }


        
        var t = transform;
        t.position = Vector3.Lerp(t.position, _pos, Time.deltaTime * 20f);

        var rotS = Quaternion.LookRotation(_dir, Vector3.up);
        if (_cmc.modelRoot) _cmc.modelRoot.rotation = rotS;
        t.rotation = rotS;

        
        var lerp = 15f * Time.deltaTime; 
        _cSpeed = Mathf.Lerp(_cSpeed, _tSpeed, lerp);
        _cDirX = Mathf.Lerp(_cDirX, _tDirX, lerp);
        _cDirY = Mathf.Lerp(_cDirY, _tDirY, lerp);

        _cHand = _tHand;
        _cGunReady = _tGunReady;
        _cDashing = _tDashing;

        ApplyNow();
    }

    private void OnEnable()
    {
        
        TryResolveAnimator(true);
    }


    private void OnDestroy()
    {
        UnhookModel();
    }

    private void HookModel(CharacterModel m)
    {
        UnhookModel();
        _model = m;
        if (_model != null)
            
            
            try
            {
                _model.OnCharacterSetEvent += OnModelSet;
            }
            catch
            {
            }
    }

    private void UnhookModel()
    {
        if (_model != null)
            try
            {
                _model.OnCharacterSetEvent -= OnModelSet;
            }
            catch
            {
            }

        _model = null;
    }

    private void OnModelSet()
    {
        
        HookModel(_cmc ? _cmc.characterModel : null);
        TryResolveAnimator(true);
    }

    public void ForceRebindAfterModelSwap()
    {
        HookModel(_cmc ? _cmc.characterModel : null);
        TryResolveAnimator(true);
    }

    private void TryResolveAnimator(bool forceRebind = false)
    {
        _magic = null;
        _anim = null;
        _animctl = null;

        
        var model = _cmc ? _cmc.characterModel : null;
        if (model != null)
            try
            {
                
                var magics = model.GetComponentsInChildren<CharacterAnimationControl_MagicBlend>(true);
                foreach (var m in magics)
                {
                    if (!m) continue;
                    if ((m.characterModel == null || m.characterModel == model) && m.animator)
                        if (m.animator.isActiveAndEnabled && m.animator.gameObject.activeInHierarchy)
                        {
                            _magic = m;
                            _anim = m.animator;
                            break;
                        }
                }

                if (_anim == null)
                {
                    var ctrls = model.GetComponentsInChildren<CharacterAnimationControl>(true);
                    foreach (var c in ctrls)
                    {
                        if (!c) continue;
                        if ((c.characterModel == null || c.characterModel == model) && c.animator)
                            if (c.animator.isActiveAndEnabled && c.animator.gameObject.activeInHierarchy)
                            {
                                _animctl = c;
                                _anim = c.animator;
                                break;
                            }
                    }
                }

                
                if (_anim == null)
                {
                    var anims = model.GetComponentsInChildren<Animator>(true);
                    foreach (var a in anims)
                    {
                        if (!a) continue;
                        if (a.isActiveAndEnabled && a.gameObject.activeInHierarchy)
                        {
                            _anim = a;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

        
        if (_anim == null)
        {
            try
            {
                var m = GetComponentInChildren<CharacterAnimationControl_MagicBlend>(true);
                if (m && m.animator && m.animator.isActiveAndEnabled && m.animator.gameObject.activeInHierarchy)
                {
                    _magic = m;
                    _anim = m.animator;
                }
            }
            catch
            {
            }

            if (_anim == null)
                try
                {
                    var c = GetComponentInChildren<CharacterAnimationControl>(true);
                    if (c && c.animator && c.animator.isActiveAndEnabled && c.animator.gameObject.activeInHierarchy)
                    {
                        _animctl = c;
                        _anim = c.animator;
                    }
                }
                catch
                {
                }
        }

        
        if (_anim == null)
            try
            {
                var anims = GetComponentsInChildren<Animator>(true);
                foreach (var a in anims)
                {
                    if (!a) continue;
                    if (a.isActiveAndEnabled && a.gameObject.activeInHierarchy)
                    {
                        _anim = a;
                        break;
                    }
                }
            }
            catch
            {
            }

        
        if (_anim != null)
        {
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _anim.updateMode = AnimatorUpdateMode.Normal;
            _anim.applyRootMotion = false; 
            if (forceRebind)
                try
                {
                    _anim.Rebind();
                    _anim.Update(0f);
                }
                catch
                {
                }
        }
    }


    
    public void SetTarget(Vector3 pos, Vector3 dir)
    {
        _pos = pos;
        _dir = dir;
    }

    public void SetAnim(float speed, float dirX, float dirY, int hand, bool gunReady, bool dashing)
    {
        _tSpeed = speed;
        _tDirX = dirX;
        _tDirY = dirY;
        _tHand = hand;
        _tGunReady = gunReady;
        _tDashing = dashing;

        
        if (_anim && _cHand == 0 && _cSpeed == 0f && _cDirX == 0f && _cDirY == 0f)
        {
            _cSpeed = _tSpeed;
            _cDirX = _tDirX;
            _cDirY = _tDirY;
            _cHand = _tHand;
            _cGunReady = _tGunReady;
            _cDashing = _tDashing;
            ApplyNow();
        }
    }

    private void ApplyNow()
    {
        if (!_anim) return;
        _anim.SetFloat(hMoveSpeed, _cSpeed);
        _anim.SetFloat(hMoveDirX, _cDirX);
        _anim.SetFloat(hMoveDirY, _cDirY);
        _anim.SetInteger(hHandState, _cHand);
        _anim.SetBool(hGunReady, _cGunReady);
        _anim.SetBool(hDashing, _cDashing);
    }

    
    public void PlayAttack()
    {
        if (_magic != null) _magic.OnAttack();
        if (_animctl != null) _animctl.OnAttack();
    }
}