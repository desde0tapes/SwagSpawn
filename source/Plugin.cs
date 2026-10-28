using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using UnityEngine;
using Reptile;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;

namespace SwagSpawn
{
    [BepInPlugin("com.desdezerotapes.swagspawn", "Swag Spawn", "7.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        private string _baseBundlesFolder;
        private Harmony _harmony;

        // Enums
        private enum EffectCategory
        {
            TrickGlow,   // Replaces the default effect category
            Boost,
            BoostTrick,
            Static,
            MiniBoost,
            Manual,
            ObjectSpawner, // Object Spawner
            Static2,       // Static 2
            Static3,       // Static 3
            MoveStyle,     // MoveStyle
            MoveStyleBoost, // MoveStyle Boost
            Manual2,       // Manual Toggle 2
            Phone,         // Phone
            HyperBoost,    // HyperBoost
            HyperCharging  // HyperCharging
        }

        // Data
        private Dictionary<GameObject, EffectCategory> _prefabCategoryMap = new Dictionary<GameObject, EffectCategory>();
        private List<GameObject> _activePuppets = new List<GameObject>();
        private Dictionary<GameObject, EffectCategory> _puppetCategoryMap = new Dictionary<GameObject, EffectCategory>();
        private Dictionary<GameObject, bool> _puppetStateCache = new Dictionary<GameObject, bool>();
        private Dictionary<Transform, Transform> _boneMap = new Dictionary<Transform, Transform>();
        private Dictionary<Transform, EffectCategory> _boneCategoryMap = new Dictionary<Transform, EffectCategory>();

        // MainCamera tracking
        private List<Transform> _mainCameraFollowers = new List<Transform>();

        // Reflection lookups
        private FieldInfo _characterVisualField;
        private FieldInfo _abilityField;
        private FieldInfo _boostingField;
        private MethodInfo _checkBoostTrickMethod;
        private FieldInfo _moveStyleField;

        // Mini Boost reflection fields
        private FieldInfo _isJumpingField;
        private FieldInfo _timeSinceLastJumpField;
        private FieldInfo _grindTrickTimerField;
        private FieldInfo _slideStateField; // Slide state lookup

        // Boost-trick reflection field
        private FieldInfo _curTrickBoostField;

        // Phone-state reflection field
        private FieldInfo _phoneActiveField;

        // Configs
        // 1. General
        public ConfigEntry<bool> IsEnabled;
        public ConfigEntry<string> EffectsBundlesLocation;

        // 1.5 Trick Glow
        public ConfigEntry<bool> TrickGlowEnabled;
        public ConfigEntry<string> TrickGlowFolderName;
        public ConfigEntry<float> TrickGlowDelay;
        public ConfigEntry<float> TrickGlowRotX, TrickGlowRotY, TrickGlowRotZ;
        public ConfigEntry<float> TrickGlowPosX, TrickGlowPosY, TrickGlowPosZ;
        public ConfigEntry<bool> TrickGlowUseBoneRot;
        public ConfigEntry<float> TrickGlowLag;

        // 2. Manual Toggle
        public ConfigEntry<bool> ManualToggleEnabled;
        public ConfigEntry<string> ManualFolderName;
        public ConfigEntry<KeyCode> ManualToggleKey;
        public ConfigEntry<bool> ManualStayEnabled;
        public ConfigEntry<bool> ManualHoldEnabled; // Hold mode
        public ConfigEntry<float> ManualRotX, ManualRotY, ManualRotZ;
        public ConfigEntry<float> ManualPosX, ManualPosY, ManualPosZ;
        public ConfigEntry<bool> ManualUseBoneRot;
        public ConfigEntry<float> ManualLag;

        // 3. Boost Effects
        public ConfigEntry<bool> BoostSwapEnabled;
        public ConfigEntry<string> BoostFolderName;
        public ConfigEntry<float> BoostDelay;
        public ConfigEntry<float> BoostRotX, BoostRotY, BoostRotZ;
        public ConfigEntry<float> BoostPosX, BoostPosY, BoostPosZ;
        public ConfigEntry<bool> BoostUseBoneRot;
        public ConfigEntry<float> BoostLag;

        // 4. Boost Trick
        public ConfigEntry<bool> BoostTrickEnabled;
        public ConfigEntry<string> BoostTrickFolderName;
        public ConfigEntry<float> BoostTrickDelay;
        public ConfigEntry<bool> BoostTrickOverrideByBoosting;
        public ConfigEntry<float> BoostTrickRotX, BoostTrickRotY, BoostTrickRotZ;
        public ConfigEntry<float> BoostTrickPosX, BoostTrickPosY, BoostTrickPosZ;
        public ConfigEntry<bool> BoostTrickUseBoneRot;
        public ConfigEntry<float> BoostTrickLag;

        // 5. Static
        public ConfigEntry<bool> StaticEnabled;
        public ConfigEntry<string> StaticFolderName;
        public ConfigEntry<float> StaticRotX, StaticRotY, StaticRotZ;
        public ConfigEntry<float> StaticPosX, StaticPosY, StaticPosZ;
        public ConfigEntry<bool> StaticUseBoneRot;
        public ConfigEntry<float> StaticLag;

        // 6. Mini Boost Effects
        public ConfigEntry<bool> MiniBoostEnabled;
        public ConfigEntry<string> MiniBoostFolderName;
        public ConfigEntry<float> MiniBoostDelay; // Grace period after a mini boost
        public ConfigEntry<float> MiniBoostRotX, MiniBoostRotY, MiniBoostRotZ;
        public ConfigEntry<float> MiniBoostPosX, MiniBoostPosY, MiniBoostPosZ;
        public ConfigEntry<bool> MiniBoostUseBoneRot;
        public ConfigEntry<float> MiniBoostLag;

        // 7. Object Spawner (Static)
        public ConfigEntry<bool> ObjectSpawnerEnabled;
        public ConfigEntry<string> ObjectSpawnerFolderName;
        public ConfigEntry<float> ObjectSpawnerRotX, ObjectSpawnerRotY, ObjectSpawnerRotZ;
        public ConfigEntry<float> ObjectSpawnerPosX, ObjectSpawnerPosY, ObjectSpawnerPosZ;
        public ConfigEntry<bool> ObjectSpawnerUseBoneRot;

        // 8. Static 2
        public ConfigEntry<bool> Static2Enabled;
        public ConfigEntry<string> Static2FolderName;
        public ConfigEntry<float> Static2RotX, Static2RotY, Static2RotZ;
        public ConfigEntry<float> Static2PosX, Static2PosY, Static2PosZ;
        public ConfigEntry<bool> Static2UseBoneRot;
        public ConfigEntry<float> Static2Lag;

        // 9. Static 3
        public ConfigEntry<bool> Static3Enabled;
        public ConfigEntry<string> Static3FolderName;
        public ConfigEntry<float> Static3RotX, Static3RotY, Static3RotZ;
        public ConfigEntry<float> Static3PosX, Static3PosY, Static3PosZ;
        public ConfigEntry<bool> Static3UseBoneRot;
        public ConfigEntry<float> Static3Lag;

        // 10. MoveStyle Effects
        public ConfigEntry<bool> MoveStyleEnabled;
        public ConfigEntry<string> MoveStyleFolderName;
        public ConfigEntry<float> MoveStyleDelay;
        public ConfigEntry<float> MoveStyleRotX, MoveStyleRotY, MoveStyleRotZ;
        public ConfigEntry<float> MoveStylePosX, MoveStylePosY, MoveStylePosZ;
        public ConfigEntry<bool> MoveStyleUseBoneRot;
        public ConfigEntry<float> MoveStyleLag;
        public ConfigEntry<bool> MoveStyleFreezeParticleKill; // Freeze behavior for unequipped effects

        // 11. MoveStyle Boost Effects
        public ConfigEntry<bool> MoveStyleBoostEnabled;
        public ConfigEntry<string> MoveStyleBoostFolderName;
        public ConfigEntry<float> MoveStyleBoostDelay; // Grace period after a MoveStyle boost
        public ConfigEntry<float> MoveStyleBoostRotX, MoveStyleBoostRotY, MoveStyleBoostRotZ;
        public ConfigEntry<float> MoveStyleBoostPosX, MoveStyleBoostPosY, MoveStyleBoostPosZ;
        public ConfigEntry<bool> MoveStyleBoostUseBoneRot;
        public ConfigEntry<float> MoveStyleBoostLag;
        public ConfigEntry<bool> MoveStyleBoostFreezeParticleKill; // Freeze behavior for unequipped effects

        // 12. Manual Toggle 2
        public ConfigEntry<bool> Manual2ToggleEnabled;
        public ConfigEntry<string> Manual2FolderName;
        public ConfigEntry<KeyCode> Manual2ToggleKey;
        public ConfigEntry<bool> Manual2StayEnabled;
        public ConfigEntry<bool> Manual2HoldEnabled; // Hold mode
        public ConfigEntry<float> Manual2RotX, Manual2RotY, Manual2RotZ;
        public ConfigEntry<float> Manual2PosX, Manual2PosY, Manual2PosZ;
        public ConfigEntry<bool> Manual2UseBoneRot;
        public ConfigEntry<float> Manual2Lag;

        // 13. Phone
        public ConfigEntry<bool> PhoneEnabled;
        public ConfigEntry<string> PhoneFolderName;
        public ConfigEntry<float> PhoneRotX, PhoneRotY, PhoneRotZ;
        public ConfigEntry<float> PhonePosX, PhonePosY, PhonePosZ;
        public ConfigEntry<bool> PhoneUseBoneRot;
        public ConfigEntry<float> PhoneLag;

        // 14. HyperBoost
        public ConfigEntry<bool> HyperBoostEnabled;
        public ConfigEntry<string> HyperBoostFolderName;
        public ConfigEntry<float> HyperBoostDelay;
        public ConfigEntry<bool> HyperBoostBoostOverride; // Suppress regular boost effects while HyperBoost is active
        public ConfigEntry<bool> HyperBoostMoveStyleBoostOverride; // Suppress MoveStyle boost effects while HyperBoost is active
        public ConfigEntry<float> HyperBoostRotX, HyperBoostRotY, HyperBoostRotZ;
        public ConfigEntry<float> HyperBoostPosX, HyperBoostPosY, HyperBoostPosZ;
        public ConfigEntry<bool> HyperBoostUseBoneRot;
        public ConfigEntry<float> HyperBoostLag;

        // 15. HyperCharging
        public ConfigEntry<bool> HyperChargingEnabled;
        public ConfigEntry<string> HyperChargingFolderName;
        public ConfigEntry<float> HyperChargingDelay;
        public ConfigEntry<float> HyperChargingRotX, HyperChargingRotY, HyperChargingRotZ;
        public ConfigEntry<float> HyperChargingPosX, HyperChargingPosY, HyperChargingPosZ;
        public ConfigEntry<bool> HyperChargingUseBoneRot;
        public ConfigEntry<float> HyperChargingLag;

        // State tracking for refreshes
        // Folder path trackers
        private string _lastEffectsBundlesLocation = "";
        private string _lastLoadedTrickGlow = "";
        private string _lastLoadedBoost = "";
        private string _lastLoadedBoostTrick = "";
        private string _lastLoadedStatic = "";
        private string _lastLoadedMiniBoost = "";
        private string _lastLoadedManual = "";
        private string _lastLoadedObjectSpawner = "";
        private string _lastLoadedStatic2 = "";
        private string _lastLoadedStatic3 = "";
        private string _lastLoadedMoveStyle = "";
        private string _lastLoadedMoveStyleBoost = "";
        private string _lastLoadedManual2 = "";
        private string _lastLoadedPhone = "";
        private string _lastLoadedHyperBoost = "";
        private string _lastLoadedHyperCharging = "";

        // Config state trackers
        private bool _lastTrickGlowState = false;
        private bool _lastBoostSwapState = false;
        private bool _lastBoostTrickState = false;
        private bool _lastStaticState = false;
        private bool _lastMiniBoostState = false;
        private bool _lastManualConfigState = false;
        private bool _lastManualStayEnabledState = false;
        private bool _lastManualHoldState = false;
        private bool _lastObjectSpawnerState = false;
        private bool _lastStatic2State = false;
        private bool _lastStatic3State = false;
        private bool _lastMoveStyleState = false;
        private bool _lastMoveStyleBoostState = false;
        private bool _lastManual2ConfigState = false;
        private bool _lastManual2StayEnabledState = false;
        private bool _lastManual2HoldState = false;
        private bool _lastPhoneState = false;
        private bool _lastHyperBoostState = false;
        private bool _lastHyperBoostBoostOverrideState = false;
        private bool _lastHyperBoostMoveStyleBoostOverrideState = false;
        private bool _lastHyperChargingState = false;

        private bool _lastMoveStyleFreezeState = true; // Previous MoveStyle freeze setting
        private bool _lastMoveStyleBoostFreezeState = true; // Previous MoveStyle Boost freeze setting

        // Transform setting trackers
        private Vector3 _lastRotTrickGlow, _lastPosTrickGlow;
        private Vector3 _lastRotBoost, _lastPosBoost;
        private Vector3 _lastRotBoostTrick, _lastPosBoostTrick;
        private Vector3 _lastRotStatic, _lastPosStatic;
        private Vector3 _lastRotMiniBoost, _lastPosMiniBoost;
        private Vector3 _lastRotManual, _lastPosManual;
        private Vector3 _lastRotObjectSpawner, _lastPosObjectSpawner;
        private Vector3 _lastRotStatic2, _lastPosStatic2;
        private Vector3 _lastRotStatic3, _lastPosStatic3;
        private Vector3 _lastRotMoveStyle, _lastPosMoveStyle;
        private Vector3 _lastRotMoveStyleBoost, _lastPosMoveStyleBoost;
        private Vector3 _lastRotManual2, _lastPosManual2;
        private Vector3 _lastRotPhone, _lastPosPhone;
        private Vector3 _lastRotHyperBoost, _lastPosHyperBoost;
        private Vector3 _lastRotHyperCharging, _lastPosHyperCharging;

        private bool _lastUseBoneRotTrickGlow, _lastUseBoneRotBoost, _lastUseBoneRotBoostTrick, _lastUseBoneRotStatic, _lastUseBoneRotMiniBoost, _lastUseBoneRotManual, _lastUseBoneRotObjectSpawner, _lastUseBoneRotStatic2, _lastUseBoneRotStatic3, _lastUseBoneRotMoveStyle, _lastUseBoneRotMoveStyleBoost, _lastUseBoneRotManual2, _lastUseBoneRotPhone, _lastUseBoneRotHyperBoost, _lastUseBoneRotHyperCharging;
        private float _lastLagTrickGlow, _lastLagBoost, _lastLagBoostTrick, _lastLagStatic, _lastLagMiniBoost, _lastLagManual, _lastLagStatic2, _lastLagStatic3, _lastLagMoveStyle, _lastLagMoveStyleBoost, _lastLagManual2, _lastLagPhone, _lastLagHyperBoost, _lastLagHyperCharging;

        private bool _needsRefresh = false;

        // Ability timing trackers
        private float _lastTrickTime = -100f;
        private float _lastBoostTrickTime = -100f;
        private float _lastRegularBoostTime = -100f; // Prevents regular boosts from being detected as boost tricks
        private float _lastBoostTime = -100f;
        private float _lastMiniBoostTime = -100f;
        private float _lastMoveStyleTime = -100f;
        private float _lastMoveStyleBoostTime = -100f;

        // MoveStyle state used to preserve effect delays
        private bool _isMoveStyleActiveForFreeze = false;
        private bool _isMoveStyleBoostActiveForFreeze = false;

        // HyperBoost and HyperCharging timers
        private float _lastHyperBoostTime = -100f;
        private float _lastHyperChargingTime = -100f;

        private bool _manualToggleActive = false;
        private bool _forceManualRetrigger = false;
        private bool _manual2ToggleActive = false;
        private bool _forceManual2Retrigger = false;

        // Delayed character and bone loading state
        private Transform _lastVisualRoot = null;
        private float _characterLoadTimeout = 0f;
        private int _lastBoneCount = 0;

        private void Awake()
        {
            Instance = this;

            // 1. General
            IsEnabled = Config.Bind("1. General", "Enabled", true, "Master switch for the entire mod. After turn off, individual folders must be re-enabled.");
            EffectsBundlesLocation = Config.Bind("1. General", "Effects Bundles Path", Path.Combine(Paths.BepInExRootPath, "EffectsBundles"), "Directory where the effects bundles are located. Moved from plugins/SwagSpawn to avoid conflicts with ld_crewboom.");

            // 1.5 Trick Glow
            TrickGlowEnabled = Config.Bind("1.5 Trick Glow", "Enable Trick Glow", false, "Load effects that only appear when doing tricks. First jumping doesn't mean starting a combo; that is until you make a trick the combo effects will start.'");
            TrickGlowFolderName = Config.Bind("1.5 Trick Glow", "Trick Glow Folder", "TrickGlow", "Folder for Trick Glow effects.");
            TrickGlowDelay = Config.Bind("1.5 Trick Glow", "Trick Glow Delay", 1.5f, "Grace period: how long effects stay after a trick.");
            TrickGlowRotX = Config.Bind("1.5 Trick Glow", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            TrickGlowRotY = Config.Bind("1.5 Trick Glow", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            TrickGlowRotZ = Config.Bind("1.5 Trick Glow", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            TrickGlowPosX = Config.Bind("1.5 Trick Glow", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            TrickGlowPosY = Config.Bind("1.5 Trick Glow", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            TrickGlowPosZ = Config.Bind("1.5 Trick Glow", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            TrickGlowUseBoneRot = Config.Bind("1.5 Trick Glow", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            TrickGlowLag = Config.Bind("1.5 Trick Glow", "Position Lag", 0f, "0 = Instant. Higher = More Delay/Smoothing.");

            // 2. Manual Toggle
            ManualToggleEnabled = Config.Bind("2. Manual Toggle", "Enable Key Toggle", false, "Allow toggling effects with a key.");
            ManualFolderName = Config.Bind("2. Manual Toggle", "Manual Folder", "ManualEffects", "Folder for Manual Toggle effects.");
            ManualToggleKey = Config.Bind("2. Manual Toggle", "Toggle Key", KeyCode.Keypad5, "Key to toggle Manual effects.");
            ManualStayEnabled = Config.Bind("2. Manual Toggle", "Toggles Stay Enabled", false, "If true, hitting the key reloads/retriggers the effect instead of turning it off, maintaining it on.");
            ManualHoldEnabled = Config.Bind("2. Manual Toggle", "Hold Mode Enabled", false, "If true, the effect is only active while holding the key (overrides toggle).");
            ManualRotX = Config.Bind("2. Manual Toggle", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            ManualRotY = Config.Bind("2. Manual Toggle", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            ManualRotZ = Config.Bind("2. Manual Toggle", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            ManualPosX = Config.Bind("2. Manual Toggle", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            ManualPosY = Config.Bind("2. Manual Toggle", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            ManualPosZ = Config.Bind("2. Manual Toggle", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            ManualUseBoneRot = Config.Bind("2. Manual Toggle", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            ManualLag = Config.Bind("2. Manual Toggle", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 3. Boost Effects
            BoostSwapEnabled = Config.Bind("3. Boost Effects", "Enable BoostEffects", false, "Load effects from the Boost Folder when boosting.");
            BoostFolderName = Config.Bind("3. Boost Effects", "Boost Folder", "BoostEffects", "Boost Swap Folder name.");
            BoostDelay = Config.Bind("3. Boost Effects", "Boost Delay", 0f, "Grace period: how long effects stay after boosting.");
            BoostRotX = Config.Bind("3. Boost Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            BoostRotY = Config.Bind("3. Boost Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            BoostRotZ = Config.Bind("3. Boost Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            BoostPosX = Config.Bind("3. Boost Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            BoostPosY = Config.Bind("3. Boost Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            BoostPosZ = Config.Bind("3. Boost Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            BoostUseBoneRot = Config.Bind("3. Boost Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            BoostLag = Config.Bind("3. Boost Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 4. Boost Trick
            BoostTrickEnabled = Config.Bind("4. Boost Trick", "Enable BoostTrickGlow", false, "Load effects when doing a Boost Trick.");
            BoostTrickFolderName = Config.Bind("4. Boost Trick", "BoostTrick Folder", "BoostTrickGlow", "Boost Trick Folder name.");
            BoostTrickDelay = Config.Bind("4. Boost Trick", "BoostTrick Delay", 1.5f, "Grace period for Boost Tricks.");
            BoostTrickOverrideByBoosting = Config.Bind("4. Boost Trick", "Overriden by Boosting", false, "If true, Boost Trick effects disappear permanently if you boost.");
            BoostTrickRotX = Config.Bind("4. Boost Trick", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            BoostTrickRotY = Config.Bind("4. Boost Trick", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            BoostTrickRotZ = Config.Bind("4. Boost Trick", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            BoostTrickPosX = Config.Bind("4. Boost Trick", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            BoostTrickPosY = Config.Bind("4. Boost Trick", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            BoostTrickPosZ = Config.Bind("4. Boost Trick", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            BoostTrickUseBoneRot = Config.Bind("4. Boost Trick", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            BoostTrickLag = Config.Bind("4. Boost Trick", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 5. Static
            StaticEnabled = Config.Bind("5. Static", "Enable Static", false, "Load always-on effects.");
            StaticFolderName = Config.Bind("5. Static", "Static Folder", "StaticEffects", "Static Effect Folder name.");
            StaticRotX = Config.Bind("5. Static", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            StaticRotY = Config.Bind("5. Static", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            StaticRotZ = Config.Bind("5. Static", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            StaticPosX = Config.Bind("5. Static", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            StaticPosY = Config.Bind("5. Static", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            StaticPosZ = Config.Bind("5. Static", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            StaticUseBoneRot = Config.Bind("5. Static", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            StaticLag = Config.Bind("5. Static", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 6. Mini Boost Effects
            MiniBoostEnabled = Config.Bind("6. Mini Boost Effects", "Enable Mini Boost", false, "Load effects for when grinding, first jumping, super-sliding and slope-sliding.");
            MiniBoostFolderName = Config.Bind("6. Mini Boost Effects", "Mini Boost Folder", "MiniBoostEffects", "Mini Boost Swap Folder name.");
            MiniBoostDelay = Config.Bind("6. Mini Boost Effects", "Mini Boost Delay", 0f, "Grace period: how long effects stay after a mini boost.");
            MiniBoostRotX = Config.Bind("6. Mini Boost Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            MiniBoostRotY = Config.Bind("6. Mini Boost Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            MiniBoostRotZ = Config.Bind("6. Mini Boost Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            MiniBoostPosX = Config.Bind("6. Mini Boost Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            MiniBoostPosY = Config.Bind("6. Mini Boost Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            MiniBoostPosZ = Config.Bind("6. Mini Boost Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            MiniBoostUseBoneRot = Config.Bind("6. Mini Boost Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            MiniBoostLag = Config.Bind("6. Mini Boost Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 7. Object Spawner (Static)
            ObjectSpawnerEnabled = Config.Bind("7. Object Spawner", "Enable Object Spawner", false, "Load objects that spawn at the player position but remain independent (World Space).");
            ObjectSpawnerFolderName = Config.Bind("7. Object Spawner", "Object Spawner Folder", "ObjectSpawner", "Folder for Object Spawner assets.");
            ObjectSpawnerRotX = Config.Bind("7. Object Spawner", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            ObjectSpawnerRotY = Config.Bind("7. Object Spawner", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            ObjectSpawnerRotZ = Config.Bind("7. Object Spawner", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            ObjectSpawnerPosX = Config.Bind("7. Object Spawner", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            ObjectSpawnerPosY = Config.Bind("7. Object Spawner", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            ObjectSpawnerPosZ = Config.Bind("7. Object Spawner", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            ObjectSpawnerUseBoneRot = Config.Bind("7. Object Spawner", "Use Bone Rotation", true, "If True, object aligns with bone. If False, aligns with Player Root.");

            // 8. Static 2
            Static2Enabled = Config.Bind("8. Static 2", "Enable Static 2", false, "Load a second set of always-on effects.");
            Static2FolderName = Config.Bind("8. Static 2", "Static 2 Folder", "StaticEffects2", "Static Effect #2 Folder name.");
            Static2RotX = Config.Bind("8. Static 2", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            Static2RotY = Config.Bind("8. Static 2", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            Static2RotZ = Config.Bind("8. Static 2", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            Static2PosX = Config.Bind("8. Static 2", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            Static2PosY = Config.Bind("8. Static 2", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            Static2PosZ = Config.Bind("8. Static 2", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            Static2UseBoneRot = Config.Bind("8. Static 2", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            Static2Lag = Config.Bind("8. Static 2", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 9. Static 3
            Static3Enabled = Config.Bind("9. Static 3", "Enable Static 3", false, "Load a third set of always-on effects.");
            Static3FolderName = Config.Bind("9. Static 3", "Static 3 Folder", "StaticEffects3", "Static Effect #3 Folder name.");
            Static3RotX = Config.Bind("9. Static 3", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            Static3RotY = Config.Bind("9. Static 3", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            Static3RotZ = Config.Bind("9. Static 3", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            Static3PosX = Config.Bind("9. Static 3", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            Static3PosY = Config.Bind("9. Static 3", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            Static3PosZ = Config.Bind("9. Static 3", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            Static3UseBoneRot = Config.Bind("9. Static 3", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            Static3Lag = Config.Bind("9. Static 3", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 10. MoveStyle Effects
            MoveStyleEnabled = Config.Bind("10. MoveStyle Effects", "Enable MoveStyle", false, "Load effects when player is using a MoveStyle (not on foot).");
            MoveStyleFolderName = Config.Bind("10. MoveStyle Effects", "MoveStyle Folder", "MoveStyleEffects", "Folder for MoveStyle assets.");
            MoveStyleDelay = Config.Bind("10. MoveStyle Effects", "MoveStyle Delay", 0f, "Grace period: how long effects stay after using MoveStyle.");
            MoveStyleRotX = Config.Bind("10. MoveStyle Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            MoveStyleRotY = Config.Bind("10. MoveStyle Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            MoveStyleRotZ = Config.Bind("10. MoveStyle Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            MoveStylePosX = Config.Bind("10. MoveStyle Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            MoveStylePosY = Config.Bind("10. MoveStyle Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            MoveStylePosZ = Config.Bind("10. MoveStyle Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            MoveStyleUseBoneRot = Config.Bind("10. MoveStyle Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            MoveStyleLag = Config.Bind("10. MoveStyle Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay.");
            MoveStyleFreezeParticleKill = Config.Bind("10. MoveStyle Effects", "Freeze Particle Kill", true, "If true, freezes the effect in place when unequipped or during its delay release so particles don't snap to the player's root.");

            // 11. MoveStyle Boost Effects
            MoveStyleBoostEnabled = Config.Bind("11. MoveStyle Boost Effects", "Enable MoveStyleBoost", false, "Load effects when player is boosting while using a MoveStyle.");
            MoveStyleBoostFolderName = Config.Bind("11. MoveStyle Boost Effects", "MoveStyleBoost Folder", "MoveStyleBoostEffects", "Folder for MoveStyleBoost assets.");
            MoveStyleBoostDelay = Config.Bind("11. MoveStyle Boost Effects", "MoveStyle Boost Delay", 0f, "Grace period: how long effects stay after a MoveStyle boost.");
            MoveStyleBoostRotX = Config.Bind("11. MoveStyle Boost Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            MoveStyleBoostRotY = Config.Bind("11. MoveStyle Boost Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            MoveStyleBoostRotZ = Config.Bind("11. MoveStyle Boost Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            MoveStyleBoostPosX = Config.Bind("11. MoveStyle Boost Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            MoveStyleBoostPosY = Config.Bind("11. MoveStyle Boost Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            MoveStyleBoostPosZ = Config.Bind("11. MoveStyle Boost Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            MoveStyleBoostUseBoneRot = Config.Bind("11. MoveStyle Boost Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            MoveStyleBoostLag = Config.Bind("11. MoveStyle Boost Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay.");
            MoveStyleBoostFreezeParticleKill = Config.Bind("11. MoveStyle Boost Effects", "Freeze Particle Kill", true, "If true, freezes the effect in place when unequipped or during its delay release so particles don't snap to the player's root.");

            // 12. Manual Toggle 2
            Manual2ToggleEnabled = Config.Bind("12. Manual Toggle 2", "Enable Key Toggle 2", false, "Allow toggling effects with a second key.");
            Manual2FolderName = Config.Bind("12. Manual Toggle 2", "Manual 2 Folder", "ManualEffects2", "Folder for the second Manual Toggle effects.");
            Manual2ToggleKey = Config.Bind("12. Manual Toggle 2", "Toggle Key 2", KeyCode.Keypad6, "Key to toggle the second Manual effects.");
            Manual2StayEnabled = Config.Bind("12. Manual Toggle 2", "Toggles Stay Enabled", false, "If true, hitting the key reloads/retriggers the effect instead of turning it off, maintaining it on.");
            Manual2HoldEnabled = Config.Bind("12. Manual Toggle 2", "Hold Mode Enabled", false, "If true, the effect is only active while holding the key (overrides toggle).");
            Manual2RotX = Config.Bind("12. Manual Toggle 2", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            Manual2RotY = Config.Bind("12. Manual Toggle 2", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            Manual2RotZ = Config.Bind("12. Manual Toggle 2", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            Manual2PosX = Config.Bind("12. Manual Toggle 2", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            Manual2PosY = Config.Bind("12. Manual Toggle 2", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            Manual2PosZ = Config.Bind("12. Manual Toggle 2", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            Manual2UseBoneRot = Config.Bind("12. Manual Toggle 2", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            Manual2Lag = Config.Bind("12. Manual Toggle 2", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay. Recommended value for bouncy smooth floating object = 6-12.");

            // 13. Phone
            PhoneEnabled = Config.Bind("13. Phone", "Enable Phone", false, "Load effects when the player has the phone out.");
            PhoneFolderName = Config.Bind("13. Phone", "Phone Folder", "PhoneEffects", "Folder for Phone assets.");
            PhoneRotX = Config.Bind("13. Phone", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            PhoneRotY = Config.Bind("13. Phone", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            PhoneRotZ = Config.Bind("13. Phone", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            PhonePosX = Config.Bind("13. Phone", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            PhonePosY = Config.Bind("13. Phone", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            PhonePosZ = Config.Bind("13. Phone", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            PhoneUseBoneRot = Config.Bind("13. Phone", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            PhoneLag = Config.Bind("13. Phone", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay.");

            // 14. HyperBoost
            HyperBoostEnabled = Config.Bind("14. HyperBoost Effects", "Enable HyperBoost", false, "Load effects when the player is using the HyperBoost ability.");
            HyperBoostFolderName = Config.Bind("14. HyperBoost Effects", "HyperBoost Folder", "HyperBoostEffects", "Folder for HyperBoost assets.");
            HyperBoostDelay = Config.Bind("14. HyperBoost Effects", "HyperBoost Delay", 0f, "Grace period: how long effects stay after a hyperboost.");
            HyperBoostBoostOverride = Config.Bind("14. HyperBoost Effects", "Boost Override", false, "If true, suppresses regular Boost effects while HyperBoost is active (including its set delay time).");
            HyperBoostMoveStyleBoostOverride = Config.Bind("14. HyperBoost Effects", "MoveStyle Boost Override", false, "If true, suppresses MoveStyle Boost effects while HyperBoost is active (including its set delay time).");
            HyperBoostRotX = Config.Bind("14. HyperBoost Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            HyperBoostRotY = Config.Bind("14. HyperBoost Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            HyperBoostRotZ = Config.Bind("14. HyperBoost Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            HyperBoostPosX = Config.Bind("14. HyperBoost Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            HyperBoostPosY = Config.Bind("14. HyperBoost Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            HyperBoostPosZ = Config.Bind("14. HyperBoost Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            HyperBoostUseBoneRot = Config.Bind("14. HyperBoost Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            HyperBoostLag = Config.Bind("14. HyperBoost Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay.");

            // 15. HyperCharging
            HyperChargingEnabled = Config.Bind("15. HyperCharging Effects", "Enable HyperCharging", false, "Load effects when the player is charging a HyperBoost (QTE / Pre-delay).");
            HyperChargingFolderName = Config.Bind("15. HyperCharging Effects", "HyperCharging Folder", "HyperChargingEffects", "Folder for HyperCharging assets.");
            HyperChargingDelay = Config.Bind("15. HyperCharging Effects", "HyperCharging Delay", 0f, "Grace period: how long effects stay after charging a hyperboost.");
            HyperChargingRotX = Config.Bind("15. HyperCharging Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            HyperChargingRotY = Config.Bind("15. HyperCharging Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            HyperChargingRotZ = Config.Bind("15. HyperCharging Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            HyperChargingPosX = Config.Bind("15. HyperCharging Effects", "Position X", 0f, "X axis Offset (commonly Left/Right)");
            HyperChargingPosY = Config.Bind("15. HyperCharging Effects", "Position Y", 0f, "Y axis Offset (commonly Up/Down)");
            HyperChargingPosZ = Config.Bind("15. HyperCharging Effects", "Position Z", 0f, "Z axis Offset (commonly Forward/Back)");
            HyperChargingUseBoneRot = Config.Bind("15. HyperCharging Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            HyperChargingLag = Config.Bind("15. HyperCharging Effects", "Position Lag", 0f, "Dragging smooth delayed position effect for floating assets. 0 = Instant. Higher = More Delay.");

            IsEnabled.SettingChanged += (sender, args) => { if (IsEnabled.Value) _needsRefresh = true; };

            // Create effect folders
            string newRootPath = Path.Combine(Paths.BepInExRootPath, "EffectsBundles");
            if (!Directory.Exists(newRootPath)) Directory.CreateDirectory(newRootPath);

            string[] subfolders = {
                TrickGlowFolderName.Value, BoostFolderName.Value, BoostTrickFolderName.Value,
                StaticFolderName.Value, MiniBoostFolderName.Value, ManualFolderName.Value,
                ObjectSpawnerFolderName.Value, Static2FolderName.Value, Static3FolderName.Value,
                MoveStyleFolderName.Value, MoveStyleBoostFolderName.Value, Manual2FolderName.Value,
                PhoneFolderName.Value, HyperBoostFolderName.Value, HyperChargingFolderName.Value
            };

            foreach (string folder in subfolders)
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    string newSub = Path.Combine(newRootPath, folder.Trim());
                    if (!Directory.Exists(newSub)) Directory.CreateDirectory(newSub);
                }
            }

            // Use the configured bundle path
            _baseBundlesFolder = EffectsBundlesLocation.Value;
            if (!Directory.Exists(_baseBundlesFolder)) Directory.CreateDirectory(_baseBundlesFolder);

            // Reflection lookups
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _characterVisualField = typeof(Player).GetField("characterVisual", flags);
            _abilityField = typeof(Player).GetField("ability", flags);
            _boostingField = typeof(Player).GetField("boosting", flags);
            _checkBoostTrickMethod = typeof(Player).GetMethod("CheckBoostTrick", flags);
            _moveStyleField = typeof(Player).GetField("moveStyle", flags);

            // Mini Boost reflection fields
            _isJumpingField = typeof(Player).GetField("isJumping", flags);
            _timeSinceLastJumpField = typeof(Player).GetField("timeSinceLastJump", flags);
            _grindTrickTimerField = typeof(GrindAbility).GetField("trickTimer", flags);
            _slideStateField = typeof(SlideAbility).GetField("slideState", flags);

            // Boost-trick reflection field
            _curTrickBoostField = typeof(GrindAbility).GetField("curTrickBoost", flags);

            // Phone-state reflection field
            _phoneActiveField = typeof(CharacterVisual).GetField("phoneActive", flags);

            _harmony = new Harmony("com.desdezerotapes.swagspawn");
            _harmony.PatchAll();

            RefreshEffects();
            _needsRefresh = true;
            Logger.LogInfo("Swag Spawn v7.0.0 (HyperBoost & HyperCharging API Integration) Initialized. Active Path: " + _baseBundlesFolder);
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void Update()
        {
            if (ManualToggleEnabled.Value)
            {
                if (ManualHoldEnabled.Value)
                {
                    _manualToggleActive = Input.GetKey(ManualToggleKey.Value);
                }
                else if (Input.GetKeyDown(ManualToggleKey.Value))
                {
                    if (ManualStayEnabled.Value)
                    {
                        _manualToggleActive = true;
                        _forceManualRetrigger = true;
                        Logger.LogInfo($"[PA] Manual Effects Retriggered.");
                    }
                    else
                    {
                        _manualToggleActive = !_manualToggleActive;
                        Logger.LogInfo($"[PA] Manual Effects Toggled: {_manualToggleActive}");
                    }
                }
            }

            if (Manual2ToggleEnabled.Value)
            {
                if (Manual2HoldEnabled.Value)
                {
                    _manual2ToggleActive = Input.GetKey(Manual2ToggleKey.Value);
                }
                else if (Input.GetKeyDown(Manual2ToggleKey.Value))
                {
                    if (Manual2StayEnabled.Value)
                    {
                        _manual2ToggleActive = true;
                        _forceManual2Retrigger = true;
                        Logger.LogInfo($"[PA] Manual 2 Effects Retriggered.");
                    }
                    else
                    {
                        _manual2ToggleActive = !_manual2ToggleActive;
                        Logger.LogInfo($"[PA] Manual 2 Effects Toggled: {_manual2ToggleActive}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), "SetCharacter")]
        public static class PlayerSetCharacterPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                if (Traverse.Create(__instance).Field("isAI").GetValue<bool>()) return;
                Instance._needsRefresh = true;
                Instance._characterLoadTimeout = 3.5f; // Start monitoring for delayed bone loading after a character change
                Instance.Logger.LogInfo("[PA] Character Changed. Refreshing and monitoring for delayed bone loads (LD Crewboom)...");
            }
        }

        private void RefreshEffects()
        {
            _baseBundlesFolder = EffectsBundlesLocation.Value;
            _prefabCategoryMap.Clear();

            if (TrickGlowEnabled.Value) LoadBundlesIntoCategory(TrickGlowFolderName.Value, EffectCategory.TrickGlow);
            if (BoostSwapEnabled.Value) LoadBundlesIntoCategory(BoostFolderName.Value, EffectCategory.Boost);
            if (BoostTrickEnabled.Value) LoadBundlesIntoCategory(BoostTrickFolderName.Value, EffectCategory.BoostTrick);
            if (StaticEnabled.Value) LoadBundlesIntoCategory(StaticFolderName.Value, EffectCategory.Static);
            if (MiniBoostEnabled.Value) LoadBundlesIntoCategory(MiniBoostFolderName.Value, EffectCategory.MiniBoost);
            if (ManualToggleEnabled.Value) LoadBundlesIntoCategory(ManualFolderName.Value, EffectCategory.Manual);
            if (ObjectSpawnerEnabled.Value) LoadBundlesIntoCategory(ObjectSpawnerFolderName.Value, EffectCategory.ObjectSpawner);
            if (Static2Enabled.Value) LoadBundlesIntoCategory(Static2FolderName.Value, EffectCategory.Static2);
            if (Static3Enabled.Value) LoadBundlesIntoCategory(Static3FolderName.Value, EffectCategory.Static3);
            if (MoveStyleEnabled.Value) LoadBundlesIntoCategory(MoveStyleFolderName.Value, EffectCategory.MoveStyle);
            if (MoveStyleBoostEnabled.Value) LoadBundlesIntoCategory(MoveStyleBoostFolderName.Value, EffectCategory.MoveStyleBoost);
            if (Manual2ToggleEnabled.Value) LoadBundlesIntoCategory(Manual2FolderName.Value, EffectCategory.Manual2);
            if (PhoneEnabled.Value) LoadBundlesIntoCategory(PhoneFolderName.Value, EffectCategory.Phone);
            if (HyperBoostEnabled.Value) LoadBundlesIntoCategory(HyperBoostFolderName.Value, EffectCategory.HyperBoost);
            if (HyperChargingEnabled.Value) LoadBundlesIntoCategory(HyperChargingFolderName.Value, EffectCategory.HyperCharging);

            // Update folder path trackers
            _lastEffectsBundlesLocation = EffectsBundlesLocation.Value;
            _lastLoadedTrickGlow = TrickGlowFolderName.Value;
            _lastLoadedBoost = BoostFolderName.Value;
            _lastLoadedBoostTrick = BoostTrickFolderName.Value;
            _lastLoadedStatic = StaticFolderName.Value;
            _lastLoadedMiniBoost = MiniBoostFolderName.Value;
            _lastLoadedManual = ManualFolderName.Value;
            _lastLoadedObjectSpawner = ObjectSpawnerFolderName.Value;
            _lastLoadedStatic2 = Static2FolderName.Value;
            _lastLoadedStatic3 = Static3FolderName.Value;
            _lastLoadedMoveStyle = MoveStyleFolderName.Value;
            _lastLoadedMoveStyleBoost = MoveStyleBoostFolderName.Value;
            _lastLoadedManual2 = Manual2FolderName.Value;
            _lastLoadedPhone = PhoneFolderName.Value;
            _lastLoadedHyperBoost = HyperBoostFolderName.Value;
            _lastLoadedHyperCharging = HyperChargingFolderName.Value;

            // Update config state trackers
            _lastTrickGlowState = TrickGlowEnabled.Value;
            _lastBoostSwapState = BoostSwapEnabled.Value;
            _lastBoostTrickState = BoostTrickEnabled.Value;
            _lastStaticState = StaticEnabled.Value;
            _lastMiniBoostState = MiniBoostEnabled.Value;
            _lastManualConfigState = ManualToggleEnabled.Value;
            _lastManualStayEnabledState = ManualStayEnabled.Value;
            _lastManualHoldState = ManualHoldEnabled.Value;
            _lastObjectSpawnerState = ObjectSpawnerEnabled.Value;
            _lastStatic2State = Static2Enabled.Value;
            _lastStatic3State = Static3Enabled.Value;
            _lastMoveStyleState = MoveStyleEnabled.Value;
            _lastMoveStyleBoostState = MoveStyleBoostEnabled.Value;
            _lastManual2ConfigState = Manual2ToggleEnabled.Value;
            _lastManual2StayEnabledState = Manual2StayEnabled.Value;
            _lastManual2HoldState = Manual2HoldEnabled.Value;
            _lastPhoneState = PhoneEnabled.Value;
            _lastHyperBoostState = HyperBoostEnabled.Value;
            _lastHyperBoostBoostOverrideState = HyperBoostBoostOverride.Value;
            _lastHyperBoostMoveStyleBoostOverrideState = HyperBoostMoveStyleBoostOverride.Value;
            _lastHyperChargingState = HyperChargingEnabled.Value;

            _lastMoveStyleFreezeState = MoveStyleFreezeParticleKill.Value;
            _lastMoveStyleBoostFreezeState = MoveStyleBoostFreezeParticleKill.Value;

            // Update transform setting trackers
            _lastRotTrickGlow = new Vector3(TrickGlowRotX.Value, TrickGlowRotY.Value, TrickGlowRotZ.Value);
            _lastPosTrickGlow = new Vector3(TrickGlowPosX.Value, TrickGlowPosY.Value, TrickGlowPosZ.Value);
            _lastUseBoneRotTrickGlow = TrickGlowUseBoneRot.Value;
            _lastLagTrickGlow = TrickGlowLag.Value;

            _lastRotBoost = new Vector3(BoostRotX.Value, BoostRotY.Value, BoostRotZ.Value);
            _lastPosBoost = new Vector3(BoostPosX.Value, BoostPosY.Value, BoostPosZ.Value);
            _lastUseBoneRotBoost = BoostUseBoneRot.Value;
            _lastLagBoost = BoostLag.Value;

            _lastRotBoostTrick = new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
            _lastPosBoostTrick = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
            _lastUseBoneRotBoostTrick = BoostTrickUseBoneRot.Value;
            _lastLagBoostTrick = BoostTrickLag.Value;

            _lastRotStatic = new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
            _lastPosStatic = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
            _lastUseBoneRotStatic = StaticUseBoneRot.Value;
            _lastLagStatic = StaticLag.Value;

            _lastRotMiniBoost = new Vector3(MiniBoostRotX.Value, MiniBoostRotY.Value, MiniBoostRotZ.Value);
            _lastPosMiniBoost = new Vector3(MiniBoostPosX.Value, MiniBoostPosY.Value, MiniBoostPosZ.Value);
            _lastUseBoneRotMiniBoost = MiniBoostUseBoneRot.Value;
            _lastLagMiniBoost = MiniBoostLag.Value;

            _lastRotManual = new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
            _lastPosManual = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
            _lastUseBoneRotManual = ManualUseBoneRot.Value;
            _lastLagManual = ManualLag.Value;

            _lastRotObjectSpawner = new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
            _lastPosObjectSpawner = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
            _lastUseBoneRotObjectSpawner = ObjectSpawnerUseBoneRot.Value;

            _lastRotStatic2 = new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
            _lastPosStatic2 = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
            _lastUseBoneRotStatic2 = Static2UseBoneRot.Value;
            _lastLagStatic2 = Static2Lag.Value;

            _lastRotStatic3 = new Vector3(Static3RotX.Value, Static3RotY.Value, Static3RotZ.Value);
            _lastPosStatic3 = new Vector3(Static3PosX.Value, Static3PosY.Value, Static3PosZ.Value);
            _lastUseBoneRotStatic3 = Static3UseBoneRot.Value;
            _lastLagStatic3 = Static3Lag.Value;

            _lastRotMoveStyle = new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
            _lastPosMoveStyle = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
            _lastUseBoneRotMoveStyle = MoveStyleUseBoneRot.Value;
            _lastLagMoveStyle = MoveStyleLag.Value;

            _lastRotMoveStyleBoost = new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
            _lastPosMoveStyleBoost = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
            _lastUseBoneRotMoveStyleBoost = MoveStyleBoostUseBoneRot.Value;
            _lastLagMoveStyleBoost = MoveStyleBoostLag.Value;

            _lastRotManual2 = new Vector3(Manual2RotX.Value, Manual2RotY.Value, Manual2RotZ.Value);
            _lastPosManual2 = new Vector3(Manual2PosX.Value, Manual2PosY.Value, Manual2PosZ.Value);
            _lastUseBoneRotManual2 = Manual2UseBoneRot.Value;
            _lastLagManual2 = Manual2Lag.Value;

            _lastRotPhone = new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
            _lastPosPhone = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
            _lastUseBoneRotPhone = PhoneUseBoneRot.Value;
            _lastLagPhone = PhoneLag.Value;

            _lastRotHyperBoost = new Vector3(HyperBoostRotX.Value, HyperBoostRotY.Value, HyperBoostRotZ.Value);
            _lastPosHyperBoost = new Vector3(HyperBoostPosX.Value, HyperBoostPosY.Value, HyperBoostPosZ.Value);
            _lastUseBoneRotHyperBoost = HyperBoostUseBoneRot.Value;
            _lastLagHyperBoost = HyperBoostLag.Value;

            _lastRotHyperCharging = new Vector3(HyperChargingRotX.Value, HyperChargingRotY.Value, HyperChargingRotZ.Value);
            _lastPosHyperCharging = new Vector3(HyperChargingPosX.Value, HyperChargingPosY.Value, HyperChargingPosZ.Value);
            _lastUseBoneRotHyperCharging = HyperChargingUseBoneRot.Value;
            _lastLagHyperCharging = HyperChargingLag.Value;
        }

        private void LoadBundlesIntoCategory(string subFolder, EffectCategory category)
        {
            string targetPath = _baseBundlesFolder;
            if (!string.IsNullOrEmpty(subFolder))
            {
                string combined = Path.Combine(_baseBundlesFolder, subFolder.Trim());
                if (Directory.Exists(combined)) targetPath = combined;
                else return;
            }

            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            if (Directory.Exists(targetPath))
            {
                foreach (var file in Directory.GetFiles(targetPath, "*.cbb", SearchOption.TopDirectoryOnly))
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(file);
                    if (bundle == null) continue;

                    var assets = bundle.LoadAllAssets<GameObject>();
                    foreach (var go in assets)
                    {
                        if (!_prefabCategoryMap.ContainsKey(go))
                        {
                            _prefabCategoryMap.Add(go, category);
                        }
                    }
                    bundle.Unload(false);
                }
            }
        }

        private void LateUpdate()
        {
            var player = WorldHandler.instance?.GetCurrentPlayer();
            if (player == null) return;

            object visualObj = _characterVisualField?.GetValue(player);
            if (visualObj == null) return;
            Transform visualRoot = ExtractTransform(visualObj);
            if (visualRoot == null) return;

            // Monitor character visuals and delayed bone loading
            if (visualRoot != _lastVisualRoot)
            {
                _lastVisualRoot = visualRoot;
                _needsRefresh = true;
                _characterLoadTimeout = 3.5f; // Monitor for 3.5 seconds to detect delayed bone loading
                Logger.LogInfo("[PA] Visual root initialized/changed. Starting delayed bone monitoring...");
            }

            if (_characterLoadTimeout > 0f)
            {
                _characterLoadTimeout -= Time.deltaTime;
                int currentBoneCount = visualRoot.GetComponentsInChildren<Transform>(true).Length;

                // Detect bones added by delayed model or plugin loading.
                if (currentBoneCount != _lastBoneCount && currentBoneCount > 0)
                {
                    // Store the new count immediately to avoid processing the same change repeatedly in this frame.
                    _lastBoneCount = currentBoneCount;
                    _needsRefresh = true;
                    Logger.LogInfo($"[PA] Skeleton structure updated (Bone count: {currentBoneCount}). Re-mapping attachments for LD Crewboom streaming...");
                }
            }

            // Detect folder path changes
            bool folderChanged =
                EffectsBundlesLocation.Value != _lastEffectsBundlesLocation ||
                TrickGlowFolderName.Value != _lastLoadedTrickGlow ||
                BoostFolderName.Value != _lastLoadedBoost ||
                BoostTrickFolderName.Value != _lastLoadedBoostTrick ||
                StaticFolderName.Value != _lastLoadedStatic ||
                MiniBoostFolderName.Value != _lastLoadedMiniBoost ||
                ManualFolderName.Value != _lastLoadedManual ||
                ObjectSpawnerFolderName.Value != _lastLoadedObjectSpawner ||
                Static2FolderName.Value != _lastLoadedStatic2 ||
                Static3FolderName.Value != _lastLoadedStatic3 ||
                MoveStyleFolderName.Value != _lastLoadedMoveStyle ||
                MoveStyleBoostFolderName.Value != _lastLoadedMoveStyleBoost ||
                Manual2FolderName.Value != _lastLoadedManual2 ||
                PhoneFolderName.Value != _lastLoadedPhone ||
                HyperBoostFolderName.Value != _lastLoadedHyperBoost ||
                HyperChargingFolderName.Value != _lastLoadedHyperCharging;

            // Detect enabled-state changes
            bool stateChanged =
                TrickGlowEnabled.Value != _lastTrickGlowState ||
                BoostSwapEnabled.Value != _lastBoostSwapState ||
                BoostTrickEnabled.Value != _lastBoostTrickState ||
                StaticEnabled.Value != _lastStaticState ||
                MiniBoostEnabled.Value != _lastMiniBoostState ||
                ManualToggleEnabled.Value != _lastManualConfigState ||
                ManualStayEnabled.Value != _lastManualStayEnabledState ||
                ManualHoldEnabled.Value != _lastManualHoldState ||
                ObjectSpawnerEnabled.Value != _lastObjectSpawnerState ||
                Static2Enabled.Value != _lastStatic2State ||
                Static3Enabled.Value != _lastStatic3State ||
                MoveStyleEnabled.Value != _lastMoveStyleState ||
                MoveStyleBoostEnabled.Value != _lastMoveStyleBoostState ||
                Manual2ToggleEnabled.Value != _lastManual2ConfigState ||
                Manual2StayEnabled.Value != _lastManual2StayEnabledState ||
                Manual2HoldEnabled.Value != _lastManual2HoldState ||
                PhoneEnabled.Value != _lastPhoneState ||
                HyperBoostEnabled.Value != _lastHyperBoostState ||
                HyperBoostBoostOverride.Value != _lastHyperBoostBoostOverrideState ||
                HyperBoostMoveStyleBoostOverride.Value != _lastHyperBoostMoveStyleBoostOverrideState ||
                HyperChargingEnabled.Value != _lastHyperChargingState ||
                MoveStyleFreezeParticleKill.Value != _lastMoveStyleFreezeState ||
                MoveStyleBoostFreezeParticleKill.Value != _lastMoveStyleBoostFreezeState;

            bool trickGlowChanged =
                _lastRotTrickGlow != new Vector3(TrickGlowRotX.Value, TrickGlowRotY.Value, TrickGlowRotZ.Value) ||
                _lastPosTrickGlow != new Vector3(TrickGlowPosX.Value, TrickGlowPosY.Value, TrickGlowPosZ.Value) ||
                _lastUseBoneRotTrickGlow != TrickGlowUseBoneRot.Value ||
                _lastLagTrickGlow != TrickGlowLag.Value;

            bool boostChanged =
                _lastRotBoost != new Vector3(BoostRotX.Value, BoostRotY.Value, BoostRotZ.Value) ||
                _lastPosBoost != new Vector3(BoostPosX.Value, BoostPosY.Value, BoostPosZ.Value) ||
                _lastUseBoneRotBoost != BoostUseBoneRot.Value ||
                _lastLagBoost != BoostLag.Value;

            bool boostTrickChanged =
                _lastRotBoostTrick != new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value) ||
                _lastPosBoostTrick != new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value) ||
                _lastUseBoneRotBoostTrick != BoostTrickUseBoneRot.Value ||
                _lastLagBoostTrick != BoostTrickLag.Value;

            bool staticChanged =
                _lastRotStatic != new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value) ||
                _lastPosStatic != new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value) ||
                _lastUseBoneRotStatic != StaticUseBoneRot.Value ||
                _lastLagStatic != StaticLag.Value;

            bool miniBoostChanged =
                _lastRotMiniBoost != new Vector3(MiniBoostRotX.Value, MiniBoostRotY.Value, MiniBoostRotZ.Value) ||
                _lastPosMiniBoost != new Vector3(MiniBoostPosX.Value, MiniBoostPosY.Value, MiniBoostPosZ.Value) ||
                _lastUseBoneRotMiniBoost != MiniBoostUseBoneRot.Value ||
                _lastLagMiniBoost != MiniBoostLag.Value;

            bool manualChanged =
                _lastRotManual != new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value) ||
                _lastPosManual != new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value) ||
                _lastUseBoneRotManual != ManualUseBoneRot.Value ||
                _lastLagManual != ManualLag.Value;

            bool objectSpawnerChanged =
                _lastRotObjectSpawner != new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value) ||
                _lastPosObjectSpawner != new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value) ||
                _lastUseBoneRotObjectSpawner != ObjectSpawnerUseBoneRot.Value;

            bool static2Changed =
                _lastRotStatic2 != new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value) ||
                _lastPosStatic2 != new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value) ||
                _lastUseBoneRotStatic2 != Static2UseBoneRot.Value ||
                _lastLagStatic2 != Static2Lag.Value;

            bool static3Changed =
                _lastRotStatic3 != new Vector3(Static3RotX.Value, Static3RotY.Value, Static3RotZ.Value) ||
                _lastPosStatic3 != new Vector3(Static3PosX.Value, Static3PosY.Value, Static3PosZ.Value) ||
                _lastUseBoneRotStatic3 != Static3UseBoneRot.Value ||
                _lastLagStatic3 != Static3Lag.Value;

            bool moveStyleChanged =
                _lastRotMoveStyle != new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value) ||
                _lastPosMoveStyle != new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value) ||
                _lastUseBoneRotMoveStyle != MoveStyleUseBoneRot.Value ||
                _lastLagMoveStyle != MoveStyleLag.Value;

            bool moveStyleBoostChanged =
                _lastRotMoveStyleBoost != new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value) ||
                _lastPosMoveStyleBoost != new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value) ||
                _lastUseBoneRotMoveStyleBoost != MoveStyleBoostUseBoneRot.Value ||
                _lastLagMoveStyleBoost != MoveStyleBoostLag.Value;

            bool manual2Changed =
                _lastRotManual2 != new Vector3(Manual2RotX.Value, Manual2RotY.Value, Manual2RotZ.Value) ||
                _lastPosManual2 != new Vector3(Manual2PosX.Value, Manual2PosY.Value, Manual2PosZ.Value) ||
                _lastUseBoneRotManual2 != Manual2UseBoneRot.Value ||
                _lastLagManual2 != Manual2Lag.Value;

            bool phoneChanged =
                _lastRotPhone != new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value) ||
                _lastPosPhone != new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value) ||
                _lastUseBoneRotPhone != PhoneUseBoneRot.Value ||
                _lastLagPhone != PhoneLag.Value;

            bool hyperBoostChanged =
                _lastRotHyperBoost != new Vector3(HyperBoostRotX.Value, HyperBoostRotY.Value, HyperBoostRotZ.Value) ||
                _lastPosHyperBoost != new Vector3(HyperBoostPosX.Value, HyperBoostPosY.Value, HyperBoostPosZ.Value) ||
                _lastUseBoneRotHyperBoost != HyperBoostUseBoneRot.Value ||
                _lastLagHyperBoost != HyperBoostLag.Value;

            bool hyperChargingChanged =
                _lastRotHyperCharging != new Vector3(HyperChargingRotX.Value, HyperChargingRotY.Value, HyperChargingRotZ.Value) ||
                _lastPosHyperCharging != new Vector3(HyperChargingPosX.Value, HyperChargingPosY.Value, HyperChargingPosZ.Value) ||
                _lastUseBoneRotHyperCharging != HyperChargingUseBoneRot.Value ||
                _lastLagHyperCharging != HyperChargingLag.Value;

            bool transformChanged = trickGlowChanged ||
                boostChanged || boostTrickChanged || staticChanged || miniBoostChanged || manualChanged || objectSpawnerChanged || static2Changed || static3Changed || moveStyleChanged || moveStyleBoostChanged ||
                manual2Changed || phoneChanged || hyperBoostChanged || hyperChargingChanged;

            if (IsEnabled.Value && (folderChanged || stateChanged || transformChanged || _needsRefresh))
            {
                bool wasRefreshRequested = _needsRefresh;
                if (!transformChanged) Logger.LogInfo("[PA] Configuration changed detected. Refreshing puppets...");
                _needsRefresh = false;

                if (folderChanged || stateChanged || wasRefreshRequested)
                {
                    DestroyPuppets();
                    RefreshEffects();
                    SpawnPuppets(visualRoot);
                }
                else if (transformChanged)
                {
                    // Save updated transform settings
                    _lastRotTrickGlow = new Vector3(TrickGlowRotX.Value, TrickGlowRotY.Value, TrickGlowRotZ.Value);
                    _lastPosTrickGlow = new Vector3(TrickGlowPosX.Value, TrickGlowPosY.Value, TrickGlowPosZ.Value);
                    _lastUseBoneRotTrickGlow = TrickGlowUseBoneRot.Value;
                    _lastLagTrickGlow = TrickGlowLag.Value;

                    _lastRotBoost = new Vector3(BoostRotX.Value, BoostRotY.Value, BoostRotZ.Value);
                    _lastPosBoost = new Vector3(BoostPosX.Value, BoostPosY.Value, BoostPosZ.Value);
                    _lastUseBoneRotBoost = BoostUseBoneRot.Value;
                    _lastLagBoost = BoostLag.Value;

                    _lastRotBoostTrick = new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
                    _lastPosBoostTrick = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
                    _lastUseBoneRotBoostTrick = BoostTrickUseBoneRot.Value;
                    _lastLagBoostTrick = BoostTrickLag.Value;

                    _lastRotStatic = new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
                    _lastPosStatic = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
                    _lastUseBoneRotStatic = StaticUseBoneRot.Value;
                    _lastLagStatic = StaticLag.Value;

                    _lastRotMiniBoost = new Vector3(MiniBoostRotX.Value, MiniBoostRotY.Value, MiniBoostRotZ.Value);
                    _lastPosMiniBoost = new Vector3(MiniBoostPosX.Value, MiniBoostPosY.Value, MiniBoostPosZ.Value);
                    _lastUseBoneRotMiniBoost = MiniBoostUseBoneRot.Value;
                    _lastLagMiniBoost = MiniBoostLag.Value;

                    _lastRotManual = new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
                    _lastPosManual = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
                    _lastUseBoneRotManual = ManualUseBoneRot.Value;
                    _lastLagManual = ManualLag.Value;

                    _lastRotObjectSpawner = new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
                    _lastPosObjectSpawner = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
                    _lastUseBoneRotObjectSpawner = ObjectSpawnerUseBoneRot.Value;

                    _lastRotStatic2 = new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
                    _lastPosStatic2 = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
                    _lastUseBoneRotStatic2 = Static2UseBoneRot.Value;
                    _lastLagStatic2 = Static2Lag.Value;

                    _lastRotStatic3 = new Vector3(Static3RotX.Value, Static3RotY.Value, Static3RotZ.Value);
                    _lastPosStatic3 = new Vector3(Static3PosX.Value, Static3PosY.Value, Static3PosZ.Value);
                    _lastUseBoneRotStatic3 = Static3UseBoneRot.Value;
                    _lastLagStatic3 = Static3Lag.Value;

                    _lastRotMoveStyle = new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
                    _lastPosMoveStyle = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
                    _lastUseBoneRotMoveStyle = MoveStyleUseBoneRot.Value;
                    _lastLagMoveStyle = MoveStyleLag.Value;

                    _lastRotMoveStyleBoost = new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
                    _lastPosMoveStyleBoost = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
                    _lastUseBoneRotMoveStyleBoost = MoveStyleBoostUseBoneRot.Value;
                    _lastLagMoveStyleBoost = MoveStyleBoostLag.Value;

                    _lastRotManual2 = new Vector3(Manual2RotX.Value, Manual2RotY.Value, Manual2RotZ.Value);
                    _lastPosManual2 = new Vector3(Manual2PosX.Value, Manual2PosY.Value, Manual2PosZ.Value);
                    _lastUseBoneRotManual2 = Manual2UseBoneRot.Value;
                    _lastLagManual2 = Manual2Lag.Value;

                    _lastRotPhone = new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
                    _lastPosPhone = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
                    _lastUseBoneRotPhone = PhoneUseBoneRot.Value;
                    _lastLagPhone = PhoneLag.Value;

                    _lastRotHyperBoost = new Vector3(HyperBoostRotX.Value, HyperBoostRotY.Value, HyperBoostRotZ.Value);
                    _lastPosHyperBoost = new Vector3(HyperBoostPosX.Value, HyperBoostPosY.Value, HyperBoostPosZ.Value);
                    _lastUseBoneRotHyperBoost = HyperBoostUseBoneRot.Value;
                    _lastLagHyperBoost = HyperBoostLag.Value;

                    _lastRotHyperCharging = new Vector3(HyperChargingRotX.Value, HyperChargingRotY.Value, HyperChargingRotZ.Value);
                    _lastPosHyperCharging = new Vector3(HyperChargingPosX.Value, HyperChargingPosY.Value, HyperChargingPosZ.Value);
                    _lastUseBoneRotHyperCharging = HyperChargingUseBoneRot.Value;
                    _lastLagHyperCharging = HyperChargingLag.Value;
                }
            }
            else if (!IsEnabled.Value && _activePuppets.Count > 0)
            {
                DestroyPuppets();
            }

            UpdatePuppetLogic(player, visualRoot, visualObj);
        }

        private void SpawnPuppets(Transform playerVisual)
        {
            _boneMap.Clear();
            _boneCategoryMap.Clear();
            _puppetStateCache.Clear();
            _puppetCategoryMap.Clear();
            _mainCameraFollowers.Clear();

            // Initialize the bone count at spawn to avoid redundant refreshes.
            _lastBoneCount = playerVisual.GetComponentsInChildren<Transform>(true).Length;

            Logger.LogInfo("--- Spawning Puppets ---");

            foreach (var kvp in _prefabCategoryMap)
            {
                GameObject prefab = kvp.Key;
                EffectCategory category = kvp.Value;

                if (prefab == null) continue;

                GameObject puppet = Instantiate(prefab);
                puppet.name = $"{prefab.name}_{category}_Puppet";

                var allChildren = puppet.GetComponentsInChildren<Transform>(true).ToList();
                bool hasAuthKey = false;

                foreach (var child in allChildren)
                {
                    if (child.name.Equals("AuthKey", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasAuthKey = true;
                        break;
                    }
                }

                string logStatus = $"Prefab: '{prefab.name}' [{category}] | AuthKey: {hasAuthKey}";

                if (!hasAuthKey)
                {
                    Logger.LogWarning($"{logStatus} -> REJECTED (No AuthKey found).");
                    DestroyImmediate(puppet);
                    continue;
                }

                Logger.LogInfo($"{logStatus} -> Accepted.");
                _activePuppets.Add(puppet);
                _puppetCategoryMap[puppet] = category;
                _puppetStateCache[puppet] = false;

                // Object Spawner uses independent world-space transforms
                if (category == EffectCategory.ObjectSpawner)
                {
                    GetCategorySettings(category, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);

                    // 1. Set the initial position
                    puppet.transform.position = playerVisual.position + (playerVisual.rotation * posOffset);

                    // 2. Set the initial rotation
                    if (useBoneRot)
                        puppet.transform.rotation = playerVisual.rotation * rotOffset;
                    else
                        puppet.transform.rotation = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0) * rotOffset;

                    puppet.transform.localScale = Vector3.one;

                    // 3. Align bones once
                    MapBonesRecursive(puppet.transform, playerVisual, oneShot: true, category);

                }
                else
                {
                    // Standard bone attachment
                    MapBonesRecursive(puppet.transform, playerVisual, oneShot: false, category);
                }
            }
        }

        // Find the active rendering camera without relying on internal fields
        private Transform GetActiveCameraTransform()
        {
            if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
            {
                return Camera.main.transform;
            }

            // Use a fallback if MainCamera is untagged or temporarily disabled, such as during cutscenes
            var allCams = Camera.allCameras;
            for (int i = 0; i < allCams.Length; i++)
            {
                var cam = allCams[i];

                if (cam.isActiveAndEnabled && cam.targetTexture == null)
                {
                    return cam.transform;
                }
            }
            return null;
        }

        private void MapBonesRecursive(Transform puppetPart, Transform playerPart, bool oneShot, EffectCategory category)
        {
            if (puppetPart == null) return;

            // Handle MainCamera attachments separately from bone attachments
            if (puppetPart.name.Equals("MainCamera", System.StringComparison.OrdinalIgnoreCase))
            {
                GetCategorySettings(category, out Vector3 cPosOffset, out Quaternion cRotOffset, out bool cUseBoneRot, out float cLag);

                if (oneShot)
                {
                    Transform cam = GetActiveCameraTransform();

                    if (cam != null)
                    {
                        if (cUseBoneRot)
                        {
                            puppetPart.position = cam.position + (cam.rotation * cPosOffset);
                            puppetPart.rotation = cam.rotation * cRotOffset;
                        }
                        else
                        {
                            puppetPart.position = cam.position + (Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * cPosOffset);
                            puppetPart.rotation = Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * cRotOffset;
                        }
                    }
                }
                else
                {
                    _mainCameraFollowers.Add(puppetPart);
                    _boneCategoryMap[puppetPart] = category;
                }

                foreach (Transform child in puppetPart) MapBonesRecursive(child, playerPart, oneShot, category);

                return;
            }

            Transform match = FindBone(playerPart, puppetPart.name);

            if (match != null)
            {
                GetCategorySettings(category, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);

                if (oneShot)
                {
                    // Align the transform once during mapping
                    if (useBoneRot)
                    {
                        puppetPart.position = match.position + (match.rotation * posOffset);
                        puppetPart.rotation = match.rotation * rotOffset;
                    }
                    else
                    {
                        puppetPart.position = match.position + (Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * posOffset);
                        puppetPart.rotation = Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * rotOffset;
                    }
                }
                else
                {
                    // Track the transform for LateUpdate updates
                    _boneMap[puppetPart] = match;
                    _boneCategoryMap[puppetPart] = category;
                }
            }

            foreach (Transform child in puppetPart)
                MapBonesRecursive(child, playerPart, oneShot, category);
        }

        private void GetCategorySettings(EffectCategory category, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag)
        {
            switch (category)
            {
                case EffectCategory.TrickGlow:
                    posOffset = new Vector3(TrickGlowPosX.Value, TrickGlowPosY.Value, TrickGlowPosZ.Value);
                    rotOffset = Quaternion.Euler(TrickGlowRotX.Value, TrickGlowRotY.Value, TrickGlowRotZ.Value);
                    useBoneRot = TrickGlowUseBoneRot.Value;
                    lag = TrickGlowLag.Value;
                    break;
                case EffectCategory.Boost:
                    posOffset = new Vector3(BoostPosX.Value, BoostPosY.Value, BoostPosZ.Value);
                    rotOffset = Quaternion.Euler(BoostRotX.Value, BoostRotY.Value, BoostRotZ.Value);
                    useBoneRot = BoostUseBoneRot.Value;
                    lag = BoostLag.Value;
                    break;
                case EffectCategory.BoostTrick:
                    posOffset = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
                    rotOffset = Quaternion.Euler(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
                    useBoneRot = BoostTrickUseBoneRot.Value;
                    lag = BoostTrickLag.Value;
                    break;
                case EffectCategory.Static:
                    posOffset = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
                    rotOffset = Quaternion.Euler(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
                    useBoneRot = StaticUseBoneRot.Value;
                    lag = StaticLag.Value;
                    break;
                case EffectCategory.MiniBoost:
                    posOffset = new Vector3(MiniBoostPosX.Value, MiniBoostPosY.Value, MiniBoostPosZ.Value);
                    rotOffset = Quaternion.Euler(MiniBoostRotX.Value, MiniBoostRotY.Value, MiniBoostRotZ.Value);
                    useBoneRot = MiniBoostUseBoneRot.Value;
                    lag = MiniBoostLag.Value;
                    break;
                case EffectCategory.Manual:
                    posOffset = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
                    rotOffset = Quaternion.Euler(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
                    useBoneRot = ManualUseBoneRot.Value;
                    lag = ManualLag.Value;
                    break;
                case EffectCategory.ObjectSpawner:
                    posOffset = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
                    rotOffset = Quaternion.Euler(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
                    useBoneRot = ObjectSpawnerUseBoneRot.Value;
                    lag = 0f;
                    break;
                case EffectCategory.Static2:
                    posOffset = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
                    rotOffset = Quaternion.Euler(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
                    useBoneRot = Static2UseBoneRot.Value;
                    lag = Static2Lag.Value;
                    break;
                case EffectCategory.Static3:
                    posOffset = new Vector3(Static3PosX.Value, Static3PosY.Value, Static3PosZ.Value);
                    rotOffset = Quaternion.Euler(Static3RotX.Value, Static3RotY.Value, Static3RotZ.Value);
                    useBoneRot = Static3UseBoneRot.Value;
                    lag = Static3Lag.Value;
                    break;
                case EffectCategory.MoveStyle:
                    posOffset = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
                    rotOffset = Quaternion.Euler(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
                    useBoneRot = MoveStyleUseBoneRot.Value;
                    lag = MoveStyleLag.Value;
                    break;
                case EffectCategory.MoveStyleBoost:
                    posOffset = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
                    rotOffset = Quaternion.Euler(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
                    useBoneRot = MoveStyleBoostUseBoneRot.Value;
                    lag = MoveStyleBoostLag.Value;
                    break;
                case EffectCategory.Manual2:
                    posOffset = new Vector3(Manual2PosX.Value, Manual2PosY.Value, Manual2PosZ.Value);
                    rotOffset = Quaternion.Euler(Manual2RotX.Value, Manual2RotY.Value, Manual2RotZ.Value);
                    useBoneRot = Manual2UseBoneRot.Value;
                    lag = Manual2Lag.Value;
                    break;
                case EffectCategory.Phone:
                    posOffset = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
                    rotOffset = Quaternion.Euler(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
                    useBoneRot = PhoneUseBoneRot.Value;
                    lag = PhoneLag.Value;
                    break;
                case EffectCategory.HyperBoost:
                    posOffset = new Vector3(HyperBoostPosX.Value, HyperBoostPosY.Value, HyperBoostPosZ.Value);
                    rotOffset = Quaternion.Euler(HyperBoostRotX.Value, HyperBoostRotY.Value, HyperBoostRotZ.Value);
                    useBoneRot = HyperBoostUseBoneRot.Value;
                    lag = HyperBoostLag.Value;
                    break;
                case EffectCategory.HyperCharging:
                    posOffset = new Vector3(HyperChargingPosX.Value, HyperChargingPosY.Value, HyperChargingPosZ.Value);
                    rotOffset = Quaternion.Euler(HyperChargingRotX.Value, HyperChargingRotY.Value, HyperChargingRotZ.Value);
                    useBoneRot = HyperChargingUseBoneRot.Value;
                    lag = HyperChargingLag.Value;
                    break;
                default:
                    posOffset = Vector3.zero;
                    rotOffset = Quaternion.identity;
                    useBoneRot = true;
                    lag = 0f;
                    break;
            }
        }

        private void UpdatePuppetLogic(Player player, Transform playerVisual, object visualObj)
        {
            if (_activePuppets.Count == 0) return;

            // 0. Detect base player states
            bool currentlyBoosting = false;

            if (_boostingField != null)
            {
                try
                {
                    currentlyBoosting = (bool)_boostingField.GetValue(player);
                }
                catch { }
            }

            object currentAbility = _abilityField?.GetValue(player);

            bool currentlyTricking = CheckIfTricking(player);
            if (currentlyTricking) _lastTrickTime = Time.time;

            bool currentlyBoostTricking = false;
            if (_checkBoostTrickMethod != null)
            {
                try
                {
                    currentlyBoostTricking = (bool)_checkBoostTrickMethod.Invoke(player, null);
                }
                catch { }
            }

            // Prevent false boost-trick detections
            if (!currentlyTricking)
            {
                currentlyBoostTricking = false;
            }

            if (currentAbility != null)
            {
                string abilityName = currentAbility.GetType().Name;

                if (abilityName == "BoostAbility")
                {
                    currentlyBoostTricking = false;
                }
                else if (abilityName == "GrindAbility" || currentAbility is GrindAbility)
                {
                    currentlyBoostTricking = false;
                }
            }

            // Detect grind boost tricks
            if (!currentlyBoostTricking && currentAbility != null && currentlyBoosting)
            {
                if (currentAbility.GetType().Name == "GrindAbility" || currentAbility is GrindAbility)
                {
                    try
                    {
                        bool isGrindBoostTrick = (bool)_curTrickBoostField.GetValue(currentAbility);
                        float gTime = (float)_grindTrickTimerField.GetValue(currentAbility);
                        if (isGrindBoostTrick && gTime > 0f) currentlyBoostTricking = true;
                    }
                    catch { }
                }
            }

            if (currentlyBoostTricking)
            {
                _lastBoostTrickTime = Time.time;
                _lastRegularBoostTime = -100f; // Clear the block when a valid trick is detected
            }

            bool isRegularBoosting = currentlyBoosting && !currentlyBoostTricking;
            if (isRegularBoosting)
            {
                _lastRegularBoostTime = Time.time;
            }

            // Detect MoveStyle state
            MoveStyle currentMoveStyle = MoveStyle.ON_FOOT;

            if (_moveStyleField != null)
            {
                try
                {
                    currentMoveStyle = (MoveStyle)_moveStyleField.GetValue(player);
                }
                catch { }
            }
            bool isMoveStyleActive = currentMoveStyle != MoveStyle.ON_FOOT;
            bool moveStyleBoostActive = isMoveStyleActive && currentlyBoosting;

            // Track MoveStyle state for delayed-effect freezing
            _isMoveStyleActiveForFreeze = isMoveStyleActive;
            _isMoveStyleBoostActiveForFreeze = moveStyleBoostActive;

            // Detect phone state
            bool isPhoneActive = false;

            if (_phoneActiveField != null && visualObj != null)
            {
                try
                {
                    isPhoneActive = (bool)_phoneActiveField.GetValue(visualObj);
                }
                catch { }
            }

            // Detect HyperBoost and HyperCharging states
            bool isHyperBoostingAPI = false;
            bool isHyperBoostAbility = false;
            bool isHyperCharging = false;

            // 1. Check the active HyperBoostAbility
            if (currentAbility != null && currentAbility.GetType().Name == "HyperBoostAbility")
            {
                isHyperBoostAbility = true;
                try
                {
                    // Check the ability state (usually START_BOOST / 0).
                    var stateField = currentAbility.GetType().GetField("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (stateField != null)
                    {
                        string stateStr = stateField.GetValue(currentAbility).ToString();
                        if (stateStr == "0" || stateStr == "START_BOOST") isHyperCharging = true;
                    }

                    // Check the QTE state.
                    var qteField = currentAbility.GetType().GetField("isWaitingForQTE", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (qteField != null)
                    {
                        if ((bool)qteField.GetValue(currentAbility)) isHyperCharging = true;
                    }
                }
                catch { }
            }

            // 2. Check the API flag in case it is set without an active ability
            if (Chainloader.PluginInfos.ContainsKey("com.desdezerotapes.hyperboost"))
            {
                Type hyperBoostType = Type.GetType("HyperBoostMod.Plugin, HyperBoostMod");
                if (hyperBoostType != null)
                {
                    MethodInfo checkMethod = hyperBoostType.GetMethod("IsPlayerHyperBoosting", BindingFlags.Public | BindingFlags.Static);
                    if (checkMethod != null)
                    {
                        isHyperBoostingAPI = (bool)checkMethod.Invoke(null, new object[] { player });
                    }
                }
            }

            // 3. Update timers based on the detected state
            if (isHyperBoostAbility)
            {
                if (isHyperCharging)
                {
                    _lastHyperChargingTime = Time.time;
                }
                else
                {
                    _lastHyperBoostTime = Time.time;
                }
            }
            else if (isHyperBoostingAPI)
            {
                // Handle the API reporting active outside HyperBoostAbility execution
                _lastHyperBoostTime = Time.time;
            }

            // 1. Calculate boost-trick state
            float timeSinceBoostTrick = Time.time - _lastBoostTrickTime;
            bool boostTrickInTimeWindow = (timeSinceBoostTrick <= BoostTrickDelay.Value);
            bool boostTrickVisualReady = (timeSinceBoostTrick >= 0.2f);
            bool boostTrickActive = boostTrickInTimeWindow && boostTrickVisualReady;

            // Apply the boost override
            if (BoostTrickOverrideByBoosting.Value)
            {
                float timeSinceRegularBoost = Time.time - _lastRegularBoostTime;
                bool blockedByRegularBoost = (timeSinceRegularBoost <= BoostTrickDelay.Value);

                if (blockedByRegularBoost && !currentlyBoostTricking)
                {
                    boostTrickActive = false;
                    _lastBoostTrickTime = -100f; // Clear the timer to prevent delayed effects after the override expires
                }
            }

            // 2. Calculate Trick Glow state
            bool trickGlowActive = false;

            if (TrickGlowEnabled.Value)
            {
                trickGlowActive = (Time.time - _lastTrickTime <= TrickGlowDelay.Value);

                if (BoostTrickEnabled.Value && boostTrickInTimeWindow)
                {
                    trickGlowActive = false;
                }
            }

            // 3. Calculate Mini Boost state
            bool miniBoostActive = false;

            if (MiniBoostEnabled.Value)
            {
                bool isJumping = false;

                try { isJumping = (bool)_isJumpingField.GetValue(player); } catch { }

                if (isJumping)
                {
                    float jumpTime = 0f;

                    try { jumpTime = (float)_timeSinceLastJumpField.GetValue(player); } catch { }
                    if (jumpTime < 0.15f) miniBoostActive = true;
                }

                if (!miniBoostActive && currentAbility != null)
                {
                    if (currentAbility is GrindAbility)
                    {
                        float gTime = 0f;
                        try
                        {
                            gTime = (float)_grindTrickTimerField.GetValue(currentAbility);
                        }
                        catch { }
                        if (gTime <= 0.325f) miniBoostActive = true;

                    }
                    else if (currentAbility is WallrunLineAbility && !currentlyBoosting)
                    {
                        miniBoostActive = true;

                    }
                    else if (currentAbility is AirDashAbility)
                    {
                        miniBoostActive = true;

                    }
                    else if (currentAbility is SlideAbility)
                    {
                        try
                        {
                            object stateObj = _slideStateField.GetValue(currentAbility);
                            string stateName = stateObj.ToString();

                            if (stateName == "SLOPE" || stateName == "SUPER")
                            {
                                miniBoostActive = true;
                            }
                        }
                        catch { }
                    }
                }
            }

            bool isAirDash = (currentAbility is AirDashAbility);

            if (currentlyBoosting && BoostSwapEnabled.Value && !isAirDash)
            {
                miniBoostActive = false;
            }

            // 4. Apply delays and overrides
            if (currentlyBoosting) _lastBoostTime = Time.time;
            bool finalBoostActive = (Time.time - _lastBoostTime <= BoostDelay.Value);

            if (miniBoostActive) _lastMiniBoostTime = Time.time;
            bool finalMiniBoostActive = (Time.time - _lastMiniBoostTime <= MiniBoostDelay.Value);

            if (isMoveStyleActive) _lastMoveStyleTime = Time.time;
            bool finalMoveStyleActive = (Time.time - _lastMoveStyleTime <= MoveStyleDelay.Value);

            if (moveStyleBoostActive) _lastMoveStyleBoostTime = Time.time;
            bool finalMoveStyleBoostActive = (Time.time - _lastMoveStyleBoostTime <= MoveStyleBoostDelay.Value);

            bool finalHyperChargingActive = (Time.time - _lastHyperChargingTime <= HyperChargingDelay.Value);
            bool finalHyperBoostActive = (Time.time - _lastHyperBoostTime <= HyperBoostDelay.Value);

            // Apply the HyperBoost boost override
            if (HyperBoostBoostOverride.Value && finalHyperBoostActive)
            {
                finalBoostActive = false;
            }

            // Apply the HyperBoost MoveStyle boost override
            if (HyperBoostMoveStyleBoostOverride.Value && finalHyperBoostActive)
            {
                finalMoveStyleBoostActive = false;
            }

            // Calculate active targets before activation snapping
            Dictionary<GameObject, bool> targetStates = new Dictionary<GameObject, bool>();

            foreach (var puppet in _activePuppets)
            {
                if (puppet == null) continue;

                EffectCategory cat = _puppetCategoryMap[puppet];
                bool shouldBeActive = false;

                switch (cat)
                {
                    case EffectCategory.TrickGlow:
                        shouldBeActive = trickGlowActive;
                        break;
                    case EffectCategory.Boost:
                        shouldBeActive = finalBoostActive;
                        break;
                    case EffectCategory.BoostTrick:
                        shouldBeActive = boostTrickActive;
                        break;
                    case EffectCategory.Static:
                        shouldBeActive = true;
                        break;
                    case EffectCategory.MiniBoost:
                        shouldBeActive = finalMiniBoostActive;
                        break;
                    case EffectCategory.Manual:
                        shouldBeActive = _manualToggleActive;
                        if (_forceManualRetrigger) _puppetStateCache[puppet] = false;
                        break;
                    case EffectCategory.Manual2:
                        shouldBeActive = _manual2ToggleActive;
                        if (_forceManual2Retrigger) _puppetStateCache[puppet] = false;
                        break;
                    case EffectCategory.ObjectSpawner:
                        shouldBeActive = true;
                        break;
                    case EffectCategory.Static2:
                        shouldBeActive = true;
                        break;
                    case EffectCategory.Static3:
                        shouldBeActive = true;
                        break;
                    case EffectCategory.MoveStyle:
                        shouldBeActive = finalMoveStyleActive;
                        break;
                    case EffectCategory.MoveStyleBoost:
                        shouldBeActive = finalMoveStyleBoostActive;
                        break;
                    case EffectCategory.Phone:
                        shouldBeActive = isPhoneActive;
                        break;
                    case EffectCategory.HyperBoost:
                        shouldBeActive = finalHyperBoostActive;
                        break;
                    case EffectCategory.HyperCharging:
                        shouldBeActive = finalHyperChargingActive;
                        break;
                }
                targetStates[puppet] = shouldBeActive;
            }

            if (_forceManualRetrigger) _forceManualRetrigger = false;
            if (_forceManual2Retrigger) _forceManual2Retrigger = false;

            // Apply root transforms and activation snapping
            foreach (var puppet in _activePuppets)
            {
                if (puppet == null) continue;
                EffectCategory cat = _puppetCategoryMap[puppet];
                bool shouldBeActive = targetStates[puppet];
                bool wasActive = _puppetStateCache.ContainsKey(puppet) ? _puppetStateCache[puppet] : false;
                bool justActivated = shouldBeActive && !wasActive;

                // Keep MoveStyle effects in place only when unequipped (ON_FOOT).
                bool freezeTransform = false;
                if (cat == EffectCategory.MoveStyle)
                {
                    if (MoveStyleFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeTransform = true;
                }
                else if (cat == EffectCategory.MoveStyleBoost)
                {
                    if (MoveStyleBoostFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeTransform = true;
                }

                if (freezeTransform)
                {
                    continue;
                }

                if (cat != EffectCategory.ObjectSpawner && !_boneMap.ContainsKey(puppet.transform) && !_mainCameraFollowers.Contains(puppet.transform))
                {
                    GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);
                    Vector3 targetPos;
                    Quaternion targetRot;

                    if (useBoneRot)
                    {
                        targetPos = playerVisual.position + (playerVisual.rotation * posOffset);
                        targetRot = playerVisual.rotation * rotOffset;
                    }
                    else
                    {
                        targetPos = playerVisual.position + (Quaternion.Euler(0, playerVisual.eulerAngles.y, 0) * posOffset);
                        targetRot = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0) * rotOffset;
                    }

                    // Smooth movement, or snap to the target when the effect activates.
                    if (lag > 0.01f && !justActivated)
                    {
                        float speed = 20f / (1f + lag);
                        puppet.transform.position = Vector3.Lerp(puppet.transform.position, targetPos, Time.deltaTime * speed);
                        puppet.transform.rotation = Quaternion.Slerp(puppet.transform.rotation, targetRot, Time.deltaTime * speed);
                    }
                    else
                    {
                        puppet.transform.position = targetPos;
                        puppet.transform.rotation = targetRot;
                    }

                    puppet.transform.localScale = Vector3.one;
                }
            }

            // Apply bone transforms and activation snapping
            foreach (var pair in _boneMap)
            {
                if (pair.Key == null || pair.Value == null) continue;

                GameObject rootPuppet = pair.Key.root.gameObject;
                bool justActivated = false;
                bool shouldBeActive = false;
                EffectCategory rootCat = EffectCategory.TrickGlow; // Default category before the root puppet is identified

                if (rootPuppet != null)
                {
                    if (targetStates.TryGetValue(rootPuppet, out shouldBeActive))
                    {
                        bool wasActive = _puppetStateCache.ContainsKey(rootPuppet) ? _puppetStateCache[rootPuppet] : false;
                        justActivated = shouldBeActive && !wasActive;
                    }
                    _puppetCategoryMap.TryGetValue(rootPuppet, out rootCat);
                }

                // Keep MoveStyle bone effects in place only when unequipped (ON_FOOT).
                bool freezeBone = false;
                if (rootCat == EffectCategory.MoveStyle)
                {
                    if (MoveStyleFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeBone = true;
                }
                else if (rootCat == EffectCategory.MoveStyleBoost)
                {
                    if (MoveStyleBoostFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeBone = true;
                }

                if (freezeBone)
                {
                    continue;
                }

                if (_boneCategoryMap.TryGetValue(pair.Key, out EffectCategory cat))
                {
                    GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);

                    Vector3 targetPos;
                    Quaternion targetRot;

                    if (useBoneRot)
                    {
                        // Method 1: Follow the bone's position and rotation
                        targetPos = pair.Value.position + (pair.Value.rotation * posOffset);
                        targetRot = pair.Value.rotation * rotOffset;
                    }
                    else
                    {
                        // Method 2: Offset from the bone using the player's upright orientation
                        Quaternion playerFacing = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0);
                        targetPos = pair.Value.position + (playerFacing * posOffset);
                        targetRot = playerFacing * rotOffset;
                    }

                    // Smooth movement, or snap to the target when the effect activates.
                    if (lag > 0.01f && !justActivated)
                    {
                        float speed = 20f / (1f + lag);
                        pair.Key.position = Vector3.Lerp(pair.Key.position, targetPos, Time.deltaTime * speed);
                        pair.Key.rotation = Quaternion.Slerp(pair.Key.rotation, targetRot, Time.deltaTime * speed);

                    }
                    else
                    {
                        pair.Key.position = targetPos;
                        pair.Key.rotation = targetRot;
                    }
                }
                else
                {
                    // Fall back to the matched bone transform
                    pair.Key.position = pair.Value.position;
                    pair.Key.rotation = pair.Value.rotation;
                }
            }

            // Update MainCamera attachments and activation snapping
            UpdateCameraFollowers(false, targetStates);

            // Update effect visuals after transforms
            foreach (var puppet in _activePuppets)
            {
                if (puppet == null) continue;
                UpdateEffectState(puppet, targetStates[puppet]);
            }
        }

        private void OnBeforeRender()
        {
            if (IsEnabled == null || !IsEnabled.Value || _activePuppets.Count == 0) return;
            // Update camera attachments just before rendering to reduce visible camera lag.
            UpdateCameraFollowers(true, null);
        }

        private void UpdateCameraFollowers(bool isBeforeRender, Dictionary<GameObject, bool> currentStates)
        {
            if (_mainCameraFollowers.Count == 0) return;

            Transform activeCam = GetActiveCameraTransform();
            if (activeCam == null) return;

            Player player = WorldHandler.instance?.GetCurrentPlayer();
            Transform playerVisual = null;
            if (player != null)
            {
                object visualObj = _characterVisualField?.GetValue(player);
                if (visualObj != null) playerVisual = ExtractTransform(visualObj);
            }

            foreach (var camFollower in _mainCameraFollowers)
            {
                if (camFollower == null) continue;

                GameObject rootPuppet = camFollower.root.gameObject;
                bool justActivated = false;
                bool shouldBeActive = false;

                if (rootPuppet != null)
                {
                    if (currentStates != null && currentStates.ContainsKey(rootPuppet))
                    {
                        shouldBeActive = currentStates[rootPuppet];
                        bool wasActive = _puppetStateCache.ContainsKey(rootPuppet) && _puppetStateCache[rootPuppet];
                        justActivated = shouldBeActive && !wasActive;
                    }
                    else if (_puppetStateCache.ContainsKey(rootPuppet))
                    {
                        shouldBeActive = _puppetStateCache[rootPuppet];
                    }

                    // Keep camera-attached MoveStyle effects in place while unequipped.
                    if (_puppetCategoryMap.TryGetValue(rootPuppet, out EffectCategory rootCat))
                    {
                        bool freezeCam = false;
                        if (rootCat == EffectCategory.MoveStyle)
                        {
                            if (MoveStyleFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeCam = true;
                        }
                        else if (rootCat == EffectCategory.MoveStyleBoost)
                        {
                            if (MoveStyleBoostFreezeParticleKill.Value && !_isMoveStyleActiveForFreeze) freezeCam = true;
                        }

                        if (freezeCam)
                        {
                            continue;
                        }
                    }
                }

                // Keep camera attachments updated even when effects are inactive.
                // Lingering particles still need the camera's current transform.

                if (_boneCategoryMap.TryGetValue(camFollower, out EffectCategory cat))
                {
                    GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);

                    Vector3 targetPos;
                    Quaternion targetRot;

                    // When enabled, use the camera's position and rotation for the attachment.
                    // When disabled, use the camera's position and the player's facing direction.
                    if (useBoneRot)
                    {
                        targetPos = activeCam.position + (activeCam.rotation * posOffset);
                        targetRot = activeCam.rotation * rotOffset;
                    }
                    else
                    {
                        if (playerVisual != null)
                        {
                            Quaternion playerFacing = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0);
                            targetPos = activeCam.position + (playerFacing * posOffset);
                            targetRot = playerFacing * rotOffset;
                        }
                        else
                        {
                            targetPos = activeCam.position + posOffset;
                            targetRot = activeCam.rotation * rotOffset;
                        }
                    }

                    if (lag > 0.01f && !justActivated)
                    {
                        // OnBeforeRender has inconsistent deltaTime.
                        // Run interpolation in LateUpdate.
                        if (!isBeforeRender)
                        {
                            float speed = 20f / (1f + lag);
                            camFollower.position = Vector3.Lerp(camFollower.position, targetPos, Time.deltaTime * speed);
                            camFollower.rotation = Quaternion.Slerp(camFollower.rotation, targetRot, Time.deltaTime * speed);
                        }
                    }
                    else
                    {
                        // Snap to the target in OnBeforeRender to reduce normal camera update lag.
                        camFollower.position = targetPos;
                        camFollower.rotation = targetRot;
                    }
                }
            }
        }

        private void UpdateEffectState(GameObject fx, bool active)
        {
            if (fx == null) return;

            bool wasActive = _puppetStateCache.ContainsKey(fx) ? _puppetStateCache[fx] : false;

            if (active && !wasActive)
            {
                var animators = fx.GetComponentsInChildren<Animator>(true);
                foreach (var anim in animators) { anim.Rebind(); anim.Update(0f); }

                var legacyAnims = fx.GetComponentsInChildren<Animation>(true);
                foreach (var anim in legacyAnims) { anim.Rewind(); anim.Play(); }
            }

            _puppetStateCache[fx] = active;

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = ps.emission;
                if (emission.enabled != active) emission.enabled = active;
                if (active && !wasActive) ps.Play();
            }

            var trails = fx.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails)
            {
                if (trail.emitting != active) trail.emitting = active;
                if (active && !wasActive) trail.Clear(); // Clear streaks caused by position snapping.
            }

            var sounds = fx.GetComponentsInChildren<AudioSource>(true);
            foreach (var source in sounds)
            {
                if (active)
                {
                    if (!wasActive) source.Play();
                }
                else
                {
                    if (source.isPlaying) source.Stop();
                }
            }

            // Toggle regular renderers here; particle systems and trails are handled separately above.
            var renderers = fx.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (r.enabled != active) r.enabled = active;
            }

            // Toggle cameras with the rest of the effect components.
            var cameras = fx.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras) if (cam.enabled != active) cam.enabled = active;

            // Toggle colliders alongside the effect visuals.
            var colliders = fx.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders) if (col.enabled != active) col.enabled = active;
        }

        private void DestroyPuppets()
        {
            foreach (var p in _activePuppets) if (p != null) Destroy(p);

            _activePuppets.Clear();
            _puppetStateCache.Clear();
            _puppetCategoryMap.Clear();
            _boneMap.Clear();
            _boneCategoryMap.Clear();
            _mainCameraFollowers.Clear();
        }

        private bool CheckIfTricking(Player p)
        {
            if (p.IsComboing()) return true;

            object abilityObj = _abilityField?.GetValue(p);
            if (abilityObj != null)
            {
                string typeName = abilityObj.GetType().Name;
                return typeName.Contains("Grind") || typeName.Contains("Wallrun") ||
                       typeName.Contains("Slide") || typeName.Contains("Trick");
            }
            return false;
        }

        private Transform ExtractTransform(object visualObj)
        {
            try
            {
                return ((GameObject)visualObj.GetType().GetProperty("gameObject").GetValue(visualObj, null)).transform;
            }
            catch
            {
                return null;
            }
        }

        private Transform FindBone(Transform root, string name)
        {
            // Ignore puppet transforms to prevent SwagSpawnStation rotation loops.
            if (root.name.ToLowerInvariant().Contains("puppet")) return null;

            // Ignore transforms ending in _NPC so effects are not attached to NPCs.
            if (root.name.EndsWith("_NPC", System.StringComparison.OrdinalIgnoreCase)) return null;

            if (root.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return root;

            foreach (Transform child in root)
            {
                Transform found = FindBone(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            DestroyPuppets();
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            _harmony?.UnpatchSelf();
            DestroyPuppets();
        }
    }
}