using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromDuckovCoopMod.Main.ModManager
{
    [Serializable]
    public class WorkshopModInfo
    {
        public string ModId;
        public string ModName;
        public string Description;
        public string Author;
        public string Version;
        public List<string> Tags;
        public string PreviewImagePath;
        public string ModPath;
        public bool IsEnabled;
        public bool IsCompatible;
        public Texture2D PreviewTexture;
        
        public WorkshopModInfo()
        {
            Tags = new List<string>();
            IsEnabled = false;
            IsCompatible = false;
        }
    }
}
