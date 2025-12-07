using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Main.Voice;

public class VoicePlayer : MonoBehaviour
{
    private string endPoint;
    private AudioSource audioSource;
    private OpusCodec opusCodec;
    private Queue<float[]> audioQueue = new Queue<float[]>();
    private float volumeMultiplier = 1.0f;
    private const int MAX_QUEUE_SIZE = 10;
    private Vector3 targetPosition;

    public void Initialize(string playerEndPoint, OpusCodec codec)
    {
        endPoint = playerEndPoint;
        opusCodec = codec;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
    }

    public void ReceiveAudioData(byte[] encodedData, Vector3 position)
    {
        targetPosition = position;
        
        float[] decodedSamples = opusCodec.Decode(encodedData);
        if (decodedSamples != null && decodedSamples.Length > 0)
        {
            if (audioQueue.Count < MAX_QUEUE_SIZE)
            {
                audioQueue.Enqueue(decodedSamples);
            }
        }
    }

    public void SetVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume * volumeMultiplier);
        }
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);

        if (audioQueue.Count > 0 && !audioSource.isPlaying)
        {
            PlayNextFrame();
        }
    }

    private void PlayNextFrame()
    {
        if (audioQueue.Count == 0)
            return;

        float[] samples = audioQueue.Dequeue();
        
        AudioClip clip = AudioClip.Create($"Voice_{endPoint}", samples.Length, 1, 16000, false);
        clip.SetData(samples, 0);
        
        audioSource.clip = clip;
        audioSource.Play();
    }
}
