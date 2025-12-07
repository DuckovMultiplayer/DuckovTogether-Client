















using System.Collections;
using System.Reflection;

namespace EscapeFromDuckovCoopMod;

[DisallowMultipleComponent]
public class AutoRequestHealthBar : MonoBehaviour
{
    private static readonly FieldInfo FI_character =
        typeof(Health).GetField("characterCached", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo FI_hasChar =
        typeof(Health).GetField("hasCharacter", BindingFlags.NonPublic | BindingFlags.Instance);

    [SerializeField] private int attempts = 30; 
    [SerializeField] private float interval = 0.1f; 

    private void OnEnable()
    {
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        
        yield return null;
        yield return null;

        var cmc = GetComponent<CharacterMainControl>();
        var h = GetComponentInChildren<Health>(true);
        if (!h) yield break;

        
        try
        {
            FI_character?.SetValue(h, cmc);
            FI_hasChar?.SetValue(h, true);
        }
        catch
        {
        }

        for (var i = 0; i < attempts; i++)
        {
            if (!h) yield break;

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
}