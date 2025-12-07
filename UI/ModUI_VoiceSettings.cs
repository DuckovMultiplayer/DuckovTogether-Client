using UnityEngine;
using System.Linq;

namespace EscapeFromDuckovCoopMod;

public partial class ModUI
{
    private bool isWaitingForKey;
    private string selectedMicrophone;
    
    private void DrawVoiceSettingsWindow(int windowID)
    {
        if (GUI.Button(new Rect(voiceSettingsWindowRect.width - 25, 5, 20, 20), "×")) 
            showVoiceSettingsWindow = false;

        voiceSettingsScrollPos = GUILayout.BeginScrollView(voiceSettingsScrollPos, GUILayout.ExpandWidth(true));

        var voiceManager = Main.Voice.VoiceManager.Instance;
        if (voiceManager == null)
        {
            GUILayout.Label(CoopLocalization.Get("ui.voice.notAvailable"));
            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }

        GUILayout.Label(CoopLocalization.Get("ui.voice.generalSettings"), GUI.skin.box);
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label(CoopLocalization.Get("ui.voice.mode"), GUILayout.Width(100));
        var newPTT = GUILayout.Toggle(voiceManager.usePushToTalk, CoopLocalization.Get("ui.voice.pushToTalk"));
        if (newPTT != voiceManager.usePushToTalk)
        {
            voiceManager.usePushToTalk = newPTT;
        }
        GUILayout.EndHorizontal();

        if (voiceManager.usePushToTalk)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(CoopLocalization.Get("ui.voice.hotkey"), GUILayout.Width(100));
            
            string keyText = isWaitingForKey ? 
                CoopLocalization.Get("ui.voice.pressKey") : 
                voiceManager.pushToTalkKey.ToString();
            
            if (GUILayout.Button(keyText, GUILayout.Width(150)))
            {
                isWaitingForKey = true;
            }
            GUILayout.EndHorizontal();

            if (isWaitingForKey)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    voiceManager.pushToTalkKey = e.keyCode;
                    isWaitingForKey = false;
                }
            }
        }

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(voiceManager.isMuted ? 
            CoopLocalization.Get("ui.voice.unmute") : 
            CoopLocalization.Get("ui.voice.mute"), 
            GUILayout.Height(30)))
        {
            voiceManager.ToggleMute();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label(CoopLocalization.Get("ui.voice.inputDevice"), GUI.skin.box);
        GUILayout.Space(5);

        var devices = Microphone.devices;
        if (devices.Length == 0)
        {
            GUILayout.Label(CoopLocalization.Get("ui.voice.noMicrophone"));
        }
        else
        {
            foreach (var device in devices)
            {
                bool isSelected = device == selectedMicrophone || 
                    (string.IsNullOrEmpty(selectedMicrophone) && devices.Length > 0 && device == devices[0]);
                
                if (GUILayout.Toggle(isSelected, device))
                {
                    selectedMicrophone = device;
                }
            }
        }

        GUILayout.Space(15);
        GUILayout.Label(CoopLocalization.Get("ui.voice.volumeSettings"), GUI.skin.box);
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label(CoopLocalization.Get("ui.voice.maxDistance"), GUILayout.Width(150));
        GUILayout.Label($"{voiceManager.maxHearingDistance:F0}m", GUILayout.Width(50));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(CoopLocalization.Get("ui.voice.maxDistance"), GUILayout.Width(150));
        float newDistance = GUILayout.HorizontalSlider(voiceManager.maxHearingDistance, 5f, 50f, GUILayout.Width(200));
        if (Mathf.Abs(newDistance - voiceManager.maxHearingDistance) > 0.1f)
        {
            voiceManager.maxHearingDistance = newDistance;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(CoopLocalization.Get("ui.voice.minVolume"), GUILayout.Width(150));
        GUILayout.Label($"{(voiceManager.minVolume * 100):F0}%", GUILayout.Width(50));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(CoopLocalization.Get("ui.voice.minVolume"), GUILayout.Width(150));
        float newMinVolume = GUILayout.HorizontalSlider(voiceManager.minVolume, 0f, 1f, GUILayout.Width(200));
        if (Mathf.Abs(newMinVolume - voiceManager.minVolume) > 0.01f)
        {
            voiceManager.minVolume = newMinVolume;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label(CoopLocalization.Get("ui.voice.activeVoice"), GUI.skin.box);
        GUILayout.Space(5);

        DrawActiveVoiceList();

        GUILayout.Space(10);

        if (voiceManager.isSpeaking)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.green;
            style.fontStyle = FontStyle.Bold;
            GUILayout.Label(CoopLocalization.Get("ui.voice.youAreSpeaking"), style);
        }
        else if (voiceManager.isMuted)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.red;
            GUILayout.Label(CoopLocalization.Get("ui.voice.youAreMuted"), style);
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private void DrawActiveVoiceList()
    {
        if (Service == null || !networkStarted)
        {
            GUILayout.Label(CoopLocalization.Get("ui.voice.notConnected"));
            return;
        }

        int speakingCount = 0;

        if (IsServer)
        {
            foreach (var kvp in playerStatuses)
            {
                if (kvp.Value.IsSpeaking)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("🔊", GUILayout.Width(30));
                    GUILayout.Label(kvp.Value.PlayerName ?? kvp.Value.EndPoint);
                    GUILayout.EndHorizontal();
                    speakingCount++;
                }
            }
        }
        else
        {
            foreach (var kvp in clientPlayerStatuses)
            {
                if (kvp.Value.IsSpeaking)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("🔊", GUILayout.Width(30));
                    GUILayout.Label(kvp.Value.PlayerName ?? kvp.Value.EndPoint);
                    GUILayout.EndHorizontal();
                    speakingCount++;
                }
            }
        }

        if (speakingCount == 0)
        {
            GUILayout.Label(CoopLocalization.Get("ui.voice.noActiveSpeakers"));
        }
    }
}
