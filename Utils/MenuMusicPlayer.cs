using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Utils;

public class MenuMusicPlayer : MonoBehaviour
{
    private static MenuMusicPlayer _instance;
    public static MenuMusicPlayer Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("MenuMusicPlayer");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MenuMusicPlayer>();
            }
            return _instance;
        }
    }

    private string _musicFilePath;
    private bool _isPlaying;
    private bool _isPaused;
    
    public KeyCode pauseKey = KeyCode.M;

    [DllImport("winmm.dll")]
    private static extern long mciSendString(string command, System.Text.StringBuilder returnValue, int returnLength, IntPtr hwndCallback);

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        string modDirectory = Path.GetDirectoryName(typeof(MenuMusicPlayer).Assembly.Location);
        _musicFilePath = Path.Combine(modDirectory, "Audio", "room_music.wav");

        if (!File.Exists(_musicFilePath))
        {
            Debug.LogWarning($"[MenuMusic] Music file not found: {_musicFilePath}");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    public void PlayMusic()
    {
        if (_isPlaying) return;
        if (!File.Exists(_musicFilePath))
        {
            Debug.LogWarning("[MenuMusic] Cannot play: music file not found");
            return;
        }

        try
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                PlayWithMCI();
            }
            else
            {
                Debug.LogWarning("[MenuMusic] Music playback only supported on Windows");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MenuMusic] Failed to play music: {ex.Message}");
        }
    }

    private void PlayWithMCI()
    {
        string alias = "MenuMusic";
        
        mciSendString($"close {alias}", null, 0, IntPtr.Zero);
        
        string openCommand = $"open \"{_musicFilePath}\" type waveaudio alias {alias}";
        long result = mciSendString(openCommand, null, 0, IntPtr.Zero);
        
        if (result != 0)
        {
            Debug.LogError($"[MenuMusic] Failed to open music file. Error code: {result}");
            return;
        }

        string playCommand = $"play {alias}";
        result = mciSendString(playCommand, null, 0, IntPtr.Zero);
        
        if (result == 0)
        {
            _isPlaying = true;
            _isPaused = false;
            Debug.Log("[MenuMusic] Music started playing");
        }
        else
        {
            Debug.LogError($"[MenuMusic] Failed to play music. Error code: {result}");
        }
    }

    public void StopMusic()
    {
        if (!_isPlaying) return;

        try
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                string alias = "MenuMusic";
                mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
                mciSendString($"close {alias}", null, 0, IntPtr.Zero);
                _isPlaying = false;
                _isPaused = false;
                Debug.Log("[MenuMusic] Music stopped");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MenuMusic] Failed to stop music: {ex.Message}");
        }
    }

    public void TogglePause()
    {
        if (!_isPlaying) return;

        try
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || 
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                string alias = "MenuMusic";
                if (_isPaused)
                {
                    mciSendString($"resume {alias}", null, 0, IntPtr.Zero);
                    _isPaused = false;
                    Debug.Log("[MenuMusic] Music resumed");
                }
                else
                {
                    mciSendString($"pause {alias}", null, 0, IntPtr.Zero);
                    _isPaused = true;
                    Debug.Log("[MenuMusic] Music paused");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MenuMusic] Failed to toggle pause: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        StopMusic();
    }

    private void OnApplicationQuit()
    {
        StopMusic();
    }
}
