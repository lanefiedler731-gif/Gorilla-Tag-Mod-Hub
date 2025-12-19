using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
using GorillaNetworking;

namespace PlayerCustomizer
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class PlayerCustomizerPlugin : BaseUnityPlugin
    {
        public static PlayerCustomizerPlugin Instance { get; private set; }
        
        // UI State
        private bool showMenu = false;
        private int selectedTab = 0;
        private string[] tabNames = { "Name", "Room", "Color" };
        
        // Input fields
        private string newDisplayName = "";
        private string colorRed = "255";
        private string colorGreen = "255";
        private string colorBlue = "255";
        
        // GUI Styles
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle tabButtonStyle;
        private bool stylesInitialized = false;
        
        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} Initializing...");
            
            try
            {
                var harmony = new Harmony(PluginInfo.GUID);
                harmony.PatchAll();
                Logger.LogInfo($"{PluginInfo.Name} loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize: {ex}");
            }
        }
        
        private void Update()
        {
            // F3 to toggle menu
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                showMenu = !showMenu;
                
                if (showMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    
                    // Load current values
                    if (PhotonNetwork.LocalPlayer != null)
                    {
                        newDisplayName = PhotonNetwork.LocalPlayer.NickName;
                    }
                    
                    // Get current color
                    if (GorillaTagger.Instance != null)
                    {
                        Color currentColor = GorillaTagger.Instance.offlineVRRig.playerColor;
                        colorRed = ((int)(currentColor.r * 255)).ToString();
                        colorGreen = ((int)(currentColor.g * 255)).ToString();
                        colorBlue = ((int)(currentColor.b * 255)).ToString();
                    }
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
        
        private void OnGUI()
        {
            if (!showMenu) return;
            
            InitializeStyles();
            DrawCustomizerMenu();
        }
        
        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            // Box style
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(2, 2, new Color(0.05f, 0.05f, 0.1f, 0.95f));
            boxStyle.border = new RectOffset(10, 10, 10, 10);
            boxStyle.padding = new RectOffset(20, 20, 20, 20);
            
            // Header style
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);
            
            // Label style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;
            labelStyle.richText = true;
            
            // Button style
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 16;
            buttonStyle.padding = new RectOffset(15, 15, 10, 10);
            buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.15f, 1f));
            buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.15f, 0.15f, 0.25f, 1f));
            buttonStyle.active.background = MakeTexture(2, 2, new Color(0.2f, 0.8f, 1f, 1f));
            buttonStyle.normal.textColor = Color.white;
            
            // Text field style
            textFieldStyle = new GUIStyle(GUI.skin.textField);
            textFieldStyle.fontSize = 16;
            textFieldStyle.padding = new RectOffset(10, 10, 8, 8);
            textFieldStyle.normal.background = MakeTexture(2, 2, new Color(0.15f, 0.15f, 0.2f, 1f));
            textFieldStyle.normal.textColor = Color.white;
            textFieldStyle.focused.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.3f, 1f));
            textFieldStyle.focused.textColor = Color.white;
            
            // Tab button style
            tabButtonStyle = new GUIStyle(buttonStyle);
            
            stylesInitialized = true;
        }
        
        private void DrawCustomizerMenu()
        {
            // Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.7f) scale = 0.7f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float width = 500;
            float height = 450;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            float x = (screenW - width) / 2;
            float y = (screenH - height) / 2;
            
            GUILayout.BeginArea(new Rect(x, y, width, height), boxStyle);
            
            // Header
            GUILayout.Label("Player Customizer", headerStyle);
            GUILayout.Space(10);
            
            // Tab buttons
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUIStyle currentTabStyle = new GUIStyle(tabButtonStyle);
                if (i == selectedTab)
                {
                    currentTabStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.8f, 1f, 0.3f));
                    currentTabStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);
                }
                
                if (GUILayout.Button(tabNames[i], currentTabStyle))
                {
                    selectedTab = i;
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            // Tab content
            switch (selectedTab)
            {
                case 0: DrawNameTab(); break;
                case 1: DrawRoomTab(); break;
                case 2: DrawColorTab(); break;
            }
            
            GUILayout.FlexibleSpace();
            
            // Footer
            GUIStyle footerStyle = new GUIStyle(labelStyle);
            footerStyle.fontSize = 11;
            footerStyle.alignment = TextAnchor.MiddleCenter;
            footerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label("Press F3 to close | v" + PluginInfo.Version, footerStyle);
            
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }
        
        private void DrawNameTab()
        {
            GUILayout.Label("<b>Change Display Name</b>", labelStyle);
            GUILayout.Space(10);
            
            GUILayout.Label("Current Name:", labelStyle);
            if (PhotonNetwork.LocalPlayer != null)
            {
                GUILayout.Label($"<color=#88FF88>{PhotonNetwork.LocalPlayer.NickName}</color>", labelStyle);
            }
            else
            {
                GUILayout.Label("<color=#888888>Not connected</color>", labelStyle);
            }
            
            GUILayout.Space(15);
            
            GUILayout.Label("New Display Name:", labelStyle);
            newDisplayName = GUILayout.TextField(newDisplayName, 20, textFieldStyle, GUILayout.Height(35));
            
            GUILayout.Space(15);
            
            if (GUILayout.Button("Apply Name", buttonStyle, GUILayout.Height(40)))
            {
                if (!string.IsNullOrWhiteSpace(newDisplayName) && NetworkSystem.Instance != null)
                {
                    NetworkSystem.Instance.SetMyNickName(newDisplayName);
                    Logger.LogInfo($"Display name changed to: {newDisplayName}");
                }
            }
            
            GUILayout.Space(10);
            
            GUIStyle noteStyle = new GUIStyle(labelStyle);
            noteStyle.fontSize = 12;
            noteStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            noteStyle.wordWrap = true;
            GUILayout.Label("Note: Name changes may require reconnecting to take full effect.", noteStyle);
        }
        
        private void DrawRoomTab()
        {
            GUILayout.Label("<b>Room Controls</b>", labelStyle);
            GUILayout.Space(10);
            
            GUILayout.Label("Current Room:", labelStyle);
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
            {
                GUILayout.Label($"<color=#88FF88>{PhotonNetwork.CurrentRoom.Name}</color>", labelStyle);
                GUILayout.Space(10);
                GUILayout.Label($"Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}", labelStyle);
            }
            else
            {
                GUILayout.Label("<color=#FF8888>Not in a room</color>", labelStyle);
            }
            
            GUILayout.Space(20);
            
            if (PhotonNetwork.InRoom)
            {
                if (GUILayout.Button("Leave Current Room", buttonStyle, GUILayout.Height(40)))
                {
                    if (NetworkSystem.Instance != null)
                    {
                        NetworkSystem.Instance.ReturnToSinglePlayer();
                        Logger.LogInfo("Leaving current room");
                    }
                }
            }
            else
            {
                GUIStyle disabledStyle = new GUIStyle(buttonStyle);
                disabledStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.enabled = false;
                GUILayout.Button("Leave Current Room", disabledStyle, GUILayout.Height(40));
                GUI.enabled = true;
            }
        }
        
        private void DrawColorTab()
        {
            GUILayout.Label("<b>Change Player Color</b>", labelStyle);
            GUILayout.Space(10);
            
            // Current color preview
            GUILayout.Label("Current Color:", labelStyle);
            if (GorillaTagger.Instance != null)
            {
                Color currentColor = GorillaTagger.Instance.offlineVRRig.playerColor;
                GUILayout.Box("", GUILayout.Height(30), GUILayout.Width(100));
                Rect colorRect = GUILayoutUtility.GetLastRect();
                GUI.DrawTexture(colorRect, MakeTexture(2, 2, currentColor));
            }
            
            GUILayout.Space(15);
            
            // RGB inputs
            GUILayout.BeginHorizontal();
            GUILayout.Label("R:", labelStyle, GUILayout.Width(30));
            colorRed = GUILayout.TextField(colorRed, 3, textFieldStyle, GUILayout.Height(35));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("G:", labelStyle, GUILayout.Width(30));
            colorGreen = GUILayout.TextField(colorGreen, 3, textFieldStyle, GUILayout.Height(35));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("B:", labelStyle, GUILayout.Width(30));
            colorBlue = GUILayout.TextField(colorBlue, 3, textFieldStyle, GUILayout.Height(35));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            if (GUILayout.Button("Apply Color", buttonStyle, GUILayout.Height(40)))
            {
                ApplyColor();
            }
            
            GUILayout.Space(10);
            
            // Quick color presets
            GUILayout.Label("Presets:", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Red", buttonStyle)) { colorRed = "255"; colorGreen = "0"; colorBlue = "0"; ApplyColor(); }
            if (GUILayout.Button("Blue", buttonStyle)) { colorRed = "0"; colorGreen = "0"; colorBlue = "255"; ApplyColor(); }
            if (GUILayout.Button("Green", buttonStyle)) { colorRed = "0"; colorGreen = "255"; colorBlue = "0"; ApplyColor(); }
            GUILayout.EndHorizontal();
        }
        
        private void ApplyColor()
        {
            try
            {
                int r = Mathf.Clamp(int.Parse(colorRed), 0, 255);
                int g = Mathf.Clamp(int.Parse(colorGreen), 0, 255);
                int b = Mathf.Clamp(int.Parse(colorBlue), 0, 255);
                
                Color newColor = new Color(r / 255f, g / 255f, b / 255f);
                
                if (GorillaTagger.Instance != null)
                {
                    GorillaTagger.Instance.offlineVRRig.playerColor = newColor;
                    GorillaTagger.Instance.UpdateColor(newColor.r, newColor.g, newColor.b);
                    Logger.LogInfo($"Color changed to: R:{r} G:{g} B:{b}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply color: {ex}");
            }
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
    
    public static class PluginInfo
    {
        public const string GUID = "com.lane.playercustomizer";
        public const string Name = "Player Customizer";
        public const string Version = "1.0.0";
    }
}
