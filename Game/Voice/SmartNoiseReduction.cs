using UnityEngine;
using System.Collections.Generic;

namespace EscapeFromDuckovCoopMod.Main.Voice;

public class SmartNoiseReduction
{
    private const int SPECTRUM_SIZE = 512;
    private const int NOISE_PROFILE_FRAMES = 30;
    private const float SMOOTHING_FACTOR = 0.85f;
    private const float GATE_THRESHOLD = 0.002f;
    
    private float[] noiseProfile;
    private float[] smoothedSpectrum;
    private Queue<float[]> noiseCalibrationBuffer;
    private bool isCalibrated;
    private int calibrationFrameCount;
    
    private float[] windowFunction;
    private float[] fftBuffer;
    private float[] prevMagnitudes;
    
    private float dynamicThreshold;
    private float noiseFloor;
    private float signalPeakHistory;
    
    public bool IsEnabled { get; set; }
    public float IntensityLevel { get; set; }
    
    public SmartNoiseReduction()
    {
        IsEnabled = true;
        IntensityLevel = 0.7f;
        
        noiseProfile = new float[SPECTRUM_SIZE];
        smoothedSpectrum = new float[SPECTRUM_SIZE];
        prevMagnitudes = new float[SPECTRUM_SIZE];
        noiseCalibrationBuffer = new Queue<float[]>();
        fftBuffer = new float[SPECTRUM_SIZE * 2];
        
        InitializeWindowFunction();
        ResetCalibration();
    }
    
    private void InitializeWindowFunction()
    {
        windowFunction = new float[SPECTRUM_SIZE];
        for (int i = 0; i < SPECTRUM_SIZE; i++)
        {
            windowFunction[i] = 0.54f - 0.46f * Mathf.Cos(2f * Mathf.PI * i / (SPECTRUM_SIZE - 1));
        }
    }
    
    public void ResetCalibration()
    {
        isCalibrated = false;
        calibrationFrameCount = 0;
        noiseCalibrationBuffer.Clear();
        dynamicThreshold = GATE_THRESHOLD;
        noiseFloor = 0.001f;
        signalPeakHistory = 0f;
        
        for (int i = 0; i < SPECTRUM_SIZE; i++)
        {
            noiseProfile[i] = 0f;
            smoothedSpectrum[i] = 0f;
            prevMagnitudes[i] = 0f;
        }
    }
    
    public void Process(float[] samples)
    {
        if (!IsEnabled || samples == null || samples.Length == 0)
            return;
        
        float energy = CalculateEnergy(samples);
        
        if (!isCalibrated)
        {
            CalibrateNoiseProfile(samples, energy);
            return;
        }
        
        if (energy < GATE_THRESHOLD * dynamicThreshold)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= 0.1f;
            }
            return;
        }
        
        ApplySpectralSubtraction(samples);
        ApplyTemporalSmoothing(samples);
        ApplyAdaptiveGating(samples, energy);
    }
    
    private float CalculateEnergy(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }
    
    private void CalibrateNoiseProfile(float[] samples, float energy)
    {
        if (energy < GATE_THRESHOLD * 2f)
        {
            float[] calibrationFrame = new float[samples.Length];
            System.Array.Copy(samples, calibrationFrame, samples.Length);
            noiseCalibrationBuffer.Enqueue(calibrationFrame);
            
            if (noiseCalibrationBuffer.Count > NOISE_PROFILE_FRAMES)
            {
                noiseCalibrationBuffer.Dequeue();
            }
            
            calibrationFrameCount++;
            
            if (calibrationFrameCount >= NOISE_PROFILE_FRAMES)
            {
                BuildNoiseProfile();
                isCalibrated = true;
            }
        }
    }
    
    private void BuildNoiseProfile()
    {
        int frameCount = noiseCalibrationBuffer.Count;
        if (frameCount == 0) return;
        
        foreach (var frame in noiseCalibrationBuffer)
        {
            for (int i = 0; i < Mathf.Min(frame.Length, SPECTRUM_SIZE); i++)
            {
                noiseProfile[i] += Mathf.Abs(frame[i]);
            }
        }
        
        float maxNoise = 0f;
        for (int i = 0; i < SPECTRUM_SIZE; i++)
        {
            noiseProfile[i] /= frameCount;
            if (noiseProfile[i] > maxNoise)
            {
                maxNoise = noiseProfile[i];
            }
        }
        
        noiseFloor = maxNoise * 0.5f;
        dynamicThreshold = 1f + (IntensityLevel * 2f);
    }
    
    private void ApplySpectralSubtraction(float[] samples)
    {
        int processLength = Mathf.Min(samples.Length, SPECTRUM_SIZE);
        
        for (int i = 0; i < processLength; i++)
        {
            float magnitude = Mathf.Abs(samples[i]);
            float noise = noiseProfile[i % noiseProfile.Length];
            
            float subtractAmount = noise * IntensityLevel * dynamicThreshold;
            float cleanMagnitude = Mathf.Max(0f, magnitude - subtractAmount);
            
            float oversubtractionFactor = 1f - (subtractAmount / Mathf.Max(magnitude, 0.0001f));
            oversubtractionFactor = Mathf.Clamp(oversubtractionFactor, 0.1f, 1f);
            
            samples[i] = samples[i] * oversubtractionFactor;
            
            smoothedSpectrum[i] = SMOOTHING_FACTOR * smoothedSpectrum[i] + (1f - SMOOTHING_FACTOR) * cleanMagnitude;
        }
    }
    
    private void ApplyTemporalSmoothing(float[] samples)
    {
        int processLength = Mathf.Min(samples.Length, SPECTRUM_SIZE);
        
        for (int i = 0; i < processLength; i++)
        {
            float current = Mathf.Abs(samples[i]);
            float prev = prevMagnitudes[i];
            
            float smoothed = 0.7f * current + 0.3f * prev;
            
            samples[i] = samples[i] >= 0 ? smoothed : -smoothed;
            prevMagnitudes[i] = smoothed;
        }
    }
    
    private void ApplyAdaptiveGating(float[] samples, float energy)
    {
        signalPeakHistory = Mathf.Max(signalPeakHistory * 0.95f, energy);
        
        float adaptiveGate = noiseFloor * dynamicThreshold;
        float gateRatio = Mathf.Clamp01((energy - adaptiveGate) / (adaptiveGate + 0.001f));
        
        float attackRelease = gateRatio > 0.5f ? 0.9f : 0.7f;
        
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= Mathf.Lerp(0.1f, 1f, gateRatio * attackRelease);
        }
        
        if (energy > signalPeakHistory * 0.3f)
        {
            UpdateNoiseProfileAdaptive(samples, energy);
        }
    }
    
    private void UpdateNoiseProfileAdaptive(float[] samples, float energy)
    {
        if (energy < noiseFloor * 3f)
        {
            float updateRate = 0.01f * IntensityLevel;
            int processLength = Mathf.Min(samples.Length, noiseProfile.Length);
            
            for (int i = 0; i < processLength; i++)
            {
                float magnitude = Mathf.Abs(samples[i]);
                noiseProfile[i] = noiseProfile[i] * (1f - updateRate) + magnitude * updateRate;
            }
        }
    }
    
    public void SetIntensity(float intensity)
    {
        IntensityLevel = Mathf.Clamp01(intensity);
        dynamicThreshold = 1f + (IntensityLevel * 2f);
    }
    
    public bool IsCalibrated()
    {
        return isCalibrated;
    }
    
    public float GetNoiseFloor()
    {
        return noiseFloor;
    }
    
    public void Enable()
    {
        IsEnabled = true;
    }
    
    public void Disable()
    {
        IsEnabled = false;
    }
}
