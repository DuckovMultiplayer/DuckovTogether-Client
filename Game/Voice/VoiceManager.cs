using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Main.Voice;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance;
    
    private ModBehaviourF Service => ModBehaviourF.Instance;
    private string microphoneDevice;
    private AudioClip microphoneClip;
    private bool isRecording;
    public bool isMuted;
    public bool isSpeaking;
    public KeyCode pushToTalkKey = KeyCode.V;
    public bool usePushToTalk = true;
    
    public float maxHearingDistance = 20f;
    public float minVolume = 0.1f;
    
    private const int SAMPLE_RATE = 16000;
    private const int FRAME_SIZE = 960;
    private const int CLIP_LENGTH = 1;
    
    private float[] sampleBuffer;
    private int bufferPosition;
    private Dictionary<string, VoicePlayer> voicePlayers = new Dictionary<string, VoicePlayer>();
    
    private OpusCodec opusCodec;
    private int sequenceNumber;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;
    
    private float voiceUpdateTimer;
    private const float VOICE_UPDATE_INTERVAL = 0.05f;
    
    private float inputVolume = 1.0f;
    private float outputVolume = 1.0f;
    private float noiseReductionLevel = 0.5f;
    private bool smartNoiseReduction = true;
    private SmartNoiseReduction smartNoiseProcessor;

    public void Init()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        opusCodec = new OpusCodec();
        sampleBuffer = new float[FRAME_SIZE];
        bufferPosition = 0;

        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            Debug.Log($"[Voice] Using microphone: {microphoneDevice}");
        }
        else
        {
            Debug.LogWarning("[Voice] No microphone detected");
        }
    }

    private void Update()
    {
        if (Service == null || !Service.networkStarted)
            return;

        HandlePushToTalk();
        ProcessMicrophone();
        UpdateVoiceState();
    }

    private void HandlePushToTalk()
    {
        if (usePushToTalk)
        {
            bool shouldSpeak = Input.GetKey(pushToTalkKey) && !isMuted;
            
            if (shouldSpeak && !isRecording)
            {
                StartRecording();
            }
            else if (!shouldSpeak && isRecording)
            {
                StopRecording();
            }
        }
        else
        {
            if (!isMuted && !isRecording)
            {
                StartRecording();
            }
            else if (isMuted && isRecording)
            {
                StopRecording();
            }
        }
    }

    private void StartRecording()
    {
        if (string.IsNullOrEmpty(microphoneDevice))
            return;

        microphoneClip = Microphone.Start(microphoneDevice, true, CLIP_LENGTH, SAMPLE_RATE);
        isRecording = true;
        isSpeaking = false;
        bufferPosition = 0;
        
        Debug.Log("[Voice] Started recording");
    }

    private void StopRecording()
    {
        if (string.IsNullOrEmpty(microphoneDevice))
            return;

        Microphone.End(microphoneDevice);
        isRecording = false;
        isSpeaking = false;
        
        Debug.Log("[Voice] Stopped recording");
    }

    private void ProcessMicrophone()
    {
        if (!isRecording || microphoneClip == null)
            return;

        int micPosition = Microphone.GetPosition(microphoneDevice);
        if (micPosition < 0 || micPosition == bufferPosition)
            return;

        int samplesAvailable = (micPosition - bufferPosition + SAMPLE_RATE * CLIP_LENGTH) % (SAMPLE_RATE * CLIP_LENGTH);
        
        while (samplesAvailable >= FRAME_SIZE)
        {
            float[] frame = new float[FRAME_SIZE];
            microphoneClip.GetData(frame, bufferPosition);
            
            ApplySmartNoiseReduction(frame);
            for (int i = 0; i < frame.Length; i++)
            {
                frame[i] *= inputVolume;
            }
            
            float volume = CalculateVolume(frame);
            bool isSpeakingNow = volume > 0.01f;
            
            if (isSpeakingNow)
            {
                isSpeaking = true;
                byte[] encodedData = opusCodec.Encode(frame);
                SendVoiceData(encodedData);
            }
            else
            {
                isSpeaking = false;
            }

            bufferPosition = (bufferPosition + FRAME_SIZE) % (SAMPLE_RATE * CLIP_LENGTH);
            samplesAvailable -= FRAME_SIZE;
        }
    }

    private float CalculateVolume(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        return sum / samples.Length;
    }

    private void SendVoiceData(byte[] data)
    {
        if (Service == null || localPlayerStatus == null)
            return;

        var message = new Net.HybridNet.VoiceDataMessage
        {
            SenderEndPoint = localPlayerStatus.EndPoint,
            SequenceNumber = sequenceNumber++,
            AudioData = data,
            Position = localPlayerStatus.Position
        };

        Service.SendVoiceData(message);
    }

    private void UpdateVoiceState()
    {
        voiceUpdateTimer += Time.deltaTime;
        if (voiceUpdateTimer < VOICE_UPDATE_INTERVAL)
            return;

        voiceUpdateTimer = 0f;

        if (localPlayerStatus != null)
        {
            bool stateChanged = localPlayerStatus.IsSpeaking != isSpeaking || localPlayerStatus.IsMuted != isMuted;
            
            if (stateChanged)
            {
                localPlayerStatus.IsSpeaking = isSpeaking;
                localPlayerStatus.IsMuted = isMuted;
                
                var message = new Net.HybridNet.VoiceStateMessage
                {
                    SenderEndPoint = localPlayerStatus.EndPoint,
                    IsSpeaking = isSpeaking,
                    IsMuted = isMuted
                };
                
                Service.SendVoiceState(message);
            }
        }
    }

    public void ReceiveVoiceData(Net.HybridNet.VoiceDataMessage message)
    {
        if (message.SenderEndPoint == localPlayerStatus?.EndPoint)
            return;

        if (!voicePlayers.TryGetValue(message.SenderEndPoint, out var player))
        {
            var playerObj = new GameObject($"VoicePlayer_{message.SenderEndPoint}");
            player = playerObj.AddComponent<VoicePlayer>();
            player.Initialize(message.SenderEndPoint, opusCodec);
            voicePlayers[message.SenderEndPoint] = player;
        }

        player.ReceiveAudioData(message.AudioData, message.Position);
        UpdateProximityVolume(player, message.Position);
    }

    public void ReceiveVoiceState(Net.HybridNet.VoiceStateMessage message)
    {
        PlayerStatus status = null;
        
        if (Service.IsServer)
        {
            foreach (var kvp in Service.playerStatuses)
            {
                if (kvp.Value.EndPoint == message.SenderEndPoint)
                {
                    status = kvp.Value;
                    break;
                }
            }
        }
        else
        {
            Service.clientPlayerStatuses?.TryGetValue(message.SenderEndPoint, out status);
        }

        if (status != null)
        {
            status.IsSpeaking = message.IsSpeaking;
            status.IsMuted = message.IsMuted;
        }
    }

    private void UpdateProximityVolume(VoicePlayer player, Vector3 sourcePosition)
    {
        var levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.MainCharacter == null)
        {
            player.SetVolume(0f);
            return;
        }

        Vector3 listenerPosition = levelManager.MainCharacter.transform.position;
        float distance = Vector3.Distance(listenerPosition, sourcePosition);
        
        float volume = 1f;
        if (distance > maxHearingDistance)
        {
            volume = 0f;
        }
        else if (distance > maxHearingDistance * 0.5f)
        {
            volume = Mathf.Lerp(1f, minVolume, (distance - maxHearingDistance * 0.5f) / (maxHearingDistance * 0.5f));
        }
        
        player.SetVolume(volume);
        player.SetPosition(sourcePosition);
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        Debug.Log($"[Voice] Muted: {isMuted}");
    }

    private void OnDestroy()
    {
        if (isRecording)
        {
            StopRecording();
        }

        foreach (var player in voicePlayers.Values)
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
        }
        
        voicePlayers.Clear();
    }
    
    public void SetMuted(bool muted)
    {
        isMuted = muted;
        if (muted && isRecording)
        {
            StopRecording();
        }
    }
    
    public void SetInputVolume(float volume)
    {
        inputVolume = Mathf.Clamp(volume, 0f, 2f);
    }
    
    public void SetOutputVolume(float volume)
    {
        outputVolume = Mathf.Clamp(volume, 0f, 2f);
        foreach (var player in voicePlayers.Values)
        {
            if (player != null)
            {
                player.SetVolume(outputVolume);
            }
        }
    }
    
    public void SetNoiseReduction(float level)
    {
        noiseReductionLevel = Mathf.Clamp01(level);
    }
    
    public void SetPushToTalkKey(KeyCode key)
    {
        pushToTalkKey = key;
    }
    
    public void SetSmartNoiseReduction(bool enabled)
    {
        smartNoiseReduction = enabled;
        if (smartNoiseProcessor != null)
        {
            if (enabled)
            {
                smartNoiseProcessor.Enable();
                smartNoiseProcessor.SetIntensity(0.7f);
            }
            else
            {
                smartNoiseProcessor.Disable();
            }
        }
    }
    
    public bool IsSmartNoiseReductionEnabled()
    {
        return smartNoiseReduction;
    }
    
    public bool IsSmartNoiseCalibrated()
    {
        return smartNoiseProcessor != null && smartNoiseProcessor.IsCalibrated();
    }
    
    private float ApplyNoiseReduction(float sample)
    {
        float threshold = 0.01f * (1f - noiseReductionLevel);
        if (Mathf.Abs(sample) < threshold)
        {
            return 0f;
        }
        return sample;
    }
    
    private float[] avgSpectrum = null;
    private int spectrumFrameCount = 0;
    
    private void ApplySmartNoiseReduction(float[] samples)
    {
        if (smartNoiseReduction && smartNoiseProcessor != null)
        {
            smartNoiseProcessor.Process(samples);
            return;
        }
        
        if (noiseReductionLevel < 0.01f)
            return;
        
        if (avgSpectrum == null)
        {
            avgSpectrum = new float[samples.Length];
        }
        
        float currentEnergy = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            currentEnergy += samples[i] * samples[i];
        }
        currentEnergy /= samples.Length;
        
        if (currentEnergy < 0.0001f)
        {
            spectrumFrameCount++;
            for (int i = 0; i < samples.Length; i++)
            {
                avgSpectrum[i] = (avgSpectrum[i] * spectrumFrameCount + Mathf.Abs(samples[i])) / (spectrumFrameCount + 1);
            }
        }
        
        float adaptiveThreshold = 0.005f + 0.02f * noiseReductionLevel;
        float gateThreshold = adaptiveThreshold;
        
        for (int i = 0; i < samples.Length; i++)
        {
            float magnitude = Mathf.Abs(samples[i]);
            float noiseEstimate = avgSpectrum != null && i < avgSpectrum.Length ? avgSpectrum[i] : 0f;
            
            if (magnitude < gateThreshold + noiseEstimate * noiseReductionLevel)
            {
                samples[i] = 0f;
            }
            else
            {
                float reduction = 1f - (noiseEstimate * noiseReductionLevel * 0.5f / magnitude);
                samples[i] *= Mathf.Clamp01(reduction);
            }
        }
    }
}
