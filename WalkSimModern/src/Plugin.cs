using BepInEx;
using UnityEngine;
using UnityEngine.InputSystem;
using GorillaLocomotion;
using HarmonyLib;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using Photon.Pun;

namespace WalkSimModern
{
    [BepInPlugin("com.modern.walksim", "WalkSim Modern", "1.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public enum WalkMode
        {
            Gorilla, // New default: Floor-based stepping
            Silly,   // Old: Hand swinging
            Ghost,   // Creepy: Floating & Glitching
            Swimming // Swimming: Arm stroke animation in water
        }

        private WalkMode currentMode = WalkMode.Gorilla;
        private WalkMode modeBeforeWater = WalkMode.Gorilla; // Store mode before entering water
        private bool wasInWater = false; // Track water state for auto-switching
        private bool isMenuOpen = false;
        private bool isMouseLookSuspended = false; // Hidden toggle for other mods
        
        // Mouse look
        private float rotationX = 0f;
        private float rotationY = 0f;
        private float mouseSensitivity = 2f;
        
        // Animation variables
        private float walkCycle = 0f;
        
        // Gorilla Mode specific
        private Vector3 leftHandOffset = Vector3.zero;
        private Vector3 rightHandOffset = Vector3.zero;

        // Speed settings
        private float walkSpeed = 5f;
        private float runSpeed = 10f;
        private float bodyLiftHeight = 0.56f;
        private float maxClimbHeight = 0f;

        // Grounding state
        private Vector3 leftHandPlantPos;
        private Vector3 rightHandPlantPos;
        private bool wasLeftPlanted = false;
        private bool wasRightPlanted = false;

        // Swimming state
        private float swimCycle = 0f;
        private bool isLeftArmStroking = true;

        // Auto Climb State
        private bool isClimbing = false;
        private Vector3 climbStartPos;
        private Vector3 climbTargetPos;
        private float climbStartTime;
        private float climbDuration = 0.6f; // Slightly longer for smoothness
        private Vector3 climbHandLeft;
        private Vector3 climbHandRight;
        private Vector3 handStartLeft;
        private Vector3 handStartRight;
        private float nextGhostUpdate = 0f;
        private Vector3 ghostLeftPos, ghostRightPos;
        private Quaternion ghostLeftRot, ghostRightRot;
        private float ghostFps = 10f; // Targeting low FPS feel
        
        private void Awake()
        {
            Debug.Log("[WalkSim Modern] Initialized v1.2.0");
            try
            {
                var harmony = new Harmony("com.modern.walksim");
                harmony.PatchAll();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WalkSim Modern] Harmony error: {ex}");
            }
        }

        private GUIStyle _menuBoxStyle;
        private Texture2D _menuBgTexture;

        private void OnGUI()
        {
            if (isMenuOpen)
            {
                // Setup Scaling
                float refHeight = 1080f;
                float scale = Screen.height / refHeight;
                // Make it slightly larger on small screens for readability
                if (scale < 0.8f) scale = 0.8f; 

                // apply scaling
                Matrix4x4 oldMatrix = GUI.matrix;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

                // Scaled Dimensions
                float width = 450;
                float height = 600; // Increased height for new options
                
                // Centered Position (accounting for scale)
                float screenW = Screen.width / scale;
                float screenH = Screen.height / scale;
                float x = (screenW - width) / 2;
                float y = (screenH - height) / 2;

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 18;
                buttonStyle.margin = new RectOffset(0, 0, 10, 10);
                buttonStyle.padding = new RectOffset(10, 10, 10, 10);

                GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontSize = 28;
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.normal.textColor = new Color(1f, 0.6f, 0.0f); // Orange tint

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 18;
                labelStyle.normal.textColor = Color.white;
                labelStyle.alignment = TextAnchor.MiddleLeft;
                labelStyle.richText = true;

                // --- CACHED STYLE ---
                if (_menuBoxStyle == null)
                {
                    _menuBoxStyle = new GUIStyle(GUI.skin.box);
                    _menuBgTexture = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                    _menuBoxStyle.normal.background = _menuBgTexture;
                    _menuBoxStyle.fontSize = 14;
                    _menuBoxStyle.normal.textColor = Color.white;
                    _menuBoxStyle.padding = new RectOffset(20, 20, 20, 20);
                }

                // --- DRAW MENU (Dynamic Centering) ---
                // We use the full scaled screen area and flexible spaces to center the content vertically and horizontally
                GUILayout.BeginArea(new Rect(0, 0, screenW, screenH));
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace(); // Push down
                
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Push right
                
                // The actual Menu Container
                GUILayout.BeginVertical(_menuBoxStyle, GUILayout.Width(width)); 

                // Header
                GUILayout.Label("WalkSim Modern", headerStyle);
                GUILayout.Space(20);

                // Mode Section
                GUILayout.Label($"<b>Current Mode:</b> {currentMode}", labelStyle);
                GUILayout.Space(10);

                if (GUILayout.Button("Gorilla Walk (Default)", buttonStyle))
                {
                    currentMode = WalkMode.Gorilla;
                }
                
                if (GUILayout.Button("Silly Swing (Old)", buttonStyle))
                {
                    currentMode = WalkMode.Silly;
                }

                if (GUILayout.Button("Ghost Walk (Creepy)", buttonStyle))
                {
                    currentMode = WalkMode.Ghost;
                    modeBeforeWater = WalkMode.Ghost;
                }

                if (GUILayout.Button("Swimming (Auto in Water)", buttonStyle))
                {
                    currentMode = WalkMode.Swimming;
                    modeBeforeWater = WalkMode.Swimming;
                }

                GUILayout.Space(20);
                
                // Speed Section
                GUILayout.Label($"<b>Walk Speed:</b> {walkSpeed:F1}", labelStyle);
                walkSpeed = GUILayout.HorizontalSlider(walkSpeed, 1f, 20f);
                
                GUILayout.Space(10);
                
                GUILayout.Label($"<b>Run Speed:</b> {runSpeed:F1}", labelStyle);
                runSpeed = GUILayout.HorizontalSlider(runSpeed, 5f, 50f);

                GUILayout.Space(10);

                GUILayout.Label($"<b>Body Lift:</b> {bodyLiftHeight:F2}", labelStyle);
                bodyLiftHeight = GUILayout.HorizontalSlider(bodyLiftHeight, 0f, 2f);

                GUILayout.Space(10);

                GUILayout.Label($"<b>Max Climb:</b> {maxClimbHeight:F1}", labelStyle);
                maxClimbHeight = GUILayout.HorizontalSlider(maxClimbHeight, 0f, 5.0f);

                GUILayout.Space(20);
                
                // Controls Section
                // Separator line using a colored box
                GUIStyle separatorMsg = new GUIStyle(GUI.skin.box);
                separatorMsg.normal.background = MakeTex(2, 2, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                separatorMsg.fixedHeight = 2;
                separatorMsg.margin = new RectOffset(0, 0, 10, 10);
                GUILayout.Box(GUIContent.none, separatorMsg);
                
                GUIStyle smallLabel = new GUIStyle(labelStyle);
                smallLabel.fontSize = 14;
                smallLabel.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

                GUILayout.Label("• Hold <b>LMB</b> to Look", smallLabel);
                GUILayout.Label("• <b>WASD</b> to Move", smallLabel);
                GUILayout.Label("• <b>SHIFT</b> Run | <b>C</b> Crouch", smallLabel);
                GUILayout.Label("• <b>ARROWS</b> to Adjust Speed", smallLabel);
                GUILayout.Label("• <b>T</b> to Sync Camera", smallLabel);

                GUILayout.EndVertical(); // End Menu Container
                
                GUILayout.FlexibleSpace(); // Push left
                GUILayout.EndHorizontal();
                
                GUILayout.FlexibleSpace(); // Push up
                GUILayout.EndVertical();
                GUILayout.EndArea();

                // Restore matrix
                GUI.matrix = oldMatrix;
            }
        }

        // Helper to generate a simple colored texture for backgrounds
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

        private void Update()
        {
            if (!GTPlayer.hasInstance || !GorillaTagger.hasInstance) return;
            
            var player = GTPlayer.Instance;
            var tagger = GorillaTagger.Instance;
            var rigidbody = tagger.rigidbody;
            
            if (player == null || tagger == null || rigidbody == null) return;

            // MENU CONTROLS (TAB)
            if (Keyboard.current.tabKey.isPressed)
            {
                if (!isMenuOpen)
                {
                    isMenuOpen = true;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else
            {
                if (isMenuOpen)
                {
                    isMenuOpen = false;
                    // Dont auto-lock, let the look logic handle it
                }
            }

            // AUTO-SWITCH TO SWIMMING IN WATER
            HandleWaterModeSwitch();

            // MOUSE SUSPEND TOGGLE (F1, F2, F3, or F5) - F2 works with PathRecorder mod
            if (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.f2Key.wasPressedThisFrame || Keyboard.current.f3Key.wasPressedThisFrame || Keyboard.current.f5Key.wasPressedThisFrame)
            {
                isMouseLookSuspended = !isMouseLookSuspended;
            }

            // LOOK CONTROLS
            // Only allow looking if menu is closed AND not suspended
            if (!isMenuOpen && !isMouseLookSuspended)
            {
                bool isLookingAround = Mouse.current.leftButton.isPressed;
                
                if (isLookingAround)
                {
                    if (Cursor.lockState != CursorLockMode.Locked)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    
                    var mouse = Mouse.current;
                    Vector2 mouseDelta = mouse.delta.ReadValue();
                    
                    rotationX += mouseDelta.x * mouseSensitivity * 0.1f;
                    rotationY -= mouseDelta.y * mouseSensitivity * 0.1f;
                    rotationY = Mathf.Clamp(rotationY, -90f, 90f);
                    
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0f);
                        cam.transform.rotation = rotation;
                        
                        // Sync rig head rotation
                        if (tagger.offlineVRRig.head.rigTarget)
                            tagger.offlineVRRig.head.rigTarget.rotation = rotation;
                    }
                }
                else
                {
                    if (Cursor.lockState != CursorLockMode.None)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
            else if (isMouseLookSuspended)
            {
                 // If suspended, ensure cursor is free
                 if (Cursor.lockState != CursorLockMode.None)
                 {
                     Cursor.lockState = CursorLockMode.None;
                     Cursor.visible = true;
                 }
            }

            // SYNC CONTROL (T)
            // SYNC or CREEPY AUDIO (T)
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (currentMode == WalkMode.Ghost)
                {
                    StartCoroutine(PlayCreepySequence());
                }
                else if (tagger.offlineVRRig.head.rigTarget)
                {
                    player.TeleportTo(tagger.offlineVRRig.head.rigTarget.position, tagger.offlineVRRig.head.rigTarget.rotation);
                }
            }

            // MOVEMENT Logic
            if (!isClimbing)
            {
                HandleMovement(rigidbody);
            }
            else
            {
                HandleClimb(rigidbody);
            }
            
            // ANIMATION Logic
            HandleAnimation(tagger, rigidbody);
        }

        private void HandleWaterModeSwitch()
        {
            if (!GTPlayer.hasInstance) return;
            
            bool isInWater = GTPlayer.Instance.InWater;
            
            // Entering water - switch to swimming
            if (isInWater && !wasInWater)
            {
                // Store the current mode if it's not already swimming
                if (currentMode != WalkMode.Swimming)
                {
                    modeBeforeWater = currentMode;
                    currentMode = WalkMode.Swimming;
                    Debug.Log("[WalkSim] Entered water - switching to Swimming mode");
                }
            }
            // Exiting water - switch back to previous mode
            else if (!isInWater && wasInWater)
            {
                // Only switch back if we're currently in swimming mode
                if (currentMode == WalkMode.Swimming)
                {
                    currentMode = modeBeforeWater;
                    Debug.Log($"[WalkSim] Exited water - switching to {modeBeforeWater} mode");
                }
            }
            
            wasInWater = isInWater;
        }

        private void HandleMovement(Rigidbody rigidbody)
        {
            // Arrow Key Speed Adjustment
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                walkSpeed = Mathf.Min(walkSpeed + 1f, 20f);
                if (runSpeed < walkSpeed) runSpeed = walkSpeed * 2f;
            }
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                walkSpeed = Mathf.Max(walkSpeed - 1f, 1f);
            }

            Vector3 moveInput = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) moveInput += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) moveInput += Vector3.back;
            if (Keyboard.current.aKey.isPressed) moveInput += Vector3.left;
            if (Keyboard.current.dKey.isPressed) moveInput += Vector3.right;

            if (moveInput.magnitude > 0.1f)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camForward = cam.transform.forward;
                    Vector3 camRight = cam.transform.right;
                    camForward.y = 0;
                    camRight.y = 0;
                    camForward.Normalize();
                    camRight.Normalize();

                    Vector3 moveDirection = (camForward * moveInput.z + camRight * moveInput.x).normalized;
                    
                    bool isCrouching = Keyboard.current.cKey.isPressed;
                    bool isSprinting = Keyboard.current.leftShiftKey.isPressed;

                    float speed = isCrouching ? walkSpeed * 0.5f : (isSprinting ? runSpeed : walkSpeed);
                    
                    // Reduce speed when swimming in water
                    if (GTPlayer.hasInstance && GTPlayer.Instance.InWater)
                    {
                        speed *= 0.6f; // Swimming is slower due to water resistance
                    }
                    
                    Vector3 targetVelocity = moveDirection * speed;
                    
                    // Height Adjustment
                    float currentLift = bodyLiftHeight;
                    if (isCrouching) currentLift -= 0.4f; // Crouch drops height
                    
                    float desiredDist = 0.35f + currentLift; 
                    if (desiredDist < 0.2f) desiredDist = 0.2f; // Minimum floor clearance

                    int layerMask = (1 << 0) | (1 << 9);

                    // Check deeper when crouching to avoid snapping
                    float rayDist = desiredDist + 2f;

                    if (Physics.Raycast(rigidbody.position, Vector3.down, out RaycastHit hitInfo, rayDist, layerMask))
                    {
                        float currentDist = hitInfo.distance;
                        if (currentDist < desiredDist)
                        {
                            // Apply lift to maintain height
                            float liftForce = (desiredDist - currentDist) * 20f; 
                            targetVelocity.y = liftForce;
                        }
                        else
                        {
                             targetVelocity.y = rigidbody.velocity.y;
                        }
                    }
                    else
                    {
                        targetVelocity.y = rigidbody.velocity.y;
                    }

                    // Check for Climbing (Only if moving forward)
                    if (moveInput.magnitude > 0.1f && !isClimbing)
                    {
                        CheckForClimb(rigidbody, moveInput);
                    }

                    rigidbody.velocity = Vector3.Lerp(rigidbody.velocity, targetVelocity, Time.deltaTime * 10f);
                }
            }

            // Jump
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (Physics.Raycast(rigidbody.position, Vector3.down, 0.5f))
                {
                    rigidbody.velocity = new Vector3(rigidbody.velocity.x, 7f, rigidbody.velocity.z);
                }
            }
        }

        private void CheckForClimb(Rigidbody rb, Vector3 moveInput)
        {
             // Direction we are moving
             var cam = Camera.main;
             if(cam == null) return;
             
             Vector3 camForward = cam.transform.forward; camForward.y = 0; camForward.Normalize();
             Vector3 dir = (camForward * moveInput.z + cam.transform.right * moveInput.x).normalized;

             int layerMask = (1 << 0) | (1 << 9);

             // 1. Check for wall in front (Waist height)
             // Start slightly below head
             Vector3 startOrigin = rb.position + Vector3.up * 0.5f; 
             
             if (Physics.Raycast(startOrigin, dir, out RaycastHit wallHit, 1.0f, layerMask))
             {
                 // 2. Check for empty space above wall (Simulate "Reach")
                 // Iterate multiple depths to catch thin walls or irregular shapes
                 float[] checkDepths = new float[] { 0.05f, 0.15f, 0.3f };
                 
                 foreach(float depth in checkDepths)
                 {
                     Vector3 reachPoint = wallHit.point + dir * depth + Vector3.up * 1.5f; // Up and slightly forward
                     
                     // Raycast DOWN from reach point to find top of obstacle
                     if (Physics.Raycast(reachPoint, Vector3.down, out RaycastHit topHit, 2.0f, layerMask))
                     {
                         // Ensure top is reachable
                         float heightDiff = topHit.point.y - rb.position.y;
                         if (heightDiff > 0.5f && heightDiff <= maxClimbHeight)
                         {
                             // Found a climbable ledge at this depth!
                             InitiateClimb(rb, topHit.point, dir);
                             return; // Stop searching once found
                         }
                     }
                 }
             }
        }

        private void InitiateClimb(Rigidbody rb, Vector3 ledgePoint, Vector3 direction)
        {
            isClimbing = true;
            climbStartTime = Time.time;
            climbStartPos = rb.position;
            
            // Target Position logic:
            // ledgePoint is the surface coordinate.
            // We want the bottoms of our "feet" (pseudo-feet) to be on that surface.
            // In Gorilla Tag, the "feet" are roughly where the body pivot is IF standing straight with bodyLift=0.
            // But with bodyLift, we float higher.
            
            // Base offset from ground to center-of-mass/camera pivot usually ~0.35 to 0.5.
            // So we take the Ledge Y + Base Offset + Body Lift.
            
            float baseStandingHeight = 0.35f; 
            
            // Check for Forward Obstacles before pushing
            float forwardOffset = 0.3f;
            int layerMask = (1 << 0) | (1 << 9);
            Vector3 landingCheckOrigin = ledgePoint + Vector3.up * 0.2f; 
            
            if (Physics.Raycast(landingCheckOrigin, direction, 0.5f, layerMask))
            {
                forwardOffset = 0f; // Blocked! Don't push forward.
            }

            climbTargetPos = ledgePoint + Vector3.up * (baseStandingHeight + bodyLiftHeight) + direction * forwardOffset;
            
            // Set Hand targets for animation sim
            // Set Hand targets for animation sim
            climbHandLeft = ledgePoint + Vector3.left * 0.2f;
            climbHandRight = ledgePoint + Vector3.right * 0.2f;

             // Capture current hand pos for smooth transition
             if (GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget != null)
                 handStartLeft = GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget.position;
             else
                 handStartLeft = climbHandLeft;

             if (GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget != null)
                 handStartRight = GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget.position;
             else
                 handStartRight = climbHandRight;
        }

        private void HandleClimb(Rigidbody rb)
        {
            float elapsed = Time.time - climbStartTime;
            float t = elapsed / climbDuration;

            if (t >= 1.0f)
            {
                isClimbing = false;
                rb.velocity = Vector3.zero;
                return;
            }
            
            // Smoother Sine-Curve Interpolation
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Calculate Target Position
            Vector3 currentPos;
            
            // Add a slight arc to Y
            float heightCurve = Mathf.Sin(t * Mathf.PI) * 0.2f; 
            
            Vector3 interpolatedPos = Vector3.Lerp(climbStartPos, climbTargetPos, smoothT);
            interpolatedPos.y += heightCurve;

            // Drive via Velocity for smoothness
            Vector3 neededVel = (interpolatedPos - rb.position) / Time.deltaTime;
            
            // Clamp velocity to avoid explosions
            if(neededVel.magnitude > 20f) neededVel = neededVel.normalized * 20f;
            
            rb.velocity = neededVel;
        }

        private void HandleAnimation(GorillaTagger tagger, Rigidbody rb)
        {
            var vrRig = tagger.offlineVRRig;
            if (vrRig == null || vrRig.leftHand.rigTarget == null || vrRig.rightHand.rigTarget == null) return;

            if (isClimbing)
            {
                 // Procedural Vault Animation
                 float elapsed = Time.time - climbStartTime;
                 float handT = Mathf.Clamp01(elapsed / 0.15f); // Hands snap quickly but smoothly (0.15s)
                 
                 // Hands grapple the ledge
                 vrRig.leftHand.rigTarget.position = Vector3.Lerp(handStartLeft, climbHandLeft, handT);
                 vrRig.rightHand.rigTarget.position = Vector3.Lerp(handStartRight, climbHandRight, handT);
                 
                 // Rotate hands to grip
                 vrRig.leftHand.rigTarget.rotation = Quaternion.Euler(90, 0, 0);
                 vrRig.rightHand.rigTarget.rotation = Quaternion.Euler(90, 0, 0);
                 return;
            }

            Vector3 horizVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            float speed = horizVel.magnitude;

            if (speed > 0.1f)
            {
                walkCycle += Time.deltaTime * speed * 3.0f; // Faster cycling to match speed
            }
            else
            {
                // Reset slightly towards constant stance if stopped? 
                // Alternatively, just pause cycle or lerp to 0. 
                // For Gorilla stepping, pausing cycle is often more natural, 
                // but let's reset for clean idle state.
                walkCycle = Mathf.Lerp(walkCycle, 0f, Time.deltaTime * 5f);
            }

            if (currentMode == WalkMode.Gorilla)
            {
                AnimateGorilla(vrRig, speed);
            }
            else if (currentMode == WalkMode.Silly)
            {
                AnimateSilly(vrRig, speed);
            }
            else if (currentMode == WalkMode.Ghost)
            {
                AnimateGhost(vrRig, speed);
            }
            else if (currentMode == WalkMode.Swimming)
            {
                AnimateSwimming(vrRig, speed);
            }
        }

        private void AnimateGorilla(VRRig vrRig, float speed)
        {
            Transform leftHand = vrRig.leftHand.rigTarget;
            Transform rightHand = vrRig.rightHand.rigTarget;
            Transform body = vrRig.transform;

            int layerMask = (1 << 0) | (1 << 9);

            // Stride and Height
            float stride = 0.7f; // Long stride
            float stepHeight = 0.35f;
            
            // ----------------------------------------------------------------
            // LEFT HAND
            // ----------------------------------------------------------------
            float tLeft = (walkCycle % (Mathf.PI * 2)) / (Mathf.PI * 2);
            bool isLeftPlanting = (tLeft >= 0.5f);
            
            // Ideal rest position
            Vector3 idealLeftFooting = body.position + body.right * -0.3f + body.forward * 0.4f;
            float finalLeftY = body.position.y - 1.5f; 
            
            if (Physics.Raycast(idealLeftFooting + Vector3.up * 1f, Vector3.down, out RaycastHit hitL, 4f + bodyLiftHeight, layerMask))
                finalLeftY = hitL.point.y;
            else
                finalLeftY = body.position.y - 0.8f - bodyLiftHeight;
                
            idealLeftFooting.y = finalLeftY;

            if (speed > 0.05f)
            {
                if (isLeftPlanting)
                {
                    if (!wasLeftPlanted)
                    {
                        // Plant WAY forward
                        Vector3 landSpot = body.position + body.right * -0.35f + body.forward * 0.8f;
                        if (Physics.Raycast(landSpot + Vector3.up * 1f, Vector3.down, out RaycastHit hitLand, 3f + bodyLiftHeight, layerMask))
                            leftHandPlantPos = hitLand.point;
                        else
                            leftHandPlantPos = landSpot + Vector3.down * (0.9f + bodyLiftHeight);
                    }

                    // Strict drag limit
                    float maxDrag = 0.85f + bodyLiftHeight;
                    if (Vector3.Distance(leftHandPlantPos, body.position) > maxDrag)
                    {
                         leftHandPlantPos = Vector3.Lerp(leftHandPlantPos, idealLeftFooting, Time.deltaTime * 30f);
                    }
                    
                    leftHand.position = leftHandPlantPos;
                }
                else // Swinging
                {
                    float swingT = tLeft * 2f; 
                    
                    Vector3 startPos = leftHandPlantPos;
                    if (startPos == Vector3.zero) startPos = idealLeftFooting;

                    float arc = Mathf.Sin(swingT * Mathf.PI) * stepHeight;
                    
                    Vector3 currentInterp = Vector3.Lerp(startPos, idealLeftFooting, swingT);
                    currentInterp.y += arc;
                    
                    float baseHeight = Mathf.Lerp(startPos.y, finalLeftY, swingT);
                    if (currentInterp.y < baseHeight) currentInterp.y = baseHeight;

                    leftHand.position = currentInterp;
                }
            }
            else
            {
                // Idle
                leftHandPlantPos = Vector3.Lerp(leftHandPlantPos, idealLeftFooting, Time.deltaTime * 10f);
                leftHand.position = leftHandPlantPos;
            }
            wasLeftPlanted = isLeftPlanting;

            // ----------------------------------------------------------------
            // RIGHT HAND
            // ----------------------------------------------------------------
            float tRight = ((walkCycle + Mathf.PI) % (Mathf.PI * 2)) / (Mathf.PI * 2);
            bool isRightPlanting = (tRight >= 0.5f);
            
            Vector3 idealRightFooting = body.position + body.right * 0.3f + body.forward * 0.4f;
            float finalRightY = body.position.y - 1.5f;
            
            if (Physics.Raycast(idealRightFooting + Vector3.up * 1f, Vector3.down, out RaycastHit hitR, 4f + bodyLiftHeight, layerMask))
                finalRightY = hitR.point.y;
            else
                finalRightY = body.position.y - 0.8f - bodyLiftHeight;
                
            idealRightFooting.y = finalRightY;

            if (speed > 0.05f)
            {
                if (isRightPlanting)
                {
                    if (!wasRightPlanted)
                    {
                        Vector3 landSpot = body.position + body.right * 0.35f + body.forward * 0.8f;
                        if (Physics.Raycast(landSpot + Vector3.up * 1f, Vector3.down, out RaycastHit hitLand, 3f + bodyLiftHeight, layerMask))
                            rightHandPlantPos = hitLand.point;
                        else
                            rightHandPlantPos = landSpot + Vector3.down * (0.9f + bodyLiftHeight);
                    }

                    float maxDrag = 0.85f + bodyLiftHeight;
                    if (Vector3.Distance(rightHandPlantPos, body.position) > maxDrag)
                    {
                         rightHandPlantPos = Vector3.Lerp(rightHandPlantPos, idealRightFooting, Time.deltaTime * 30f);
                    }

                    rightHand.position = rightHandPlantPos;
                }
                else
                {
                    float swingT = tRight * 2f;
                    Vector3 startPos = rightHandPlantPos;
                    if (startPos == Vector3.zero) startPos = idealRightFooting;

                    float arc = Mathf.Sin(swingT * Mathf.PI) * stepHeight;
                    Vector3 currentInterp = Vector3.Lerp(startPos, idealRightFooting, swingT);
                    currentInterp.y += arc;
                    
                    float baseHeight = Mathf.Lerp(startPos.y, finalRightY, swingT);
                    if (currentInterp.y < baseHeight) currentInterp.y = baseHeight;

                    rightHand.position = currentInterp;
                }
            }
            else
            {
                rightHandPlantPos = Vector3.Lerp(rightHandPlantPos, idealRightFooting, Time.deltaTime * 10f);
                rightHand.position = rightHandPlantPos;
            }
            wasRightPlanted = isRightPlanting;

            // ----------------------------------------------------------------
            // ROTATION FIX
            // ----------------------------------------------------------------
            // 180 was "Down and Side". 
            // Adding 90 deg Y rotation to correct "Side" to "Forward".
            leftHand.rotation = vrRig.transform.rotation * Quaternion.Euler(180f, 90f, 0f);
            rightHand.rotation = vrRig.transform.rotation * Quaternion.Euler(180f, -90f, 0f);
        }

        private void AnimateSilly(VRRig vrRig, float speed)
        {
            Transform leftHand = vrRig.leftHand.rigTarget;
            Transform rightHand = vrRig.rightHand.rigTarget;
            
            float swingAmount = Mathf.Sin(walkCycle) * 0.3f;
            Vector3 bodyPos = vrRig.transform.position;
            Vector3 bodyForward = vrRig.transform.forward;
            Vector3 bodyRight = vrRig.transform.right;
            
            float handHeight = -0.3f;
            Vector3 leftBase = bodyPos + bodyRight * -0.3f + Vector3.up * handHeight;
            Vector3 rightBase = bodyPos + bodyRight * 0.3f + Vector3.up * handHeight;
            
            if (speed > 0.1f)
            {
                leftHand.position = leftBase + bodyForward * swingAmount;
                rightHand.position = rightBase + bodyForward * -swingAmount;
                
                float rot = swingAmount * 30f;
                leftHand.rotation = Quaternion.Euler(-rot, 0, 0) * vrRig.transform.rotation;
                rightHand.rotation = Quaternion.Euler(rot, 0, 0) * vrRig.transform.rotation;
            }
            else
            {
                leftHand.position = leftBase;
                rightHand.position = rightBase;
                leftHand.rotation = vrRig.transform.rotation;
                rightHand.rotation = vrRig.transform.rotation;
            }
        }

        private void AnimateGhost(VRRig vrRig, float speed)
        {
            // "Ghost" is laggy and machine-like.
            
            // Only calculate new positions if it's time for a "frame"
            if (Time.time < nextGhostUpdate)
            {
                // Strict Snap (Maintain previous position)
                vrRig.leftHand.rigTarget.position = ghostLeftPos;
                vrRig.leftHand.rigTarget.rotation = ghostLeftRot;
                vrRig.rightHand.rigTarget.position = ghostRightPos;
                vrRig.rightHand.rigTarget.rotation = ghostRightRot;
                return;
            }

            // Set next update time (randomize FPS for uneasiness)
            ghostFps = UnityEngine.Random.Range(4f, 12f);
            nextGhostUpdate = Time.time + (1f / ghostFps);

            Transform leftHand = vrRig.leftHand.rigTarget;
            Transform rightHand = vrRig.rightHand.rigTarget;
            Transform body = vrRig.transform;

            int layerMask = (1 << 0) | (1 << 9);

            float stride = 0.5f; 
            float stepHeight = 0.2f; 
            
            // Logic similar to Gorilla but "Snap" calculated
            // ----------------------------------------------------------------
            // LEFT HAND
            // ----------------------------------------------------------------
            float tLeft = (walkCycle % (Mathf.PI * 2)) / (Mathf.PI * 2);
            bool isLeftPlanting = (tLeft >= 0.5f);
            
            Vector3 idealLeftFooting = body.position + body.right * -0.25f + body.forward * 0.3f;
            float finalLeftY = body.position.y - 1.5f; 
            
            if (Physics.Raycast(idealLeftFooting + Vector3.up * 1f, Vector3.down, out RaycastHit hitL, 4f, layerMask))
                finalLeftY = hitL.point.y;
            else
                finalLeftY = body.position.y - 0.8f;
                
            idealLeftFooting.y = finalLeftY;

            Vector3 targetL = leftHand.position; // fallback

            if (speed > 0.05f)
            {
                if (isLeftPlanting)
                {
                    if (!wasLeftPlanted)
                    {
                        Vector3 landSpot = body.position + body.right * -0.25f + body.forward * 0.6f;
                        if (Physics.Raycast(landSpot + Vector3.up * 1f, Vector3.down, out RaycastHit hitLand, 3f, layerMask))
                            leftHandPlantPos = hitLand.point;
                        else
                            leftHandPlantPos = landSpot + Vector3.down * 0.9f;
                    }

                    if (Vector3.Distance(leftHandPlantPos, body.position) > 0.85f)
                    {
                         leftHandPlantPos = Vector3.Lerp(leftHandPlantPos, idealLeftFooting, Time.deltaTime * 30f);
                    }
                    targetL = leftHandPlantPos;
                }
                else
                {
                    float swingT = tLeft * 2f; 
                    Vector3 startPos = leftHandPlantPos;
                    if (startPos == Vector3.zero) startPos = idealLeftFooting;

                    float arc = Mathf.Sin(swingT * Mathf.PI) * stepHeight;
                    Vector3 currentInterp = Vector3.Lerp(startPos, idealLeftFooting, swingT);
                    currentInterp.y += arc;
                    
                    float baseHeight = Mathf.Lerp(startPos.y, finalLeftY, swingT);
                    if (currentInterp.y < baseHeight) currentInterp.y = baseHeight;

                    targetL = currentInterp;
                }
            }
            else
            {
                targetL = idealLeftFooting;
            }
            wasLeftPlanted = isLeftPlanting;

            // ----------------------------------------------------------------
            // RIGHT HAND
            // ----------------------------------------------------------------
            float tRight = ((walkCycle + Mathf.PI) % (Mathf.PI * 2)) / (Mathf.PI * 2);
            bool isRightPlanting = (tRight >= 0.5f);
            
            Vector3 idealRightFooting = body.position + body.right * 0.25f + body.forward * 0.3f;
            float finalRightY = body.position.y - 1.5f;
            
            if (Physics.Raycast(idealRightFooting + Vector3.up * 1f, Vector3.down, out RaycastHit hitR, 4f, layerMask))
                finalRightY = hitR.point.y;
            else
                finalRightY = body.position.y - 0.8f;
                
            idealRightFooting.y = finalRightY;

            Vector3 targetR = rightHand.position;

            if (speed > 0.05f)
            {
                if (isRightPlanting)
                {
                    if (!wasRightPlanted)
                    {
                        Vector3 landSpot = body.position + body.right * 0.25f + body.forward * 0.6f;
                        if (Physics.Raycast(landSpot + Vector3.up * 1f, Vector3.down, out RaycastHit hitLand, 3f, layerMask))
                            rightHandPlantPos = hitLand.point;
                        else
                            rightHandPlantPos = landSpot + Vector3.down * 0.9f;
                    }

                    if (Vector3.Distance(rightHandPlantPos, body.position) > 0.85f)
                    {
                         rightHandPlantPos = Vector3.Lerp(rightHandPlantPos, idealRightFooting, Time.deltaTime * 30f);
                    }
                    targetR = rightHandPlantPos;
                }
                else
                {
                    float swingT = tRight * 2f;
                    Vector3 startPos = rightHandPlantPos;
                    if (startPos == Vector3.zero) startPos = idealRightFooting;

                    float arc = Mathf.Sin(swingT * Mathf.PI) * stepHeight;
                    Vector3 currentInterp = Vector3.Lerp(startPos, idealRightFooting, swingT);
                    currentInterp.y += arc;
                    
                    float baseHeight = Mathf.Lerp(startPos.y, finalRightY, swingT);
                    if (currentInterp.y < baseHeight) currentInterp.y = baseHeight;

                    targetR = currentInterp;
                }
            }
            else
            {
                targetR = idealRightFooting;
            }
            wasRightPlanted = isRightPlanting;

            // ROTATION calc
            Quaternion baseRotL = vrRig.transform.rotation * Quaternion.Euler(180f, 90f, 0f);
            Quaternion baseRotR = vrRig.transform.rotation * Quaternion.Euler(180f, -90f, 0f);

            ghostLeftRot = baseRotL * Quaternion.Euler(UnityEngine.Random.Range(-5,5), UnityEngine.Random.Range(-5,5), 0);
            ghostRightRot = baseRotR * Quaternion.Euler(UnityEngine.Random.Range(-5,5), UnityEngine.Random.Range(-5,5), 0);

            // Jitter Pos
            targetL += UnityEngine.Random.insideUnitSphere * 0.04f;
            targetR += UnityEngine.Random.insideUnitSphere * 0.04f;

            ghostLeftPos = targetL;
            ghostRightPos = targetR;

            // Apply immediately this frame
            leftHand.position = ghostLeftPos;
            leftHand.rotation = ghostLeftRot;
            rightHand.position = ghostRightPos;
            rightHand.rotation = ghostRightRot;
        }

        private void AnimateSwimming(VRRig vrRig, float speed)
        {
            Transform leftHand = vrRig.leftHand.rigTarget;
            Transform rightHand = vrRig.rightHand.rigTarget;
            Transform body = vrRig.transform;
            Transform head = vrRig.head.rigTarget;

            // Advance swim cycle - continuous even when idle for treading water effect
            float cycleSpeed = speed > 0.1f ? 3.0f : 1.5f; // Faster when moving
            swimCycle += Time.deltaTime * cycleSpeed;
            
            // Use head position as base since that's where the camera/player actually is
            Vector3 headPos = head != null ? head.position : body.position;
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : body.forward;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : body.right;
            
            // Flatten forward/right for horizontal swimming
            forward.y = 0;
            forward.Normalize();
            right.y = 0;
            right.Normalize();

            // BREASTSTROKE ANIMATION - Both arms move together
            // Cycle: 0-1 normalized
            float cycle = (swimCycle % (Mathf.PI * 2)) / (Mathf.PI * 2); // 0 to 1
            
            // Swimming stroke phases for BREASTSTROKE:
            // 0.0 - 0.3: REACH - Arms extend forward, staying WIDE apart
            // 0.3 - 0.5: CATCH - Arms spread even wider outward
            // 0.5 - 0.8: PULL - Arms sweep back, staying wide
            // 0.8 - 1.0: RECOVERY - Arms swing forward, staying wide
            
            float forwardReach;  // How far forward (positive = in front of player)
            float sideSpread;    // How far to the side - extended 5x!
            float verticalPos;   // Height relative to head
            
            if (cycle < 0.3f)
            {
                // REACH PHASE: Arms extend forward, staying wide apart
                float t = cycle / 0.3f;
                forwardReach = Mathf.Lerp(1.0f, 3.0f, t);    // 5x: was 0.2-0.6
                sideSpread = Mathf.Lerp(3.5f, 4.0f, t);      // 5x: was 0.7-0.8
                verticalPos = Mathf.Lerp(-2.0f, -1.0f, t);   // 5x: was -0.4 to -0.2
            }
            else if (cycle < 0.5f)
            {
                // CATCH PHASE: Arms spread even wider
                float t = (cycle - 0.3f) / 0.2f;
                forwardReach = Mathf.Lerp(3.0f, 2.0f, t);    // 5x: was 0.6-0.4
                sideSpread = Mathf.Lerp(4.0f, 5.5f, t);      // 5x: was 0.8-1.1
                verticalPos = Mathf.Lerp(-1.0f, -1.5f, t);   // 5x: was -0.2 to -0.3
            }
            else if (cycle < 0.8f)
            {
                // PULL PHASE: Arms sweep back, staying wide
                float t = (cycle - 0.5f) / 0.3f;
                forwardReach = Mathf.Lerp(2.0f, -1.5f, t);   // 5x: was 0.4 to -0.3
                sideSpread = Mathf.Lerp(5.5f, 4.0f, t);      // 5x: was 1.1-0.8
                verticalPos = Mathf.Lerp(-1.5f, -2.5f, t);   // 5x: was -0.3 to -0.5
            }
            else
            {
                // RECOVERY PHASE: Arms swing forward, staying wide
                float t = (cycle - 0.8f) / 0.2f;
                forwardReach = Mathf.Lerp(-1.5f, 1.0f, t);   // 5x: was -0.3 to 0.2
                sideSpread = Mathf.Lerp(4.0f, 3.5f, t);      // 5x: was 0.8-0.7
                verticalPos = Mathf.Lerp(-2.5f, -2.0f, t);   // 5x: was -0.5 to -0.4
            }
            
            // Calculate hand positions relative to HEAD (not body center)
            Vector3 leftTarget = headPos 
                + forward * forwardReach 
                - right * sideSpread 
                + Vector3.up * verticalPos;
                
            Vector3 rightTarget = headPos 
                + forward * forwardReach 
                + right * sideSpread 
                + Vector3.up * verticalPos;
            
            // Calculate hand rotations - palms facing down/back for pushing water
            Quaternion leftRot, rightRot;
            if (cycle < 0.5f)
            {
                // Reaching/catching - palms angled down and outward
                leftRot = Quaternion.LookRotation(forward + Vector3.down * 0.5f, Vector3.up) * Quaternion.Euler(0, 90, 0);
                rightRot = Quaternion.LookRotation(forward + Vector3.down * 0.5f, Vector3.up) * Quaternion.Euler(0, -90, 0);
            }
            else
            {
                // Pulling - palms facing backward to push water
                leftRot = Quaternion.LookRotation(-forward + Vector3.down * 0.3f, Vector3.up) * Quaternion.Euler(0, 90, 0);
                rightRot = Quaternion.LookRotation(-forward + Vector3.down * 0.3f, Vector3.up) * Quaternion.Euler(0, -90, 0);
            }

            // Apply with smooth interpolation
            float smoothing = 15f * Time.deltaTime;
            leftHand.position = Vector3.Lerp(leftHand.position, leftTarget, smoothing);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, leftRot, smoothing);
            
            rightHand.position = Vector3.Lerp(rightHand.position, rightTarget, smoothing);
            rightHand.rotation = Quaternion.Slerp(rightHand.rotation, rightRot, smoothing);
        }

        // --- AUDIO COMPONENT ---
        private System.Collections.IEnumerator PlayCreepySequence()
        {
             // Sequence:
             // 1 1 1 1 (High, Fast)
             // Wait 5-7s
             // 0 (Low)
             // Wait 3s
             // 00000 (Low, Fast)

             // 1 = High (e.g., 880Hz)
             // 0 = Low (e.g., 200Hz)
             
             AudioClip highBeep = MakeTone(1000f, 0.15f);
             AudioClip lowBeep  = MakeTone(200f,  0.4f);

             // Create Full Sequence Clip
             // 4x High (0.15s) + 0.25s gap = 4 * 0.4 = 1.6s
             // Wait 6s
             // 1x Low (0.4s)
             // Wait 3s
             // 5x Low (0.4s) + 0.6s gap = 5*1.0? No, wait logic was:
             // 1 1 1 1 (High, yield 0.25) -> 0.15 duration, 0.25 wait = 0.1s gap
             // Wait 5-7s
             // 0 (Low)
             // Wait 3s
             // 00000 (Low, yield 0.6) -> 0.4 duration, 0.6 wait = 0.2s gap
             
             // To inject into Photon Voice properly as a clip, we should pre-bake the whole thing into one AudioClip.
             
             float totDuration = 0f;
             // Part 1: 4 highs
             totDuration += 4 * 0.4f; 
             // Part 2: Wait
             totDuration += 6.0f; 
             // Part 3: 1 low
             totDuration += 0.4f; 
             // Part 4: Wait
             totDuration += 3.0f;
             // Part 5: 5 low
             totDuration += 5 * 0.6f;
             
             // Add a buffer
             totDuration += 1.0f;
             
             int sampleRate = 44100;
             int lengthSamples = (int)(totDuration * sampleRate);
             float[] fullSequence = new float[lengthSamples];
             
             int cursor = 0;
             
             void AddClip(AudioClip clip, float waitTime)
             {
                 float[] data = new float[clip.samples];
                 clip.GetData(data, 0);
                 for(int i=0; i<data.Length && (cursor+i)<lengthSamples; i++)
                 {
                     fullSequence[cursor+i] = data[i];
                 }
                 cursor += (int)(waitTime * sampleRate);
             }
             
             // 1 1 1 1
             for(int i=0; i<4; i++) AddClip(highBeep, 0.4f);
             
             // Wait 6s
             cursor += (int)(6.0f * sampleRate);
             
             // 0
             AddClip(lowBeep, 0.0f); // just clip length effectively
             cursor += (int)(lowBeep.length * sampleRate); // Move past clip
             
             // Wait 3s
             cursor += (int)(3.0f * sampleRate);
             
             // 0 0 0 0 0
             for(int i=0; i<5; i++) AddClip(lowBeep, 0.6f);
             
             AudioClip finalClip = AudioClip.Create("CreepySeq", lengthSamples, 1, sampleRate, false);
             finalClip.SetData(fullSequence, 0);

             // INJECT INTO PHOTON MIC
             // Try to find the local recorder
             
             Recorder recorder = null;
             
             // 1. Try PhotonVoiceNetwork (Preferred)
             if (PhotonVoiceNetwork.Instance != null && PhotonVoiceNetwork.Instance.PrimaryRecorder != null)
             {
                 recorder = PhotonVoiceNetwork.Instance.PrimaryRecorder;
             }
             // 2. Fallback to searching
             else 
             {
                 var recorders = GameObject.FindObjectsOfType<Recorder>();
                 foreach(var r in recorders)
                 {
                      // Check if valid?
                      if (r != null) { recorder = r; break; } // Pick first one found if we can't check ownership easily
                 }
             }
             
             if (recorder != null)
             {
                 Debug.Log("[WalkSim] Found Recorder, Injecting Audio...");
                 
                 // Force transmit if not enabled
                 bool wasTransmitting = recorder.TransmitEnabled;
                 recorder.TransmitEnabled = true;
                 
                 var oldSource = recorder.SourceType;
                 var oldClip   = recorder.AudioClip;
                 var oldLoop   = recorder.LoopAudioClip;
                 
                 recorder.SourceType = Recorder.InputSourceType.AudioClip;
                 recorder.AudioClip = finalClip;
                 recorder.LoopAudioClip = false;
                 
                 // Critical: Restart to apply source change
                 recorder.RestartRecording();
                 
                 // Also play locally for me (spatial)
                 AudioSource.PlayClipAtPoint(finalClip, GorillaTagger.Instance.headCollider.transform.position);

                 yield return new WaitForSeconds(totDuration);
                 
                 // Restore
                 recorder.SourceType = oldSource;
                 recorder.AudioClip = oldClip;
                 recorder.LoopAudioClip = oldLoop;
                 recorder.TransmitEnabled = wasTransmitting;
                 
                 recorder.RestartRecording();
             }
             else
             {
                 Debug.Log("[WalkSim] Recorder NOT found. Playing locally only.");
                 AudioSource.PlayClipAtPoint(finalClip, GorillaTagger.Instance.headCollider.transform.position);
                 yield return new WaitForSeconds(totDuration);
             }
        }

        private AudioClip MakeTone(float frequency, float length)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * length);
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate);
            }
            
            AudioClip clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }

    // Patch ControllerInputPoller.LateUpdate to fake controller input
    [HarmonyPatch(typeof(ControllerInputPoller), "LateUpdate")]
    public class ControllerPatch
    {
        static void Postfix()
        {
            // This makes the game think we're holding controllers
            // so movement doesn't break
        }
    }
}
