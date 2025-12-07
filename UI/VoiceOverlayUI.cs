using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EscapeFromDuckovCoopMod.Main.UI;

public class VoiceOverlayUI : MonoBehaviour
{
    public static VoiceOverlayUI Instance;
    
    private NetService Service => NetService.Instance;
    private Utils.Database.PlayerInfoDatabase PlayerDb => Utils.Database.PlayerInfoDatabase.Instance;
    
    private Rect overlayRect = new Rect(10, 200, 260, 400);
    private Vector2 scrollPosition = Vector2.zero;
    
    private const float AVATAR_SIZE = 48f;
    private const float SPEAKING_AVATAR_SIZE = 56f;
    private const float ENTRY_HEIGHT = 70f;
    
    private GUIStyle avatarStyle;
    private GUIStyle speakingAvatarStyle;
    private GUIStyle nameStyle;
    private GUIStyle speakingNameStyle;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        InitializeStyles();
    }
    
    private void InitializeStyles()
    {
        avatarStyle = new GUIStyle();
        avatarStyle.normal.background = Texture2D.whiteTexture;
        
        speakingAvatarStyle = new GUIStyle();
        speakingAvatarStyle.normal.background = Texture2D.whiteTexture;
        
        nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = 14;
        nameStyle.fontStyle = FontStyle.Normal;
        nameStyle.normal.textColor = Color.white;
        
        speakingNameStyle = new GUIStyle(GUI.skin.label);
        speakingNameStyle.fontSize = 16;
        speakingNameStyle.fontStyle = FontStyle.Bold;
        speakingNameStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
    }
    
    private void OnGUI()
    {
        if (Service == null || !Service.networkStarted)
            return;
        
        DrawVoiceOverlay();
    }
    
    private void DrawVoiceOverlay()
    {
        var speakingPlayers = GetSpeakingPlayers();
        
        if (speakingPlayers.Count == 0)
            return;
        
        float totalHeight = Mathf.Min(speakingPlayers.Count * ENTRY_HEIGHT + 20, overlayRect.height);
        overlayRect.height = totalHeight;
        
        GUILayout.BeginArea(overlayRect);
        
        GUI.Box(new Rect(0, 0, overlayRect.width, overlayRect.height), "");
        
        GUILayout.Space(5);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(overlayRect.width), GUILayout.Height(overlayRect.height - 10));
        
        foreach (var player in speakingPlayers)
        {
            DrawPlayerEntry(player);
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    
    private void DrawPlayerEntry(PlayerVoiceInfo player)
    {
        GUILayout.BeginHorizontal();
        
        float avatarSize = player.IsSpeaking ? SPEAKING_AVATAR_SIZE : AVATAR_SIZE;
        GUIStyle currentAvatarStyle = player.IsSpeaking ? speakingAvatarStyle : avatarStyle;
        GUIStyle currentNameStyle = player.IsSpeaking ? speakingNameStyle : nameStyle;
        
        if (player.AvatarTexture != null)
        {
            Color originalColor = GUI.color;
            
            if (player.IsSpeaking)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 5f);
                GUI.color = new Color(pulse, 1f, pulse);
            }
            
            GUILayout.Box(player.AvatarTexture, currentAvatarStyle, 
                GUILayout.Width(avatarSize), GUILayout.Height(avatarSize));
            
            GUI.color = originalColor;
        }
        else
        {
            GUILayout.Box("", currentAvatarStyle, 
                GUILayout.Width(avatarSize), GUILayout.Height(avatarSize));
        }
        
        GUILayout.BeginVertical();
        GUILayout.Space((avatarSize - 40) * 0.5f);
        
        GUILayout.Label(player.PlayerName, currentNameStyle);
        
        if (player.IsMuted)
        {
            GUILayout.Label("[Muted]", nameStyle);
        }
        
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
    }
    
    private List<PlayerVoiceInfo> GetSpeakingPlayers()
    {
        var result = new List<PlayerVoiceInfo>();
        
        if (Service.IsServer)
        {
            if (Service.localPlayerStatus != null && Service.localPlayerStatus.IsSpeaking)
            {
                var info = GetPlayerVoiceInfo(Service.localPlayerStatus.EndPoint, Service.localPlayerStatus);
                if (info != null)
                    result.Add(info);
            }
            
            foreach (var kvp in Service.playerStatuses)
            {
                if (kvp.Value.IsSpeaking || kvp.Value.IsMuted)
                {
                    var info = GetPlayerVoiceInfo(kvp.Value.EndPoint, kvp.Value);
                    if (info != null)
                        result.Add(info);
                }
            }
        }
        else
        {
            if (Service.localPlayerStatus != null && Service.localPlayerStatus.IsSpeaking)
            {
                var info = GetPlayerVoiceInfo(Service.localPlayerStatus.EndPoint, Service.localPlayerStatus);
                if (info != null)
                    result.Add(info);
            }
            
            foreach (var kvp in Service.clientPlayerStatuses)
            {
                if (kvp.Value.IsSpeaking || kvp.Value.IsMuted)
                {
                    var info = GetPlayerVoiceInfo(kvp.Key, kvp.Value);
                    if (info != null)
                        result.Add(info);
                }
            }
        }
        
        return result.OrderByDescending(p => p.IsSpeaking).ToList();
    }
    
    private PlayerVoiceInfo GetPlayerVoiceInfo(string endPoint, PlayerStatus status)
    {
        if (string.IsNullOrEmpty(endPoint) || status == null)
            return null;
        
        var playerEntity = PlayerDb?.GetPlayerByEndPoint(endPoint);
        
        return new PlayerVoiceInfo
        {
            EndPoint = endPoint,
            PlayerName = playerEntity?.PlayerName ?? status.PlayerName ?? "Unknown",
            AvatarTexture = playerEntity?.AvatarTexture,
            IsSpeaking = status.IsSpeaking,
            IsMuted = status.IsMuted
        };
    }
    
    private class PlayerVoiceInfo
    {
        public string EndPoint { get; set; }
        public string PlayerName { get; set; }
        public Texture2D AvatarTexture { get; set; }
        public bool IsSpeaking { get; set; }
        public bool IsMuted { get; set; }
    }
}
