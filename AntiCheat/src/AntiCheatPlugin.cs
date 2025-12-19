using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace GorillaAntiCheat
{
    /// <summary>
    /// Advanced Anti-Cheat System for Gorilla Tag
    /// Blocks detection and reporting of mods to protect your gameplay.
    /// Press F1 for the control panel.
    /// </summary>
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class AntiCheatPlugin : BaseUnityPlugin
    {
        public static AntiCheatPlugin Instance { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }
        
        // Protection Status
        public static bool ProtectionEnabled = true;
        public static bool BlockReports = true;
        public static bool BlockRPCTracking = true;
        public static bool BlockNetworkEvents = true;
        public static bool SpoofPlayerData = true;
        public static bool StealthMode = true;
        public static bool AntiKick = true;
        
        // Stats
        public static int BlockedReports = 0;
        public static int BlockedRPCAlerts = 0;
        public static int BlockedNetEvents = 0;
        public static int BlockedKickAttempts = 0;
        
        // Logging
        public static List<string> ActivityLog = new List<string>();
        private const int MaxLogEntries = 50;
        
        // Menu state
        private bool showMenu = false;
        private Vector2 scrollPosition = Vector2.zero;
        private int currentTab = 0;
        private string[] tabNames = { "Protection", "Stats", "Log", "Settings" };
        
        // Menu styling
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toggleStyle;
        private GUIStyle logStyle;
        private bool stylesInitialized = false;
        
        // Delayed patching
        private static bool delayedPatchesApplied = false;
        private static bool isInRoom = false;
        
        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} Initializing...");
            
            try
            {
                HarmonyInstance = new Harmony(PluginInfo.GUID);
                
                // Apply all patches EXCEPT the delayed ones
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                
                // Now unpatch the ReportMute method - we'll patch it later
                UnpatchDelayedMethods();
                
                AddLog("[SYSTEM] Anti-Cheat initialized successfully!");
                AddLog($"[SYSTEM] Base patches applied, waiting for room join...");
                
                Logger.LogInfo($"{PluginInfo.Name} loaded successfully!");
                
                // Hook into room join events
                NetworkSystem.Instance.OnJoinedRoomEvent.Add(OnJoinedRoom);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize: {ex}");
                AddLog($"[ERROR] Initialization failed: {ex.Message}");
            }
        }

        private void OnJoinedRoom()
        {
            if (delayedPatchesApplied)
                return;

            try
            {
                // Only apply patches if we're in an actual multiplayer lobby
                // NOT when connecting to Mothership or in the default room
                if (!IsInActualLobby())
                {
                    AddLog("[SYSTEM] Room joined but not a lobby - waiting...");
                    return;
                }
                
                ApplyDelayedPatches();
                delayedPatchesApplied = true;
                isInRoom = true;
                AddLog("[SYSTEM] Public lobby joined - delayed patches applied!");
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] Failed to apply delayed patches: {ex.Message}");
            }
        }

        private bool IsInActualLobby()
        {
            try
            {
                // Check if we're online and in a room
                if (!PhotonNetwork.InRoom)
                    return false;

                Room currentRoom = PhotonNetwork.CurrentRoom;
                if (currentRoom == null)
                    return false;

                // Check if this is a public/visible room (not Mothership etc)
                // Real lobbies are visible and have actual room names
                bool isPublicRoom = currentRoom.IsVisible;
                bool hasRoomName = !string.IsNullOrEmpty(currentRoom.Name);
                
                // Additional check: room has at least 1 player (us) and can have more
                bool hasPlayers = currentRoom.PlayerCount >= 1;
                bool canHaveOthers = currentRoom.MaxPlayers > 1;

                AddLog($"[DEBUG] Room check: visible={isPublicRoom}, name={hasRoomName}, players={currentRoom.PlayerCount}/{currentRoom.MaxPlayers}");

                return isPublicRoom && hasRoomName && hasPlayers && canHaveOthers;
            }
            catch
            {
                return false;
            }
        }

        private void UnpatchDelayedMethods()
        {
            try
            {
                // Unpatch GorillaScoreboardTotalUpdater.ReportMute
                var reportMuteMethod = AccessTools.Method(typeof(GorillaScoreboardTotalUpdater), "ReportMute");
                if (reportMuteMethod != null)
                {
                    HarmonyInstance.Unpatch(reportMuteMethod, HarmonyPatchType.All, PluginInfo.GUID);
                    AddLog("[SYSTEM] Unpatched ReportMute (will apply after room join)");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[WARN] Could not unpatch delayed methods: {ex.Message}");
            }
        }

        private void ApplyDelayedPatches()
        {
            try
            {
                // Manually patch ReportMute
                var reportMuteMethod = AccessTools.Method(typeof(GorillaScoreboardTotalUpdater), "ReportMute");
                
                // Find the patch method via reflection since it's in a nested class
                var patchAssembly = Assembly.GetExecutingAssembly();
                var scoreboardUpdaterPatchesType = patchAssembly.GetType("GorillaAntiCheat.Patches.ScoreboardUpdaterPatches");
                
                if (scoreboardUpdaterPatchesType != null && reportMuteMethod != null)
                {
                    var reportMutePrefix = AccessTools.Method(scoreboardUpdaterPatchesType, "ReportMutePrefix");
                    
                    if (reportMutePrefix != null)
                    {
                        HarmonyInstance.Patch(reportMuteMethod, prefix: new HarmonyMethod(reportMutePrefix));
                        AddLog("[SYSTEM] Applied ReportMute patch");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] Failed to apply delayed patches: {ex.Message}");
            }
        }

        private void Update()
        {
            // F1 to toggle menu
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                showMenu = !showMenu;
                
                if (showMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
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
            DrawControlPanel();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            // Box style - dark glassmorphism
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(2, 2, new Color(0.05f, 0.05f, 0.1f, 0.95f));
            boxStyle.border = new RectOffset(10, 10, 10, 10);
            
            // Header style
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.2f, 1f, 0.4f); // Bright green
            
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
            buttonStyle.active.background = MakeTexture(2, 2, new Color(0.2f, 1f, 0.4f, 1f));
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(0.2f, 1f, 0.4f);
            
            // Toggle button style
            toggleStyle = new GUIStyle(buttonStyle);
            
            // Log style
            logStyle = new GUIStyle(GUI.skin.label);
            logStyle.fontSize = 12;
            logStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            logStyle.wordWrap = true;
            logStyle.richText = true;
            
            stylesInitialized = true;
        }

        private void DrawControlPanel()
        {
            // Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.7f) scale = 0.7f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float width = 600;
            float height = 550;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            float x = (screenW - width) / 2;
            float y = (screenH - height) / 2;
            
            GUILayout.BeginArea(new Rect(x, y, width, height), boxStyle);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 20, 20) });
            
            // Header
            GUILayout.Label("🛡️ Gorilla Anti-Cheat", headerStyle);
            GUILayout.Space(5);
            
            // Status indicator
            string statusText = ProtectionEnabled 
                ? "<color=#00ff66>● PROTECTION ACTIVE</color>" 
                : "<color=#ff4444>● PROTECTION DISABLED</color>";
            GUIStyle statusStyle = new GUIStyle(labelStyle);
            statusStyle.alignment = TextAnchor.MiddleCenter;
            statusStyle.fontSize = 14;
            GUILayout.Label(statusText, statusStyle);
            
            GUILayout.Space(15);
            
            // Tab buttons
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUIStyle tabStyle = new GUIStyle(buttonStyle);
                if (i == currentTab)
                {
                    tabStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 1f, 0.4f, 0.3f));
                    tabStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);
                }
                
                if (GUILayout.Button(tabNames[i], tabStyle))
                {
                    currentTab = i;
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            // Separator
            GUI.Box(GUILayoutUtility.GetRect(width - 40, 2), "", new GUIStyle { normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.3f, 0.5f)) } });
            
            GUILayout.Space(15);
            
            // Tab content
            switch (currentTab)
            {
                case 0: DrawProtectionTab(); break;
                case 1: DrawStatsTab(); break;
                case 2: DrawLogTab(); break;
                case 3: DrawSettingsTab(); break;
            }
            
            GUILayout.FlexibleSpace();
            
            // Footer
            GUILayout.Space(10);
            GUIStyle footerStyle = new GUIStyle(labelStyle);
            footerStyle.fontSize = 11;
            footerStyle.alignment = TextAnchor.MiddleCenter;
            footerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label("Press F1 to close | v" + PluginInfo.Version, footerStyle);
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }

        private void DrawProtectionTab()
        {
            GUILayout.Label("<b>Protection Modules</b>", labelStyle);
            GUILayout.Space(10);
            
            // Master toggle
            ProtectionEnabled = DrawToggle("Master Protection", ProtectionEnabled, "Enable/disable all protection");
            
            GUILayout.Space(10);
            
            // Individual toggles
            GUI.enabled = ProtectionEnabled;
            
            BlockReports = DrawToggle("Block Reports", BlockReports, 
                "Prevents GorillaNot from sending reports about you");
            
            BlockRPCTracking = DrawToggle("Block RPC Tracking", BlockRPCTracking, 
                "Prevents RPC call counting that could flag you");
            
            BlockNetworkEvents = DrawToggle("Block Detection Events", BlockNetworkEvents, 
                "Blocks network events used for cheat detection");
            
            SpoofPlayerData = DrawToggle("Spoof Player Data", SpoofPlayerData, 
                "Makes your data appear normal to anti-cheat");
            
            StealthMode = DrawToggle("Stealth Mode", StealthMode, 
                "Hides mod signatures from detection");
            
            AntiKick = DrawToggle("Anti-Kick", AntiKick, 
                "Prevents kick attempts from other players");
            
            GUI.enabled = true;
        }

        private void DrawStatsTab()
        {
            GUILayout.Label("<b>Protection Statistics</b>", labelStyle);
            GUILayout.Space(15);
            
            DrawStatLine("Reports Blocked", BlockedReports, new Color(1f, 0.4f, 0.4f));
            DrawStatLine("RPC Alerts Blocked", BlockedRPCAlerts, new Color(1f, 0.8f, 0.2f));
            DrawStatLine("Network Events Blocked", BlockedNetEvents, new Color(0.4f, 0.8f, 1f));
            DrawStatLine("Kick Attempts Blocked", BlockedKickAttempts, new Color(0.8f, 0.4f, 1f));
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("Reset Statistics", buttonStyle))
            {
                BlockedReports = 0;
                BlockedRPCAlerts = 0;
                BlockedNetEvents = 0;
                BlockedKickAttempts = 0;
                AddLog("[STATS] Statistics reset");
            }
        }

        private void DrawLogTab()
        {
            GUILayout.Label("<b>Activity Log</b>", labelStyle);
            GUILayout.Space(10);
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            
            for (int i = ActivityLog.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(ActivityLog[i], logStyle);
            }
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Clear Log", buttonStyle))
            {
                ActivityLog.Clear();
                AddLog("[LOG] Log cleared");
            }
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label("<b>Advanced Settings</b>", labelStyle);
            GUILayout.Space(15);
            
            GUILayout.Label("Protection Level:", labelStyle);
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Minimal", buttonStyle))
            {
                SetProtectionLevel(1);
            }
            if (GUILayout.Button("Standard", buttonStyle))
            {
                SetProtectionLevel(2);
            }
            if (GUILayout.Button("Maximum", buttonStyle))
            {
                SetProtectionLevel(3);
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.Space(20);
            
            GUILayout.Label("<b>Quick Actions</b>", labelStyle);
            GUILayout.Space(10);
            
            if (GUILayout.Button("🔄 Reapply All Patches", buttonStyle))
            {
                try
                {
                    HarmonyInstance.UnpatchSelf();
                    HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                    AddLog("[SYSTEM] All patches reapplied successfully");
                }
                catch (Exception ex)
                {
                    AddLog($"[ERROR] Failed to reapply patches: {ex.Message}");
                }
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("🧹 Clear Reported Players List", buttonStyle))
            {
                try
                {
                    if (GorillaNot.instance != null)
                    {
                        // Use reflection to access internal field
                        var field = AccessTools.Field(typeof(GorillaNot), "reportedPlayers");
                        if (field != null)
                        {
                            var list = field.GetValue(GorillaNot.instance) as System.Collections.IList;
                            list?.Clear();
                            AddLog("[SYSTEM] Cleared reported players list");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[ERROR] Failed to clear list: {ex.Message}");
                }
            }
        }

        private bool DrawToggle(string label, bool value, string tooltip)
        {
            GUILayout.BeginHorizontal();
            
            GUIStyle toggleLabelStyle = new GUIStyle(labelStyle);
            toggleLabelStyle.normal.textColor = value ? new Color(0.2f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            
            GUILayout.Label(label, toggleLabelStyle, GUILayout.Width(220));
            
            GUIStyle btnStyle = new GUIStyle(buttonStyle);
            btnStyle.normal.background = value 
                ? MakeTexture(2, 2, new Color(0.2f, 0.8f, 0.4f, 0.8f))
                : MakeTexture(2, 2, new Color(0.4f, 0.2f, 0.2f, 0.8f));
            btnStyle.normal.textColor = Color.white;
            
            if (GUILayout.Button(value ? "ON" : "OFF", btnStyle, GUILayout.Width(60)))
            {
                value = !value;
                AddLog($"[CONFIG] {label} set to {(value ? "ON" : "OFF")}");
            }
            
            GUILayout.EndHorizontal();
            
            // Tooltip
            GUIStyle tipStyle = new GUIStyle(labelStyle);
            tipStyle.fontSize = 11;
            tipStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label("   " + tooltip, tipStyle);
            GUILayout.Space(5);
            
            return value;
        }

        private void DrawStatLine(string label, int value, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", labelStyle, GUILayout.Width(200));
            
            GUIStyle valueStyle = new GUIStyle(labelStyle);
            valueStyle.fontStyle = FontStyle.Bold;
            valueStyle.normal.textColor = color;
            
            GUILayout.Label(value.ToString(), valueStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void SetProtectionLevel(int level)
        {
            switch (level)
            {
                case 1: // Minimal
                    BlockReports = true;
                    BlockRPCTracking = false;
                    BlockNetworkEvents = false;
                    SpoofPlayerData = false;
                    StealthMode = false;
                    AntiKick = false;
                    AddLog("[CONFIG] Protection level: MINIMAL");
                    break;
                    
                case 2: // Standard
                    BlockReports = true;
                    BlockRPCTracking = true;
                    BlockNetworkEvents = true;
                    SpoofPlayerData = false;
                    StealthMode = true;
                    AntiKick = false;
                    AddLog("[CONFIG] Protection level: STANDARD");
                    break;
                    
                case 3: // Maximum
                    BlockReports = true;
                    BlockRPCTracking = true;
                    BlockNetworkEvents = true;
                    SpoofPlayerData = true;
                    StealthMode = true;
                    AntiKick = true;
                    AddLog("[CONFIG] Protection level: MAXIMUM");
                    break;
            }
        }

        public static void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            
            ActivityLog.Add(logEntry);
            
            // Keep log size manageable
            while (ActivityLog.Count > MaxLogEntries)
            {
                ActivityLog.RemoveAt(0);
            }
            
            // Also log to console
            if (Instance != null)
            {
                Instance.Logger.LogInfo(message);
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

        private void OnDestroy()
        {
            HarmonyInstance?.UnpatchSelf();
        }
    }

    /// <summary>
    /// Plugin information
    /// </summary>
    public static class PluginInfo
    {
        public const string GUID = "com.lane.gorillaanticheat";
        public const string Name = "Gorilla Anti-Cheat";
        public const string Version = "2.0.0";
    }
}
