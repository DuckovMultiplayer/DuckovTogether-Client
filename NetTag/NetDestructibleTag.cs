















using System.Text;

namespace EscapeFromDuckovCoopMod;

[DisallowMultipleComponent]
public class NetDestructibleTag : MonoBehaviour
{
    public uint id;

    private void Awake()
    {
        id = ComputeStableId(gameObject);
    }

    public static uint ComputeStableId(GameObject go)
    {
        var sceneIndex = go.scene.buildIndex;

        var t = go.transform;
        var stack = new Stack<Transform>();
        while (t != null)
        {
            stack.Push(t);
            t = t.parent;
        }

        var sb = new StringBuilder(256);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            sb.Append('/').Append(cur.name).Append('#').Append(cur.GetSiblingIndex());
        }

        var p = go.transform.position;
        var px = Mathf.RoundToInt(p.x * 100f);
        var py = Mathf.RoundToInt(p.y * 100f);
        var pz = Mathf.RoundToInt(p.z * 100f);

        var key = $"{sceneIndex}:{sb}:{px},{py},{pz}";

        
        unchecked
        {
            var hash = 2166136261;
            for (var i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }

            return hash == 0 ? 1u : hash;
        }
    }
}