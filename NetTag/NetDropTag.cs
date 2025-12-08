using ItemStatsSystem;

namespace EscapeFromDuckovCoopMod;

public class NetDropTag : MonoBehaviour
{
    public uint id;


    private void Awake()
    {
        //😆
    }

    private static void AddNetDropTag(GameObject go, uint id)
    {
        if (!go) return;
        var tag = go.GetComponent<NetDropTag>() ?? go.AddComponent<NetDropTag>();
        tag.id = id;
    }

    private static void AddNetDropTag(Item item, uint id)
    {
        try
        {
            var ag = item?.ActiveAgent;
            if (ag && ag.gameObject) AddNetDropTag(ag.gameObject, id);
        }
        catch
        {
        }
    }
}