using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using GorillaLocomotion;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathRecorder
{
    [BepInPlugin("com.pathrecorder.mod", "Path Recorder", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        // Recording state
        private bool isRecording = false;
        private List<PathPoint> recordedPath = new List<PathPoint>();
        private string currentRecordingName = "NewPath";
        private float lastRecordTime = 0f;
        private float recordInterval = 0.1f; // Record 10 points per second (adjustable)
        
        // Playback state
        private bool isPlayingBack = false;
        private float playbackStartTime = 0f;
        private List<PathPoint> currentPlaybackPath = null;
        
        // UI state
        private bool isMenuOpen = false;
        private Vector2 scrollPosition = Vector2.zero;
        private List<string> savedPaths = new List<string>();
        
        // Feedback system
        private string feedbackMessage = "";
        private float feedbackTime = 0f;
        private float feedbackDuration = 3f;
        private Color feedbackColor = Color.green;
        
        // Settings
        private float optimizationAmount = 0f; // 0 = no optimization, 1 = maximum optimization
        private float curvenessAmount = 0f; // 0 = linear, 1 = smooth curves
        
        // Paths directory
        private string pathsDirectory;
        
        // GUI Styles
        private GUIStyle menuBoxStyle;
        private Texture2D menuBgTexture;
        
        private void Awake()
        {
            Debug.Log("[PathRecorder] Initialized v1.0.0");
            
            // Setup paths directory - use BepInEx plugins folder
            string bepInExFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            pathsDirectory = Path.Combine(bepInExFolder, "paths");
            
            try
            {
                if (!Directory.Exists(pathsDirectory))
                {
                    Directory.CreateDirectory(pathsDirectory);
                    Debug.Log($"[PathRecorder] Created paths directory: {pathsDirectory}");
                }
                else
                {
                    Debug.Log($"[PathRecorder] Using paths directory: {pathsDirectory}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PathRecorder] Failed to create paths directory: {ex.Message}");
            }
            
            RefreshSavedPaths();
        }
        
        private void Update()
        {
            if (!GTPlayer.hasInstance || !GorillaTagger.hasInstance) return;
            
            // F2 Key to toggle menu
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                isMenuOpen = !isMenuOpen;
                
                if (isMenuOpen)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            
            // Recording logic
            if (isRecording)
            {
                RecordCurrentPosition();
            }
            
            // Playback logic
            if (isPlayingBack && currentPlaybackPath != null)
            {
                UpdatePlayback();
            }
        }
        
        private void RecordCurrentPosition()
        {
            // Throttle recording to prevent massive files
            if (Time.time - lastRecordTime < recordInterval) return;
            
            var player = GTPlayer.Instance;
            var tagger = GorillaTagger.Instance;
            
            if (player == null || tagger == null) return;
            
            PathPoint point = new PathPoint
            {
                position = tagger.rigidbody.position,
                rotation = tagger.offlineVRRig.head.rigTarget.rotation,
                leftHandPosition = tagger.offlineVRRig.leftHand.rigTarget.position,
                leftHandRotation = tagger.offlineVRRig.leftHand.rigTarget.rotation,
                rightHandPosition = tagger.offlineVRRig.rightHand.rigTarget.position,
                rightHandRotation = tagger.offlineVRRig.rightHand.rigTarget.rotation,
                timestamp = Time.time
            };
            
            recordedPath.Add(point);
            lastRecordTime = Time.time;
        }
        
        private void UpdatePlayback()
        {
            if (currentPlaybackPath == null || currentPlaybackPath.Count == 0) return;
            
            float elapsedTime = Time.time - playbackStartTime;
            
            // Find the appropriate point in the path based on elapsed time
            PathPoint targetPoint = null;
            PathPoint nextPoint = null;
            
            for (int i = 0; i < currentPlaybackPath.Count - 1; i++)
            {
                float pointTime = currentPlaybackPath[i].timestamp - currentPlaybackPath[0].timestamp;
                float nextPointTime = currentPlaybackPath[i + 1].timestamp - currentPlaybackPath[0].timestamp;
                
                if (elapsedTime >= pointTime && elapsedTime < nextPointTime)
                {
                    targetPoint = currentPlaybackPath[i];
                    nextPoint = currentPlaybackPath[i + 1];
                    break;
                }
            }
            
            // If we've reached the end, stop playback
            if (targetPoint == null)
            {
                if (elapsedTime >= currentPlaybackPath[currentPlaybackPath.Count - 1].timestamp - currentPlaybackPath[0].timestamp)
                {
                    StopPlayback();
                }
                return;
            }
            
            // Interpolate between points based on curveness setting
            var tagger = GorillaTagger.Instance;
            if (tagger == null) return;
            
            float targetPointTime = targetPoint.timestamp - currentPlaybackPath[0].timestamp;
            float targetNextPointTime = nextPoint.timestamp - currentPlaybackPath[0].timestamp;
            float t = (elapsedTime - targetPointTime) / (targetNextPointTime - targetPointTime);
            
            // Apply curveness (smooth interpolation)
            if (curvenessAmount > 0)
            {
                t = Mathf.SmoothStep(0f, 1f, t * (1f - curvenessAmount * 0.5f) + curvenessAmount * 0.5f);
            }
            
            // Set positions and rotations
            tagger.rigidbody.position = Vector3.Lerp(targetPoint.position, nextPoint.position, t);
            
            // Set velocity to zero to prevent physics interference
            var rb = tagger.rigidbody;
            rb.velocity = Vector3.zero;
            
            if (tagger.offlineVRRig.head.rigTarget != null)
            {
                tagger.offlineVRRig.head.rigTarget.rotation = Quaternion.Slerp(targetPoint.rotation, nextPoint.rotation, t);
            }
            
            if (tagger.offlineVRRig.leftHand.rigTarget != null)
            {
                tagger.offlineVRRig.leftHand.rigTarget.position = Vector3.Lerp(targetPoint.leftHandPosition, nextPoint.leftHandPosition, t);
                tagger.offlineVRRig.leftHand.rigTarget.rotation = Quaternion.Slerp(targetPoint.leftHandRotation, nextPoint.leftHandRotation, t);
            }
            
            if (tagger.offlineVRRig.rightHand.rigTarget != null)
            {
                tagger.offlineVRRig.rightHand.rigTarget.position = Vector3.Lerp(targetPoint.rightHandPosition, nextPoint.rightHandPosition, t);
                tagger.offlineVRRig.rightHand.rigTarget.rotation = Quaternion.Slerp(targetPoint.rightHandRotation, nextPoint.rightHandRotation, t);
            }
        }
        
        private void StartRecording()
        {
            recordedPath.Clear();
            isRecording = true;
            isPlayingBack = false;
            ShowFeedback("Recording started!", Color.green);
            Debug.Log("[PathRecorder] Started recording");
        }
        
        private void StopRecording()
        {
            isRecording = false;
            ShowFeedback($"Recording stopped! Captured {recordedPath.Count} points", Color.yellow);
            Debug.Log($"[PathRecorder] Stopped recording. Recorded {recordedPath.Count} points");
        }
        
        private void SaveCurrentPath(string name)
        {
            if (recordedPath.Count == 0)
            {
                ShowFeedback("No path to save! Record something first.", Color.red);
                Debug.LogWarning("[PathRecorder] No path to save!");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowFeedback("Please enter a path name!", Color.red);
                return;
            }
            
            // Apply optimization if needed
            List<PathPoint> pathToSave = recordedPath;
            
            if (optimizationAmount > 0)
            {
                pathToSave = OptimizePath(recordedPath, optimizationAmount);
                Debug.Log($"[PathRecorder] Optimized path from {recordedPath.Count} to {pathToSave.Count} points");
            }
            
            PathData data = new PathData
            {
                name = name,
                points = pathToSave
            };
            
            try
            {
                string json = SerializePathData(data);
                string filePath = Path.Combine(pathsDirectory, $"{name}.json");
                
                Debug.Log($"[PathRecorder] Attempting to save to: {filePath}");
                File.WriteAllText(filePath, json);
                ShowFeedback($"Saved '{name}' with {pathToSave.Count} points!", Color.green);
                Debug.Log($"[PathRecorder] Successfully saved path to: {filePath}");
                
                RefreshSavedPaths();
            }
            catch (System.Exception ex)
            {
                ShowFeedback($"Error saving path: {ex.Message}", Color.red);
                Debug.LogError($"[PathRecorder] Failed to save path: {ex}");
            }
        }
        
        private string SerializePathData(PathData data)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": \"{data.name}\",");
            sb.AppendLine("  \"points\": [");
            
            for (int i = 0; i < data.points.Count; i++)
            {
                PathPoint p = data.points[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"px\": {p.position.x}, \"py\": {p.position.y}, \"pz\": {p.position.z},");
                sb.AppendLine($"      \"rx\": {p.rotation.x}, \"ry\": {p.rotation.y}, \"rz\": {p.rotation.z}, \"rw\": {p.rotation.w},");
                sb.AppendLine($"      \"lhx\": {p.leftHandPosition.x}, \"lhy\": {p.leftHandPosition.y}, \"lhz\": {p.leftHandPosition.z},");
                sb.AppendLine($"      \"lhrx\": {p.leftHandRotation.x}, \"lhry\": {p.leftHandRotation.y}, \"lhrz\": {p.leftHandRotation.z}, \"lhrw\": {p.leftHandRotation.w},");
                sb.AppendLine($"      \"rhx\": {p.rightHandPosition.x}, \"rhy\": {p.rightHandPosition.y}, \"rhz\": {p.rightHandPosition.z},");
                sb.AppendLine($"      \"rhrx\": {p.rightHandRotation.x}, \"rhry\": {p.rightHandRotation.y}, \"rhrz\": {p.rightHandRotation.z}, \"rhrw\": {p.rightHandRotation.w},");
                sb.AppendLine($"      \"timestamp\": {p.timestamp}");
                sb.Append("    }");
                if (i < data.points.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }
        
        private PathData DeserializePathData(string json)
        {
            // Simple JSON parser for our specific format
            PathData data = new PathData();
            data.points = new List<PathPoint>();
            
            string[] lines = json.Split('\n');
            PathPoint currentPoint = null;
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim().TrimEnd(',');
                
                if (trimmed.Contains("\"name\":"))
                {
                    int start = trimmed.IndexOf('"', trimmed.IndexOf(':')) + 1;
                    int end = trimmed.LastIndexOf('"');
                    data.name = trimmed.Substring(start, end - start);
                }
                else if (trimmed == "{" && currentPoint == null)
                {
                    currentPoint = new PathPoint();
                }
                else if (trimmed.StartsWith("\"px\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.position.x = float.Parse(parts[0].Split(':')[1].Trim());
                    currentPoint.position.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.position.z = float.Parse(parts[2].Split(':')[1].Trim());
                }
                else if (trimmed.StartsWith("\"rx\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.rotation.x = float.Parse(parts[0].Split(':')[1].Trim());
                    currentPoint.rotation.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.rotation.z = float.Parse(parts[2].Split(':')[1].Trim());
                    currentPoint.rotation.w = float.Parse(parts[3].Split(':')[1].Trim());
                }
                else if (trimmed.StartsWith("\"lhx\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.leftHandPosition.x = float.Parse(parts[0].Split(':')[1].Trim());
                    currentPoint.leftHandPosition.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.leftHandPosition.z = float.Parse(parts[2].Split(':')[1].Trim());
                }
                else if (trimmed.StartsWith("\"lhrx\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.leftHandRotation.x = float.Parse(parts[0].Split(':')[1].Trim());
                    currentPoint.leftHandRotation.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.leftHandRotation.z = float.Parse(parts[2].Split(':')[1].Trim());
                    currentPoint.leftHandRotation.w = float.Parse(parts[3].Split(':')[1].Trim());
                }
               else if (trimmed.StartsWith("\"rhx\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.rightHandPosition.x = float.Parse(parts[0].Split(':')[1].Trim());
                    currentPoint.rightHandPosition.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.rightHandPosition.z = float.Parse(parts[2].Split(':')[1].Trim());
                }
                else if (trimmed.StartsWith("\"rhrx\":"))
                {
                    string[] parts = trimmed.Split(',');
                    currentPoint.rightHandRotation.x = float.Parse(parts[0].Split(':')[1].Trim());
                   currentPoint.rightHandRotation.y = float.Parse(parts[1].Split(':')[1].Trim());
                    currentPoint.rightHandRotation.z = float.Parse(parts[2].Split(':')[1].Trim());
                    currentPoint.rightHandRotation.w = float.Parse(parts[3].Split(':')[1].Trim());
                }
                else if (trimmed.StartsWith("\"timestamp\":"))
                {
                    currentPoint.timestamp = float.Parse(trimmed.Split(':')[1].Trim());
                }
                else if (trimmed == "}" && currentPoint != null)
                {
                    data.points.Add(currentPoint);
                    currentPoint = null;
                }
            }
            
            return data;
        }
        
        private List<PathPoint> OptimizePath(List<PathPoint> original, float amount)
        {
            if (original.Count <= 2) return new List<PathPoint>(original);
            
            // Simple optimization: remove points based on distance threshold
            // Higher optimization = remove more points
            float threshold = 0.01f + (amount * 0.5f); // 0.01 to 0.51 units
            
            List<PathPoint> optimized = new List<PathPoint>();
            optimized.Add(original[0]); // Always keep first point
            
            for (int i = 1; i < original.Count - 1; i++)
            {
                float distance = Vector3.Distance(original[i].position, optimized[optimized.Count - 1].position);
                
                if (distance >= threshold)
                {
                    optimized.Add(original[i]);
                }
            }
            
            optimized.Add(original[original.Count - 1]); // Always keep last point
            
            return optimized;
        }
        
        private void LoadPath(string name)
        {
            string filePath = Path.Combine(pathsDirectory, $"{name}.json");
            
            if (!File.Exists(filePath))
            {
                ShowFeedback($"Path '{name}' not found!", Color.red);
                Debug.LogError($"[PathRecorder] Path file not found: {filePath}");
                return;
            }
            
            try
            {
                string json = File.ReadAllText(filePath);
                PathData data = DeserializePathData(json);
                
                currentPlaybackPath = data.points;
                ShowFeedback($"Loaded '{name}' ({currentPlaybackPath.Count} points)", Color.cyan);
                Debug.Log($"[PathRecorder] Loaded path '{name}' with {currentPlaybackPath.Count} points");
            }
            catch (System.Exception ex)
            {
                ShowFeedback($"Error loading '{name}': {ex.Message}", Color.red);
                Debug.LogError($"[PathRecorder] Error loading path: {ex}");
            }
        }
        
        private void StartPlayback()
        {
            if (currentPlaybackPath == null || currentPlaybackPath.Count == 0)
            {
                ShowFeedback("Load a path first before playing back!", Color.red);
                Debug.LogWarning("[PathRecorder] No path loaded for playback!");
                return;
            }
            
            isPlayingBack = true;
            isRecording = false;
            playbackStartTime = Time.time;
            float duration = currentPlaybackPath[currentPlaybackPath.Count - 1].timestamp - currentPlaybackPath[0].timestamp;
            ShowFeedback($"Playback started! Duration: {duration:F1}s", Color.green);
            Debug.Log("[PathRecorder] Started playback");
        }
        
        private void StopPlayback()
        {
            isPlayingBack = false;
            ShowFeedback("Playback stopped", Color.yellow);
            Debug.Log("[PathRecorder] Stopped playback");
        }
        
        private void RefreshSavedPaths()
        {
            savedPaths.Clear();
            
            if (!Directory.Exists(pathsDirectory))
            {
                return;
            }
            
            string[] files = Directory.GetFiles(pathsDirectory, "*.json");
            
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                savedPaths.Add(name);
            }
            
            Debug.Log($"[PathRecorder] Found {savedPaths.Count} saved paths");
        }
        
        private void DeletePath(string name)
        {
            string filePath = Path.Combine(pathsDirectory, $"{name}.json");
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                ShowFeedback($"Deleted '{name}'", Color.yellow);
                Debug.Log($"[PathRecorder] Deleted path: {name}");
                RefreshSavedPaths();
            }
            else
            {
                ShowFeedback($"Path '{name}' not found!", Color.red);
            }
        }
        
        private void ShowFeedback(string message, Color color)
        {
            feedbackMessage = message;
            feedbackColor = color;
            feedbackTime = Time.time;
        }
        
        private void OnGUI()
        {
            if (!isMenuOpen) return;
            
            // Setup Scaling
            float refHeight = 1080f;
            float scale = Screen.height / refHeight;
            if (scale < 0.8f) scale = 0.8f;
            
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            
            float width = 600;
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;
            
            // Setup styles
            if (menuBoxStyle == null)
            {
                menuBoxStyle = new GUIStyle(GUI.skin.box);
                menuBgTexture = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                menuBoxStyle.normal.background = menuBgTexture;
                menuBoxStyle.fontSize = 14;
                menuBoxStyle.normal.textColor = Color.white;
                menuBoxStyle.padding = new RectOffset(20, 20, 20, 20);
            }
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 16;
            buttonStyle.margin = new RectOffset(0, 0, 5, 5);
            buttonStyle.padding = new RectOffset(10, 10, 8, 8);
            
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.normal.textColor = Color.white;
            labelStyle.richText = true;
            
            GUIStyle smallButtonStyle = new GUIStyle(GUI.skin.button);
            smallButtonStyle.fontSize = 14;
            smallButtonStyle.padding = new RectOffset(5, 5, 5, 5);
            
            // Dynamic path list height
            int pathCount = savedPaths.Count;
            float pathListHeight = Mathf.Max(100, Mathf.Min(300, pathCount * 35 + 50));
            
            // Center horizontally, start near top vertically
            float x = (screenW - width) / 2;
            float y = 50; // Start from top with padding
            
            // Begin vertical auto-layout
            GUILayout.BeginArea(new Rect(x, y, width, screenH - 100)); // Use screen height minus padding
            GUILayout.BeginVertical(menuBoxStyle);
            
            // Header
            GUILayout.Label("Path Recorder", headerStyle);
            GUILayout.Space(10);
            
            // Feedback message display (prominent)
            if (Time.time - feedbackTime < feedbackDuration && !string.IsNullOrEmpty(feedbackMessage))
            {
                GUIStyle feedbackStyle = new GUIStyle(GUI.skin.box);
                feedbackStyle.fontSize = 16;
                feedbackStyle.fontStyle = FontStyle.Bold;
                feedbackStyle.alignment = TextAnchor.MiddleCenter;
                feedbackStyle.normal.textColor = feedbackColor;
                feedbackStyle.normal.background = MakeTex(2, 2, new Color(feedbackColor.r * 0.2f, feedbackColor.g * 0.2f, feedbackColor.b * 0.2f, 0.8f));
                feedbackStyle.padding = new RectOffset(10, 10, 10, 10);
                
                GUILayout.Box(feedbackMessage, feedbackStyle);
                GUILayout.Space(10);
            }
            
            // Status display
            string status = "Idle";
            if (isRecording)
            {
                status = $"<color=#ff4444>● RECORDING ({recordedPath.Count} points)</color>";
            }
            else if (isPlayingBack)
            {
                float elapsed = Time.time - playbackStartTime;
                float total = currentPlaybackPath[currentPlaybackPath.Count - 1].timestamp - currentPlaybackPath[0].timestamp;
                status = $"<color=#44ff44>▶ PLAYING ({elapsed:F1}s / {total:F1}s)</color>";
            }
            
            GUILayout.Label($"<b>Status:</b> {status}", labelStyle);
            GUILayout.Space(10);
            
            // Recording Section
            GUILayout.Label("<b>Recording Controls:</b>", labelStyle);
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Path Name:", labelStyle, GUILayout.Width(100));
            currentRecordingName = GUILayout.TextField(currentRecordingName, 50, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            
            if (!isRecording)
            {
                if (GUILayout.Button("Start Recording", buttonStyle, GUILayout.Height(35)))
                {
                    StartRecording();
                }
            }
            else
            {
                if (GUILayout.Button("Stop Recording", buttonStyle, GUILayout.Height(35)))
                {
                    StopRecording();
                }
            }
            
            GUI.enabled = !isRecording && recordedPath.Count > 0;
            if (GUILayout.Button("Save Path", buttonStyle, GUILayout.Height(35)))
            {
                SaveCurrentPath(currentRecordingName);
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
            
            if (recordedPath.Count > 0 && !isRecording)
            {
                GUILayout.Label($"<color=#ffff44>Current: {recordedPath.Count} points (not saved)</color>", labelStyle);
            }
            
            GUILayout.Space(15);
            
            // Settings Section
            GUILayout.Label("<b>Path Settings:</b>", labelStyle);
            GUILayout.Space(5);
            
            GUILayout.Label($"Optimization: {optimizationAmount:F2} (Note: Does not account for objects)", labelStyle);
            optimizationAmount = GUILayout.HorizontalSlider(optimizationAmount, 0f, 1f);
            
            GUILayout.Space(5);
            
            GUILayout.Label($"Curveness: {curvenessAmount:F2} (Note: Does not account for objects)", labelStyle);
            curvenessAmount = GUILayout.HorizontalSlider(curvenessAmount, 0f, 1f);
            
            GUILayout.Space(15);
            
            // Saved Paths Section
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Saved Paths ({savedPaths.Count}):</b>", labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", smallButtonStyle))
            {
                RefreshSavedPaths();
                ShowFeedback("Path list refreshed", Color.cyan);
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Scrollable list of saved paths
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(pathListHeight));
            
            if (savedPaths.Count == 0)
            {
                GUILayout.Space(30);
                GUIStyle emptyStyle = new GUIStyle(labelStyle);
                emptyStyle.alignment = TextAnchor.MiddleCenter;
                emptyStyle.normal.textColor = Color.gray;
                GUILayout.Label("No saved paths\nRecord and save to get started!", emptyStyle);
            }
            else
            {
                foreach (string pathName in savedPaths)
                {
                    GUILayout.BeginHorizontal();
                    
                    GUILayout.Label(pathName, labelStyle, GUILayout.Width(350));
                    
                    if (GUILayout.Button("Load", smallButtonStyle, GUILayout.Width(70)))
                    {
                        LoadPath(pathName);
                        currentRecordingName = pathName;
                    }
                    
                    if (GUILayout.Button("Delete", smallButtonStyle, GUILayout.Width(80)))
                    {
                        DeletePath(pathName);
                    }
                    
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);
                }
            }
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // Playback Controls
            GUILayout.Label("<b>Playback Controls:</b>", labelStyle);
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            
            GUI.enabled = currentPlaybackPath != null && !isPlayingBack;
            if (GUILayout.Button("Start Playback", buttonStyle, GUILayout.Height(35)))
            {
                StartPlayback();
            }
            GUI.enabled = true;
            
            GUI.enabled = isPlayingBack;
            if (GUILayout.Button("Stop Playback", buttonStyle, GUILayout.Height(35)))
            {
                StopPlayback();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
            
            if (currentPlaybackPath != null)
            {
                float duration = currentPlaybackPath[currentPlaybackPath.Count - 1].timestamp - currentPlaybackPath[0].timestamp;
                GUILayout.Label($"<color=#44ff44>Loaded: {currentPlaybackPath.Count} points, {duration:F1}s</color>", labelStyle);
            }
            
            GUILayout.Space(10);
            
            GUILayout.Label("<color=yellow>Press F2 to close this menu</color>", labelStyle);
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            GUI.matrix = oldMatrix;
        }
        
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
    
    [System.Serializable]
    public class PathPoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;
        public float timestamp;
    }
    
    [System.Serializable]
    public class PathData
    {
        public string name;
        public List<PathPoint> points;
    }
}
