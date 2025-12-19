using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using PlayFab;
using GorillaNetworking;
using Steamworks;

namespace PlayerInfoMod
{
    /// <summary>
    /// Advanced Player Information Mod for Gorilla Tag
    /// Shows detailed info about all players including PlayFab ID, Photon data, Steam ID, and more
    /// Press F5 to toggle the player list, click on a player to see ALL their info
    /// </summary>
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class PlayerInfoPlugin : BaseUnityPlugin
    {
        public static PlayerInfoPlugin Instance { get; private set; }
        
        // UI State
        private bool showPlayerList = false;
        private bool showDetailedInfo = false;
        private NetPlayer selectedPlayer = null;
        private Vector2 playerListScroll = Vector2.zero;
        private Vector2 detailedInfoScroll = Vector2.zero;
        
        // Success message
        private string successMessage = "";
        private float successMessageTime = 0f;
        private const float SUCCESS_MESSAGE_DURATION = 3f;
        
        // Cached player info
        private Dictionary<string, PlayerData> playerDataCache = new Dictionary<string, PlayerData>();
        
        // GUI Styles
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle playerButtonStyle;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle dataLabelStyle;
        private bool stylesInitialized = false;
        
        private void Awake()
        {
            Instance = this;
            Logger.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} Initializing...");
            
            try
            {
                var harmony = new Harmony(PluginInfo.GUID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Logger.LogInfo($"{PluginInfo.Name} loaded successfully!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize: {ex}");
            }
        }
        
        private void Update()
        {
            // F5 to toggle player list
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                showPlayerList = !showPlayerList;
                
                if (showPlayerList)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    RefreshPlayerData();
                }
                else
                {
                    showDetailedInfo = false;
                    selectedPlayer = null;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            
            // ESC to close detailed view
            if (showDetailedInfo && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                showDetailedInfo = false;
                selectedPlayer = null;
            }
            
            // Update success message timer
            if (Time.time > successMessageTime + SUCCESS_MESSAGE_DURATION)
            {
                successMessage = "";
            }
        }
        
        private void OnGUI()
        {
            if (!showPlayerList) return;
            
            InitializeStyles();
            
            if (showDetailedInfo && selectedPlayer != null)
            {
                DrawDetailedPlayerInfo();
            }
            else
            {
                DrawPlayerList();
            }
            
            // Draw success message overlay if active
            if (!string.IsNullOrEmpty(successMessage))
            {
                DrawSuccessMessage();
            }
        }
        
        private void InitializeStyles()
        {
            if (stylesInitialized) return;
            
            // Box style - dark glassmorphism
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(2, 2, new Color(0.05f, 0.05f, 0.1f, 0.95f));
            boxStyle.border = new RectOffset(10, 10, 10, 10);
            boxStyle.padding = new RectOffset(20, 20, 20, 20);
            
            // Header style
            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.2f, 0.8f, 1f); // Cyan
            
            // Section header style
            sectionHeaderStyle = new GUIStyle(GUI.skin.label);
            sectionHeaderStyle.fontSize = 20;
            sectionHeaderStyle.fontStyle = FontStyle.Bold;
            sectionHeaderStyle.normal.textColor = new Color(1f, 0.6f, 0.2f); // Orange
            sectionHeaderStyle.margin = new RectOffset(0, 0, 10, 5);
            
            // Label style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;
            labelStyle.richText = true;
            
            // Data label style (for key-value pairs)
            dataLabelStyle = new GUIStyle(GUI.skin.label);
            dataLabelStyle.fontSize = 14;
            dataLabelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            dataLabelStyle.richText = true;
            dataLabelStyle.wordWrap = true;
            
            // Button style
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 16;
            buttonStyle.padding = new RectOffset(15, 15, 10, 10);
            buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.15f, 1f));
            buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.15f, 0.15f, 0.25f, 1f));
            buttonStyle.active.background = MakeTexture(2, 2, new Color(0.2f, 0.8f, 1f, 1f));
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(0.2f, 0.8f, 1f);
            
            // Player button style
            playerButtonStyle = new GUIStyle(buttonStyle);
            playerButtonStyle.alignment = TextAnchor.MiddleLeft;
            playerButtonStyle.fontSize = 14;
            
            stylesInitialized = true;
        }
        
        private void DrawPlayerList()
        {
            // Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.7f) scale = 0.7f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float width = 500;
            float height = 700;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            float x = (screenW - width) / 2;
            float y = (screenH - height) / 2;
            
            GUILayout.BeginArea(new Rect(x, y, width, height), boxStyle);
            
            // Header
            GUILayout.Label("Player Info Browser", headerStyle);
            GUILayout.Space(10);
            
            // Info text
            GUIStyle infoStyle = new GUIStyle(labelStyle);
            infoStyle.fontSize = 12;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label("Click on a player to view ALL their information", infoStyle);
            
            GUILayout.Space(15);
            
            // Separator
            GUI.Box(new Rect(20, 100, width - 40, 2), "", new GUIStyle { normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.3f, 0.5f)) } });
            
            GUILayout.Space(5);
            
            // Player count
            int playerCount = 0;
            if (NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom)
            {
                playerCount = NetworkSystem.Instance.AllNetPlayers.Length;
            }
            
            GUILayout.Label($"<b>Players in Room:</b> {playerCount}", labelStyle);
            GUILayout.Space(10);
            
            // Player list
            playerListScroll = GUILayout.BeginScrollView(playerListScroll, GUILayout.Height(500));
            
            if (NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom)
            {
                NetPlayer[] players = NetworkSystem.Instance.AllNetPlayers;
                
                foreach (NetPlayer player in players)
                {
                    if (player == null || !player.IsValid) continue;
                    
                    // Get player data
                    string playerId = player.UserId;
                    PlayerData data = GetOrCreatePlayerData(player);
                    
                    // Color code based on if it's local player
                    Color buttonColor = player.IsLocal ? new Color(0.2f, 0.8f, 0.4f, 0.3f) : new Color(0.1f, 0.1f, 0.15f, 1f);
                    GUIStyle customButtonStyle = new GUIStyle(playerButtonStyle);
                    customButtonStyle.normal.background = MakeTexture(2, 2, buttonColor);
                    
                    string playerLabel = player.IsLocal ? "[YOU] " : "";
                    playerLabel += $"{player.NickName}";
                    if (player.IsMasterClient) playerLabel += " [HOST]";
                    
                    if (GUILayout.Button(playerLabel, customButtonStyle, GUILayout.Height(35)))
                    {
                        selectedPlayer = player;
                        showDetailedInfo = true;
                        RefreshSelectedPlayerData();
                    }
                    
                    GUILayout.Space(5);
                }
            }
            else
            {
                GUILayout.Label("Not connected to a room", labelStyle);
            }
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // Footer
            GUIStyle footerStyle = new GUIStyle(labelStyle);
            footerStyle.fontSize = 11;
            footerStyle.alignment = TextAnchor.MiddleCenter;
            footerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label("Press F5 to close | ESC to go back | v" + PluginInfo.Version, footerStyle);
            
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }
        
        private void DrawDetailedPlayerInfo()
        {
            if (selectedPlayer == null || !selectedPlayer.IsValid)
            {
                showDetailedInfo = false;
                return;
            }
            
            // Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.7f) scale = 0.7f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float width = 700;
            float height = 800;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            float x = (screenW - width) / 2;
            float y = (screenH - height) / 2;
            
            GUILayout.BeginArea(new Rect(x, y, width, height), boxStyle);
            
            // Header with player name
            GUILayout.Label($"Player Info: {selectedPlayer.NickName}", headerStyle);
            GUILayout.Space(15);
            
            // Back button
            if (GUILayout.Button("← Back to Player List", buttonStyle, GUILayout.Height(30)))
            {
                showDetailedInfo = false;
                selectedPlayer = null;
            }
            
            GUILayout.Space(15);
            
            // Scrollable info area
            detailedInfoScroll = GUILayout.BeginScrollView(detailedInfoScroll, GUILayout.Height(600));
            
            PlayerData data = GetOrCreatePlayerData(selectedPlayer);
            
            // === BASIC INFO ===
            DrawSection("Basic Information");
            DrawInfoLine("Nickname", selectedPlayer.NickName);
            DrawInfoLine("Display Name", selectedPlayer.SanitizedNickName);
            DrawInfoLine("Is Local Player", selectedPlayer.IsLocal ? "YES" : "NO");
            DrawInfoLine("Is Master Client", selectedPlayer.IsMasterClient ? "YES" : "NO");
            DrawInfoLine("Actor Number", selectedPlayer.ActorNumber.ToString());
            DrawInfoLine("In Room", selectedPlayer.InRoom ? "YES" : "NO");
            DrawInfoLine("Joined Time", selectedPlayer.JoinedTime.ToString("F2") + "s");
            
            GUILayout.Space(10);
            
            // === PHOTON INFO ===
            DrawSection("Photon Network Data");
            DrawInfoLine("User ID", selectedPlayer.UserId);
            
            // Get Photon Player reference
            Player photonPlayer = GetPhotonPlayer(selectedPlayer);
            if (photonPlayer != null)
            {
                DrawInfoLine("Actor Number", photonPlayer.ActorNumber.ToString());
                DrawInfoLine("Is Inactive", photonPlayer.IsInactive ? "YES" : "NO");
                DrawInfoLine("Is Master Client", photonPlayer.IsMasterClient ? "YES" : "NO");
                DrawInfoLine("Nickname", photonPlayer.NickName);
                DrawInfoLine("User ID", photonPlayer.UserId);
                
                // Custom properties
                if (photonPlayer.CustomProperties != null && photonPlayer.CustomProperties.Count > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("<b>Custom Properties:</b>", dataLabelStyle);
                    foreach (var prop in photonPlayer.CustomProperties)
                    {
                        DrawInfoLine($"  • {prop.Key}", prop.Value?.ToString() ?? "null");
                    }
                }
            }
            else
            {
                DrawInfoLine("Photon Player", "Not available");
            }
            
            GUILayout.Space(10);
            
            // === PLAYFAB INFO ===
            DrawSection("PlayFab Data");
            DrawInfoLine("PlayFab ID", data.PlayFabId ?? "Not available");
            DrawInfoLine("Title ID", PlayFabSettings.TitleId ?? "Not set");
            
            if (selectedPlayer.IsLocal && PlayFabAuthenticator.instance != null)
            {
                try
                {
                    string localPlayFabId = PlayFabAuthenticator.instance.GetPlayFabPlayerId();
                    if (!string.IsNullOrEmpty(localPlayFabId))
                    {
                        DrawInfoLine("Local PlayFab ID", localPlayFabId);
                    }
                }
                catch { }
            }
            
            GUILayout.Space(10);
            
            // === STEAM INFO ===
            DrawSection("Steam Data");
            if (selectedPlayer.IsLocal)
            {
                try
                {
                    CSteamID steamId = SteamUser.GetSteamID();
                    DrawInfoLine("Steam ID", steamId.ToString());
                    DrawInfoLine("Steam ID (64-bit)", steamId.m_SteamID.ToString());
                    DrawInfoLine("Account ID", steamId.GetAccountID().ToString());
                    
                    string personaName = SteamFriends.GetPersonaName();
                    DrawInfoLine("Steam Name", personaName);
                    
                    EPersonaState state = SteamFriends.GetPersonaState();
                    DrawInfoLine("Persona State", state.ToString());
                    
                    int playerLevel = SteamUser.GetPlayerSteamLevel();
                    DrawInfoLine("Steam Level", playerLevel.ToString());
                }
                catch (Exception ex)
                {
                    DrawInfoLine("Steam Data", $"Error: {ex.Message}");
                }
            }
            else
            {
                DrawInfoLine("Steam ID", "Only available for local player");
            }
            
            GUILayout.Space(10);
            
            // === VOICE INFO ===
            DrawSection("Voice & Audio");
            RigContainer rigContainer = GetRigContainer(selectedPlayer);
            if (rigContainer != null)
            {
                DrawInfoLine("Is Muted", rigContainer.Muted ? "YES" : "NO");
                DrawInfoLine("Force Mute", rigContainer.ForceMute ? "YES" : "NO");
                DrawInfoLine("Has Manual Mute", rigContainer.hasManualMute ? "YES" : "NO");
                DrawInfoLine("Auto Muted", rigContainer.GetIsPlayerAutoMuted() ? "YES" : "NO");
                DrawInfoLine("Chat Quality", rigContainer.playerChatQuality.ToString());
                DrawInfoLine("Initialized", rigContainer.Initialized ? "YES" : "NO");
                
                if (rigContainer.Voice != null)
                {
                    DrawInfoLine("Voice View Exists", "YES");
                    DrawInfoLine("Speaker Enabled", rigContainer.Voice.SpeakerInUse?.enabled.ToString() ?? "N/A");
                }
            }
            else
            {
                DrawInfoLine("Voice Data", "Not available");
            }
            
            GUILayout.Space(10);
            
            // === VR RIG INFO ===
            DrawSection("VR Rig Data");
            VRRig rig = GetVRRig(selectedPlayer);
            if (rig != null)
            {
                DrawInfoLine("Is Offline Rig", rig.isOfflineVRRig ? "YES" : "NO");
                DrawInfoLine("Is My Player", rig.isMyPlayer ? "YES" : "NO");
                DrawInfoLine("Show Name", rig.showName ? "YES" : "NO");
                DrawInfoLine("Player Name", rig.playerNameVisible);
                DrawInfoLine("Is Muted", rig.muted ? "YES" : "NO");
                
                // Color
                Color playerColor = rig.playerColor;
                DrawInfoLine("Player Color", $"R:{playerColor.r:F2} G:{playerColor.g:F2} B:{playerColor.b:F2}");
                
                // Position
                Vector3 pos = rig.transform.position;
                DrawInfoLine("Position", $"X:{pos.x:F2} Y:{pos.y:F2} Z:{pos.z:F2}");
                
                // Network view exists check
                try
                {
                    var netView = typeof(VRRig).GetField("netView", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(rig);
                    if (netView != null)
                    {
                        DrawInfoLine("Has Network View", "YES");
                    }
                }
                catch { }
                
                // Cosmetics
                if (rig.cosmeticSet != null)
                {
                    DrawInfoLine("Has Cosmetic Set", "YES");
                }
            }
            else
            {
                DrawInfoLine("VR Rig", "Not available");
            }
            
            GUILayout.Space(10);
            
            // === CONNECTION INFO ===
            DrawSection("Connection & Network");
            DrawInfoLine("Photon Server", PhotonNetwork.CloudRegion ?? "Unknown");
            DrawInfoLine("Server Address", PhotonNetwork.ServerAddress ?? "Unknown");
            DrawInfoLine("Ping", PhotonNetwork.GetPing().ToString() + " ms");
            DrawInfoLine("Room Name", PhotonNetwork.CurrentRoom?.Name ?? "Unknown");
            DrawInfoLine("Room Player Count", PhotonNetwork.CurrentRoom?.PlayerCount.ToString() ?? "0");
            DrawInfoLine("Room Max Players", PhotonNetwork.CurrentRoom?.MaxPlayers.ToString() ?? "0");
            DrawInfoLine("Room Visible", PhotonNetwork.CurrentRoom?.IsVisible.ToString() ?? "Unknown");
            DrawInfoLine("Room Open", PhotonNetwork.CurrentRoom?.IsOpen.ToString() ?? "Unknown");
            
            GUILayout.Space(10);
            
            // === GAME STATE INFO ===
            DrawSection("Game State");
            if (GorillaGameManager.instance != null)
            {
                DrawInfoLine("Game Manager Active", "YES");
            }
            
            GUILayout.Space(10);
            
            // === TIMESTAMP ===
            DrawInfoLine("Data Retrieved At", DateTime.Now.ToString("HH:mm:ss"));
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // Action buttons
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Refresh Data", buttonStyle, GUILayout.Height(30)))
            {
                RefreshSelectedPlayerData();
            }
            
            if (GUILayout.Button("Save to File", buttonStyle, GUILayout.Height(30)))
            {
                if (SavePlayerData(selectedPlayer, data))
                {
                    successMessage = $"Data saved for {selectedPlayer.NickName}!";
                    successMessageTime = Time.time;
                }
            }
            
            if (GUILayout.Button("View Saved Data", buttonStyle, GUILayout.Height(30)))
            {
                OpenSavedDataFile();
            }
            
            GUILayout.EndHorizontal();
            
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }
        
        private void DrawSection(string title)
        {
            GUILayout.Space(5);
            
            // Separator line
            GUI.Box(new Rect(20, GUILayoutUtility.GetLastRect().yMax, 660, 2), "", 
                new GUIStyle { normal = { background = MakeTexture(2, 2, new Color(0.4f, 0.4f, 0.4f, 0.5f)) } });
            
            GUILayout.Space(10);
            GUILayout.Label(title, sectionHeaderStyle);
            GUILayout.Space(5);
        }
        
        private void DrawSuccessMessage()
        {
            // Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.7f) scale = 0.7f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            
            // Success box styling
            GUIStyle successBoxStyle = new GUIStyle(GUI.skin.box);
            successBoxStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.8f, 0.4f, 0.9f));
            successBoxStyle.padding = new RectOffset(20, 20, 15, 15);
            
            GUIStyle successTextStyle = new GUIStyle(GUI.skin.label);
            successTextStyle.fontSize = 18;
            successTextStyle.fontStyle = FontStyle.Bold;
            successTextStyle.normal.textColor = Color.white;
            successTextStyle.alignment = TextAnchor.MiddleCenter;
            
            // Calculate message size
            float messageWidth = 400;
            float messageHeight = 60;
            float x = (screenW - messageWidth) / 2;
            float y = screenH - 150; // Bottom of screen
            
            // Draw success message box
            GUILayout.BeginArea(new Rect(x, y, messageWidth, messageHeight), successBoxStyle);
            GUILayout.Label(successMessage, successTextStyle);
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }
        
        private void DrawInfoLine(string key, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{key}:</b>", dataLabelStyle, GUILayout.Width(200));
            GUILayout.Label($"<color=#88FF88>{value}</color>", dataLabelStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }
        
        private void RefreshPlayerData()
        {
            if (NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom) return;
            
            NetPlayer[] players = NetworkSystem.Instance.AllNetPlayers;
            foreach (NetPlayer player in players)
            {
                if (player != null && player.IsValid)
                {
                    GetOrCreatePlayerData(player);
                }
            }
        }
        
        private void RefreshSelectedPlayerData()
        {
            if (selectedPlayer != null && selectedPlayer.IsValid)
            {
                GetOrCreatePlayerData(selectedPlayer);
            }
        }
        
        private PlayerData GetOrCreatePlayerData(NetPlayer player)
        {
            string userId = player.UserId;
            
            if (!playerDataCache.ContainsKey(userId))
            {
                playerDataCache[userId] = new PlayerData();
            }
            
            PlayerData data = playerDataCache[userId];
            data.LastUpdated = Time.time;
            data.NickName = player.NickName;
            data.ActorNumber = player.ActorNumber;
            data.UserId = userId;
            data.IsLocal = player.IsLocal;
            data.IsMasterClient = player.IsMasterClient;
            
            // Try to get PlayFab ID if it's the local player
            if (player.IsLocal && PlayFabAuthenticator.instance != null)
            {
                try
                {
                    data.PlayFabId = PlayFabAuthenticator.instance.GetPlayFabPlayerId();
                }
                catch { }
            }
            
            return data;
        }
        
        private Player GetPhotonPlayer(NetPlayer netPlayer)
        {
            try
            {
                if (netPlayer is PunNetPlayer punPlayer)
                {
                    return punPlayer.PlayerRef;
                }
            }
            catch { }
            return null;
        }
        
        private VRRig GetVRRig(NetPlayer player)
        {
            try
            {
                // Try to find VRRig through GameObject search
                VRRig[] allRigs = FindObjectsOfType<VRRig>();
                foreach (VRRig rig in allRigs)
                {
                    // Access internal creator field via reflection
                    var creatorField = typeof(VRRig).GetField("creator", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (creatorField != null)
                    {
                        NetPlayer creator = creatorField.GetValue(rig) as NetPlayer;
                        if (creator == player)
                        {
                            return rig;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
        
        private RigContainer GetRigContainer(NetPlayer player)
        {
            try
            {
                // Try to find RigContainer through GameObject search
                VRRig rig = GetVRRig(player);
                if (rig != null)
                {
                    // Access internal rigContainer field via reflection
                    var rigContainerField = typeof(VRRig).GetField("rigContainer", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (rigContainerField != null)
                    {
                        return rigContainerField.GetValue(rig) as RigContainer;
                    }
                }
            }
            catch { }
            return null;
        }
        
        private bool SavePlayerData(NetPlayer player, PlayerData data)
        {
            try
            {
                // Get the DLL directory
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                string filePath = Path.Combine(dllDirectory, "playerdata.txt");
                
                // Build the data string
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=============================================");
                sb.AppendLine($"PLAYER DATA SAVED: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("=============================================");
                sb.AppendLine();
                
                sb.AppendLine("--- BASIC INFORMATION ---");
                sb.AppendLine($"Nickname: {player.NickName}");
                sb.AppendLine($"Display Name: {player.SanitizedNickName}");
                sb.AppendLine($"Is Local Player: {(player.IsLocal ? "YES" : "NO")}");
                sb.AppendLine($"Is Master Client: {(player.IsMasterClient ? "YES" : "NO")}");
                sb.AppendLine($"Actor Number: {player.ActorNumber}");
                sb.AppendLine($"In Room: {(player.InRoom ? "YES" : "NO")}");
                sb.AppendLine($"Joined Time: {player.JoinedTime:F2}s");
                sb.AppendLine();
                
                sb.AppendLine("--- PHOTON NETWORK DATA ---");
                sb.AppendLine($"User ID: {player.UserId}");
                
                Player photonPlayer = GetPhotonPlayer(player);
                if (photonPlayer != null)
                {
                    sb.AppendLine($"Actor Number: {photonPlayer.ActorNumber}");
                    sb.AppendLine($"Is Inactive: {(photonPlayer.IsInactive ? "YES" : "NO")}");
                    sb.AppendLine($"Is Master Client: {(photonPlayer.IsMasterClient ? "YES" : "NO")}");
                    sb.AppendLine($"Nickname: {photonPlayer.NickName}");
                    sb.AppendLine($"User ID: {photonPlayer.UserId}");
                    
                    if (photonPlayer.CustomProperties != null && photonPlayer.CustomProperties.Count > 0)
                    {
                        sb.AppendLine("Custom Properties:");
                        foreach (var prop in photonPlayer.CustomProperties)
                        {
                            sb.AppendLine($"  {prop.Key}: {prop.Value}");
                        }
                    }
                }
                sb.AppendLine();
                
                sb.AppendLine("--- PLAYFAB DATA ---");
                sb.AppendLine($"PlayFab ID: {data.PlayFabId ?? "Not available"}");
                sb.AppendLine($"Title ID: {PlayFabSettings.TitleId ?? "Not set"}");
                
                if (player.IsLocal && PlayFabAuthenticator.instance != null)
                {
                    try
                    {
                        string localPlayFabId = PlayFabAuthenticator.instance.GetPlayFabPlayerId();
                        if (!string.IsNullOrEmpty(localPlayFabId))
                        {
                            sb.AppendLine($"Local PlayFab ID: {localPlayFabId}");
                        }
                    }
                    catch { }
                }
                sb.AppendLine();
                
                sb.AppendLine("--- STEAM DATA ---");
                if (player.IsLocal)
                {
                    try
                    {
                        CSteamID steamId = SteamUser.GetSteamID();
                        sb.AppendLine($"Steam ID: {steamId}");
                        sb.AppendLine($"Steam ID (64-bit): {steamId.m_SteamID}");
                        sb.AppendLine($"Account ID: {steamId.GetAccountID()}");
                        sb.AppendLine($"Steam Name: {SteamFriends.GetPersonaName()}");
                        sb.AppendLine($"Persona State: {SteamFriends.GetPersonaState()}");
                        sb.AppendLine($"Steam Level: {SteamUser.GetPlayerSteamLevel()}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Steam Data Error: {ex.Message}");
                    }
                }
                else
                {
                    sb.AppendLine("Only available for local player");
                }
                sb.AppendLine();
                
                sb.AppendLine("--- VOICE & AUDIO ---");
                RigContainer rigContainer = GetRigContainer(player);
                if (rigContainer != null)
                {
                    sb.AppendLine($"Is Muted: {(rigContainer.Muted ? "YES" : "NO")}");
                    sb.AppendLine($"Force Mute: {(rigContainer.ForceMute ? "YES" : "NO")}");
                    sb.AppendLine($"Has Manual Mute: {(rigContainer.hasManualMute ? "YES" : "NO")}");
                    sb.AppendLine($"Auto Muted: {(rigContainer.GetIsPlayerAutoMuted() ? "YES" : "NO")}");
                    sb.AppendLine($"Chat Quality: {rigContainer.playerChatQuality}");
                    sb.AppendLine($"Initialized: {(rigContainer.Initialized ? "YES" : "NO")}");
                }
                sb.AppendLine();
                
                sb.AppendLine("--- VR RIG DATA ---");
                VRRig rig = GetVRRig(player);
                if (rig != null)
                {
                    sb.AppendLine($"Is Offline Rig: {(rig.isOfflineVRRig ? "YES" : "NO")}");
                    sb.AppendLine($"Is My Player: {(rig.isMyPlayer ? "YES" : "NO")}");
                    sb.AppendLine($"Show Name: {(rig.showName ? "YES" : "NO")}");
                    sb.AppendLine($"Player Name: {rig.playerNameVisible}");
                    sb.AppendLine($"Is Muted: {(rig.muted ? "YES" : "NO")}");
                    
                    Color playerColor = rig.playerColor;
                    sb.AppendLine($"Player Color: R:{playerColor.r:F2} G:{playerColor.g:F2} B:{playerColor.b:F2}");
                    
                    Vector3 pos = rig.transform.position;
                    sb.AppendLine($"Position: X:{pos.x:F2} Y:{pos.y:F2} Z:{pos.z:F2}");
                }
                sb.AppendLine();
                
                sb.AppendLine("--- CONNECTION & NETWORK ---");
                sb.AppendLine($"Photon Server: {PhotonNetwork.CloudRegion ?? "Unknown"}");
                sb.AppendLine($"Server Address: {PhotonNetwork.ServerAddress ?? "Unknown"}");
                sb.AppendLine($"Ping: {PhotonNetwork.GetPing()} ms");
                sb.AppendLine($"Room Name: {PhotonNetwork.CurrentRoom?.Name ?? "Unknown"}");
                sb.AppendLine($"Room Player Count: {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");
                sb.AppendLine($"Room Max Players: {PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0}");
                sb.AppendLine($"Room Visible: {PhotonNetwork.CurrentRoom?.IsVisible ?? false}");
                sb.AppendLine($"Room Open: {PhotonNetwork.CurrentRoom?.IsOpen ?? false}");
                sb.AppendLine();
                
                sb.AppendLine("=============================================");
                sb.AppendLine();
                
                // Append to file (not overwrite)
                File.AppendAllText(filePath, sb.ToString());
                
                Logger.LogInfo($"Player data saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save player data: {ex}");
                return false;
            }
        }
        
        private void OpenSavedDataFile()
        {
            try
            {
                string dllPath = Assembly.GetExecutingAssembly().Location;
                string dllDirectory = Path.GetDirectoryName(dllPath);
                string filePath = Path.Combine(dllDirectory, "playerdata.txt");
                
                if (File.Exists(filePath))
                {
                    // Open file with default text editor on Linux
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    
                    successMessage = "Opening saved data file...";
                    successMessageTime = Time.time;
                }
                else
                {
                    successMessage = "No saved data file found";
                    successMessageTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open saved data file: {ex}");
                successMessage = "Failed to open file";
                successMessageTime = Time.time;
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
            playerDataCache.Clear();
        }
    }
    
    /// <summary>
    /// Player data storage class
    /// </summary>
    public class PlayerData
    {
        public string NickName;
        public string UserId;
        public int ActorNumber;
        public bool IsLocal;
        public bool IsMasterClient;
        public string PlayFabId;
        public float LastUpdated;
    }
    
    /// <summary>
    /// Plugin information
    /// </summary>
    public static class PluginInfo
    {
        public const string GUID = "com.lane.playerinfomod";
        public const string Name = "Player Info Mod";
        public const string Version = "1.0.0";
    }
}
