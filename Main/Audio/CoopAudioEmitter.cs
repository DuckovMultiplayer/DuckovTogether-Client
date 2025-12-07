















using Duckov;
using UnityEngine;

namespace EscapeFromDuckovCoopMod;

public sealed class CoopAudioEmitter : MonoBehaviour
{
    private float _lifeTime;

    public static CoopAudioEmitter Spawn()
    {
        var go = new GameObject("CoopAudioEmitter");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        return go.AddComponent<CoopAudioEmitter>();
    }

    private void Awake()
    {
        _lifeTime = 4f;
    }

    private void Update()
    {
        _lifeTime -= Time.deltaTime;
        if (_lifeTime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public void Play(CoopAudioEventPayload payload)
    {
        transform.position = payload.Position;

        if (payload.HasSwitch || payload.HasSoundKey)
        {
            AudioManager.Post(payload.EventName, gameObject);
        }
        else
        {
            AudioManager.Post(payload.EventName, gameObject);
        }
    }
}
