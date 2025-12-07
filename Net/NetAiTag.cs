















namespace EscapeFromDuckovCoopMod;

public sealed class NetAiTag : MonoBehaviour
{
    public int aiId;
    public string nameOverride; 
    public int? iconTypeOverride; 
    public bool? showNameOverride; 

    private void Awake()
    {
        Guard();
    }

    private void OnEnable()
    {
        Guard();
    }

    private void Guard()
    {
        try
        {
            var cmc = GetComponent<CharacterMainControl>();
            var mod = ModBehaviourF.Instance;
            if (!cmc || mod == null) return;

            if (!AITool.IsRealAI(cmc)) Destroy(this);
        }
        catch
        {
        }
    }
}