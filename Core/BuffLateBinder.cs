















using System.Reflection;
using Duckov.Buffs;
using ItemStatsSystem;

namespace EscapeFromDuckovCoopMod;

internal class BuffLateBinder : MonoBehaviour
{
    private Buff _buff;
    private bool _done;
    private FieldInfo _fiEffects;

    private void Update()
    {
        if (_done || _buff == null)
        {
            Destroy(this);
            return;
        }

        
        var cmc = (_buff ? AccessTools.Field(typeof(Buff), "master")?.GetValue(_buff) as CharacterBuffManager : null)?.Master;
        var item = cmc ? cmc.CharacterItem : null;
        if (item == null || item.transform == null) return; 

        
        _buff.transform.SetParent(item.transform, false);

        
        var effectsObj = _fiEffects?.GetValue(_buff) as IList<Effect>;
        if (effectsObj != null)
            for (var i = 0; i < effectsObj.Count; i++)
            {
                var e = effectsObj[i];
                if (e != null) e.SetItem(item);
            }

        
        _done = true;
        Destroy(this);
    }

    public void Init(Buff buff, FieldInfo fiEffects)
    {
        _buff = buff;
        _fiEffects = fiEffects;
    }
}