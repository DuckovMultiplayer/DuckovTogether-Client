namespace EscapeFromDuckovCoopMod.Utils;

public sealed class PlayerColorManager : MonoBehaviour
{
    public static PlayerColorManager Instance { get; private set; }

    private static readonly Color[] AvailableColors = new[]
    {
        new Color(1f, 0.3f, 0.3f),
        new Color(0.3f, 0.5f, 1f),
        new Color(1f, 0.9f, 0.3f),
        new Color(1f, 0.3f, 1f),
        new Color(0.3f, 1f, 1f),
        new Color(1f, 0.6f, 0.2f),
        new Color(0.6f, 0.3f, 1f),
        new Color(0.3f, 1f, 0.6f),
        new Color(1f, 0.4f, 0.7f),
        new Color(0.7f, 1f, 0.3f)
    };

    private static readonly Color LocalPlayerColor = new Color(0.3f, 1f, 0.3f);

    private readonly Dictionary<string, Color> _playerColorMap = new();
    private readonly HashSet<int> _usedColorIndices = new();
    private readonly System.Random _random = new();

    private void Awake()
    {
        Instance = this;
    }

    public Color GetOrAssignColor(string playerId, bool isLocal)
    {
        if (isLocal) return LocalPlayerColor;

        if (_playerColorMap.TryGetValue(playerId, out var existingColor))
            return existingColor;

        var colorIndex = GetNextAvailableColorIndex();
        var color = AvailableColors[colorIndex];
        _playerColorMap[playerId] = color;
        _usedColorIndices.Add(colorIndex);

        return color;
    }

    private int GetNextAvailableColorIndex()
    {
        if (_usedColorIndices.Count >= AvailableColors.Length)
        {
            _usedColorIndices.Clear();
        }

        int index;
        int attempts = 0;
        do
        {
            index = _random.Next(AvailableColors.Length);
            attempts++;
        } while (_usedColorIndices.Contains(index) && attempts < 100);

        return index;
    }

    public void ReleaseColor(string playerId)
    {
        if (_playerColorMap.TryGetValue(playerId, out var color))
        {
            for (int i = 0; i < AvailableColors.Length; i++)
            {
                if (ColorEquals(AvailableColors[i], color))
                {
                    _usedColorIndices.Remove(i);
                    break;
                }
            }
            _playerColorMap.Remove(playerId);
        }
    }

    private static bool ColorEquals(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f &&
               Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f &&
               Mathf.Abs(a.a - b.a) < 0.01f;
    }

    public void Clear()
    {
        _playerColorMap.Clear();
        _usedColorIndices.Clear();
    }
}
