using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NCMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("NuclearOption.exe")]
    public sealed class NuclearOptionTrainer : BaseUnityPlugin
    {
        public const string PluginGuid = "ua.ncmod.nuclearoption.trainer";
        public const string PluginName = "NCMod Trainer and Cockpit Physics";
        public const string PluginVersion = "1.1.2";

        private const int WindowId = 340101;
        private const float FeedLifetime = 5.0f;
        private const int MaxFeedEntries = 8;

        private static readonly object FeedLock = new object();
        private static readonly List<FeedEntry> Feed = new List<FeedEntry>();

        internal static NuclearOptionTrainer Instance;
        internal static ManualLogSource LogSource;
        internal static bool UnlimitedAmmo;
        internal static bool UnlimitedFuel;
        internal static bool CockpitPhysics;
        internal static bool DamageFeed;
        internal static bool HideTargetMarkers;

        private Harmony _harmony;
        private Rect _windowRect = new Rect(24f, 80f, 330f, 0f);
        private int _rankTarget;
        private string _status = "Ready. Single-player or host authority required for funds/rank.";
        private float _statusUntil;
        private bool _menuVisible;
        private HudHideMode _hudHideMode;
        private float _nextCanvasSweep;
        private readonly Dictionary<int, CanvasRestoreState> _hiddenCanvases =
            new Dictionary<int, CanvasRestoreState>();
        private readonly Dictionary<int, GameObjectRestoreState> _hiddenMarkerObjects =
            new Dictionary<int, GameObjectRestoreState>();

        private ConfigEntry<KeyboardShortcut> _menuKey;
        private ConfigEntry<KeyboardShortcut> _alternateMenuKey;
        private ConfigEntry<KeyboardShortcut> _interfaceToggleKey;
        private ConfigEntry<KeyboardShortcut> _markersToggleKey;
        private ConfigEntry<bool> _unlimitedAmmoConfig;
        private ConfigEntry<bool> _unlimitedFuelConfig;
        private ConfigEntry<bool> _cockpitPhysicsConfig;
        private ConfigEntry<bool> _damageFeedConfig;

        internal ConfigEntry<float> PositionStrength;
        internal ConfigEntry<float> RotationStrength;
        internal ConfigEntry<float> ShakeStrength;
        internal ConfigEntry<float> MaxPositionOffset;
        internal ConfigEntry<float> MaxRotationOffset;
        internal ConfigEntry<float> SonicBoomShakeStrength;
        internal ConfigEntry<float> SonicBoomVolumeMultiplier;

        private GUIStyle _windowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _feedStyle;
        private GUIStyle _feedShadowStyle;

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;

            _menuKey = Config.Bind("Keybinds", "MenuToggleKey", new KeyboardShortcut(KeyCode.Insert),
                "Primary key used to show or hide the trainer window.");
            _alternateMenuKey = Config.Bind("Keybinds", "AlternateMenuToggleKey", new KeyboardShortcut(KeyCode.F10),
                "Alternate key used to show or hide the trainer window.");
            _interfaceToggleKey = Config.Bind("Keybinds", "InterfaceToggleKey", new KeyboardShortcut(KeyCode.F8),
                "Hide or restore screen-space HUD while preserving physical cockpit displays.");
            _markersToggleKey = Config.Bind("Keybinds", "TargetMarkersToggleKey", new KeyboardShortcut(KeyCode.F9),
                "Hide or restore target markers only. Cockpit displays and menus remain visible.");
            _unlimitedAmmoConfig = Config.Bind("Cheats", "UnlimitedAmmo", false,
                "Keep ammunition loaded on the locally controlled aircraft.");
            _unlimitedFuelConfig = Config.Bind("Cheats", "UnlimitedFuel", false,
                "Prevent fuel consumption on the locally controlled aircraft.");
            _cockpitPhysicsConfig = Config.Bind("CockpitPhysics", "Enabled", true,
                "Enable procedural cockpit head movement.");
            _damageFeedConfig = Config.Bind("DamageFeed", "Enabled", true,
                "Show vehicle-part damage and detachment messages.");
            PositionStrength = Config.Bind("CockpitPhysics", "PositionStrength", 1.0f,
                "Head translation intensity. Recommended range: 0.25 to 2.0.");
            RotationStrength = Config.Bind("CockpitPhysics", "RotationStrength", 1.0f,
                "Head rotation intensity. Recommended range: 0.25 to 2.0.");
            ShakeStrength = Config.Bind("CockpitPhysics", "ShakeStrength", 1.0f,
                "Turbulence, impulse and high-G shake intensity.");
            MaxPositionOffset = Config.Bind("CockpitPhysics", "MaxPositionOffset", 0.115f,
                "Hard safety clamp for procedural head translation, in metres.");
            MaxRotationOffset = Config.Bind("CockpitPhysics", "MaxRotationOffset", 8.0f,
                "Hard safety clamp for procedural head rotation, in degrees.");
            SonicBoomShakeStrength = Config.Bind("CockpitPhysics", "SonicBoomShakeStrength", 1.0f,
                "Cockpit impulse when the local aircraft crosses Mach 1.");
            SonicBoomVolumeMultiplier = Config.Bind("CockpitPhysics", "SonicBoomVolumeMultiplier", 1.8f,
                "Volume multiplier applied only to the game's sonic-boom clip.");

            UnlimitedAmmo = _unlimitedAmmoConfig.Value;
            UnlimitedFuel = _unlimitedFuelConfig.Value;
            CockpitPhysics = _cockpitPhysicsConfig.Value;
            DamageFeed = _damageFeedConfig.Value;

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(NuclearOptionTrainer).Assembly);
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded for Nuclear Option 0.34.x.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Harmony patching failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            SetHudHideMode(HudHideMode.Visible);
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            CockpitHeadMotion.Reset();
            Instance = null;
        }

        private void Update()
        {
            if (_menuKey.Value.IsDown() || _alternateMenuKey.Value.IsDown())
            {
                _menuVisible = !_menuVisible;
            }

            if (_interfaceToggleKey.Value.IsDown())
            {
                SetHudHideMode(_hudHideMode == HudHideMode.CleanScreen ? HudHideMode.Visible : HudHideMode.CleanScreen);
            }

            if (_markersToggleKey.Value.IsDown())
            {
                SetHudHideMode(_hudHideMode == HudHideMode.TargetMarkersOnly ? HudHideMode.Visible : HudHideMode.TargetMarkersOnly);
            }

            if (_hudHideMode != HudHideMode.Visible && Time.unscaledTime >= _nextCanvasSweep)
            {
                ApplyHudHideMode();
                _nextCanvasSweep = Time.unscaledTime + 0.35f;
            }

            RemoveExpiredFeedEntries();
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_menuVisible)
            {
                _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow,
                    "NCMod • Nuclear Option 0.34", _windowStyle, GUILayout.Width(330f));
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - 40f));
            }

            if (DamageFeed && _hudHideMode != HudHideMode.CleanScreen)
            {
                DrawDamageFeed();
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(2f);
            GUILayout.Label("ECONOMY", _headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10M", GUILayout.Height(26f)))
            {
                AddTeamFunds(10f);
            }
            if (GUILayout.Button("+100M", GUILayout.Height(26f)))
            {
                AddTeamFunds(100f);
            }
            if (GUILayout.Button("+500M", GUILayout.Height(26f)))
            {
                AddTeamFunds(500f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Adds millions to your personal account balance.", _smallStyle);

            GUILayout.Space(5f);
            GUILayout.Label("PROGRESSION", _headerStyle);
            Player player;
            int maxRank = GetMaximumRank(TryGetLocalPlayer(out player) ? player : null);
            _rankTarget = Mathf.Clamp(_rankTarget, 0, maxRank);
            GUILayout.Label("Rank: " + _rankTarget + " / " + maxRank, _smallStyle);
            _rankTarget = Mathf.RoundToInt(GUILayout.HorizontalSlider(_rankTarget, 0f, maxRank));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply rank", GUILayout.Height(24f)))
            {
                SetRank(_rankTarget);
            }
            if (GUILayout.Button("Max rank", GUILayout.Height(24f)))
            {
                _rankTarget = maxRank;
                SetRank(maxRank);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.Label("FLIGHT", _headerStyle);
            SetToggle(ref UnlimitedAmmo, _unlimitedAmmoConfig, "Unlimited ammo");
            SetToggle(ref UnlimitedFuel, _unlimitedFuelConfig, "Unlimited fuel");
            SetToggle(ref CockpitPhysics, _cockpitPhysicsConfig, "Dynamic cockpit head physics");
            SetToggle(ref DamageFeed, _damageFeedConfig, "Combat / damage feed");

            bool cleanScreen = GUILayout.Toggle(_hudHideMode == HudHideMode.CleanScreen,
                "Clean screen (keep cockpit displays)  [" + _interfaceToggleKey.Value + "]");
            if (cleanScreen != (_hudHideMode == HudHideMode.CleanScreen))
            {
                SetHudHideMode(cleanScreen ? HudHideMode.CleanScreen : HudHideMode.Visible);
            }
            bool markersOnly = GUILayout.Toggle(_hudHideMode == HudHideMode.TargetMarkersOnly,
                "Hide target markers only  [" + _markersToggleKey.Value + "]");
            if (markersOnly != (_hudHideMode == HudHideMode.TargetMarkersOnly))
            {
                SetHudHideMode(markersOnly ? HudHideMode.TargetMarkersOnly : HudHideMode.Visible);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Camera intensity: " + PositionStrength.Value.ToString("0.00", CultureInfo.InvariantCulture), _smallStyle);
            float cameraIntensity = GUILayout.HorizontalSlider(PositionStrength.Value, 0.25f, 2.0f);
            if (Math.Abs(cameraIntensity - PositionStrength.Value) > 0.001f)
            {
                PositionStrength.Value = cameraIntensity;
                RotationStrength.Value = cameraIntensity;
            }

            GUILayout.Space(5f);
            if (Time.unscaledTime < _statusUntil)
            {
                GUILayout.Label(_status, _smallStyle);
            }
            else
            {
                GUILayout.Label(_menuKey.Value + " / " + _alternateMenuKey.Value +
                                " menu • " + _interfaceToggleKey.Value + " clean • " +
                                _markersToggleKey.Value + " markers", _smallStyle);
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void SetToggle(ref bool runtimeValue, ConfigEntry<bool> config, string label)
        {
            bool value = GUILayout.Toggle(runtimeValue, label);
            if (value != runtimeValue)
            {
                runtimeValue = value;
                config.Value = value;
                if (!value && label.IndexOf("cockpit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CockpitHeadMotion.Reset();
                }
            }
        }

        private void AddTeamFunds(float millions)
        {
            Player player;
            if (!TryGetAuthoritativePlayer(out player))
            {
                ShowStatus("Player not found.");
                return;
            }

            try
            {
                float before = player.Allocation;
                player.AddAllocation(millions);
                float actual = player.Allocation;
                ShowStatus("Player funds: " + before.ToString("N2", CultureInfo.InvariantCulture) + "M → " +
                           actual.ToString("N2", CultureInfo.InvariantCulture) + "M (added " +
                           (actual - before).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "M).");
            }
            catch (Exception ex)
            {
                ShowStatus("Player-funds change failed; see BepInEx log.");
                Logger.LogError(ex);
            }
        }

        private void SetRank(int rank)
        {
            Player player;
            if (!TryGetAuthoritativePlayer(out player))
            {
                return;
            }

            try
            {
                int clamped = Mathf.Clamp(rank, 0, GetMaximumRank(player));
                player.SetRank(clamped, true);
                _rankTarget = clamped;
                ShowStatus("Rank set to " + clamped + ".");
            }
            catch (Exception ex)
            {
                ShowStatus("Rank change failed; see BepInEx log.");
                Logger.LogError(ex);
            }
        }

        private bool TryGetAuthoritativePlayer(out Player player)
        {
            if (!TryGetLocalPlayer(out player))
            {
                ShowStatus("No active local player. Enter a flight session first.");
                return false;
            }

            if (!player.IsServer)
            {
                ShowStatus("Funds/rank are server-authoritative: use single-player or host the session.");
                return false;
            }

            return true;
        }

        internal static bool TryGetLocalPlayer(out Player player)
        {
            player = null;
            try
            {
                return GameManager.GetLocalPlayer<Player>(out player) && player != null;
            }
            catch
            {
                player = null;
                return false;
            }
        }

        private static int GetMaximumRank(Player player)
        {
            if (player == null)
            {
                return 10;
            }

            try
            {
                FieldInfo field = AccessTools.Field(typeof(Player), "rankThresholds");
                float[] thresholds = field == null ? null : field.GetValue(player) as float[];
                return thresholds == null || thresholds.Length == 0 ? 10 : thresholds.Length - 1;
            }
            catch
            {
                return 10;
            }
        }

        private void ShowStatus(string message)
        {
            _status = message;
            _statusUntil = Time.unscaledTime + 5f;
            Logger.LogMessage(message);
        }

        private void SetHudHideMode(HudHideMode mode)
        {
            if (_hudHideMode == mode && (mode != HudHideMode.Visible ||
                                        (_hiddenCanvases.Count == 0 && _hiddenMarkerObjects.Count == 0)))
            {
                return;
            }

            RestoreHiddenHud();
            _hudHideMode = mode;
            HideTargetMarkers = mode == HudHideMode.TargetMarkersOnly || mode == HudHideMode.CleanScreen;
            if (mode != HudHideMode.Visible)
            {
                ApplyHudHideMode();
                _nextCanvasSweep = Time.unscaledTime + 0.35f;
                ShowStatus(mode == HudHideMode.CleanScreen
                    ? "Screen-space HUD hidden; cockpit displays preserved."
                    : "Target markers hidden; HUD and cockpit displays preserved.");
            }
            else
            {
                ShowStatus("Game UI and target markers restored.");
            }
        }

        private void RestoreHiddenHud()
        {
            foreach (CanvasRestoreState state in _hiddenCanvases.Values)
                if (state.Canvas != null) state.Canvas.enabled = state.WasEnabled;
            _hiddenCanvases.Clear();

            foreach (GameObjectRestoreState state in _hiddenMarkerObjects.Values)
                if (state.Object != null) state.Object.SetActive(state.WasActive);
            _hiddenMarkerObjects.Clear();
        }

        private void ApplyHudHideMode()
        {
            if (_hudHideMode == HudHideMode.CleanScreen)
            {
                CaptureAndHideScreenCanvases();
                CaptureAndHideTargetMarkers();
            }
            else if (_hudHideMode == HudHideMode.TargetMarkersOnly) CaptureAndHideTargetMarkers();
        }

        private void CaptureAndHideScreenCanvases()
        {
            var canvases = new List<Canvas>();
            try
            {
                FlightHud[] flightHuds = Resources.FindObjectsOfTypeAll<FlightHud>();
                for (int i = 0; i < flightHuds.Length; i++)
                {
                    Canvas canvas = flightHuds[i] == null ? null : flightHuds[i].GetComponentInParent<Canvas>(true);
                    if (canvas != null && !canvases.Contains(canvas)) canvases.Add(canvas);
                }
                CombatHUD[] combatHuds = Resources.FindObjectsOfTypeAll<CombatHUD>();
                for (int i = 0; i < combatHuds.Length; i++)
                {
                    Canvas canvas = combatHuds[i] == null ? null : combatHuds[i].GetComponentInParent<Canvas>(true);
                    if (canvas != null && !canvases.Contains(canvas)) canvases.Add(canvas);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Canvas scan failed: " + ex.Message);
                return;
            }

            for (int i = 0; i < canvases.Count; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.gameObject == null || !canvas.gameObject.scene.IsValid())
                {
                    continue;
                }

                int id = canvas.GetInstanceID();
                if (!_hiddenCanvases.ContainsKey(id))
                {
                    _hiddenCanvases.Add(id, new CanvasRestoreState
                    {
                        Canvas = canvas,
                        WasEnabled = canvas.enabled
                    });
                }

                if (canvas.enabled)
                {
                    canvas.enabled = false;
                }
            }
        }

        private void CaptureAndHideTargetMarkers()
        {
            CombatHUD[] huds;
            try { huds = Resources.FindObjectsOfTypeAll<CombatHUD>(); }
            catch (Exception ex)
            {
                Logger.LogDebug("Target-marker scan failed: " + ex.Message);
                return;
            }

            FieldInfo iconLayerField = AccessTools.Field(typeof(CombatHUD), "iconLayer");
            FieldInfo targetArrowField = AccessTools.Field(typeof(CombatHUD), "targetArrow");
            FieldInfo targetDesignatorField = AccessTools.Field(typeof(CombatHUD), "targetDesignator");
            for (int i = 0; i < huds.Length; i++)
            {
                CombatHUD hud = huds[i];
                if (hud == null) continue;
                Transform iconLayer = iconLayerField == null ? null : iconLayerField.GetValue(hud) as Transform;
                Behaviour arrow = targetArrowField == null ? null : targetArrowField.GetValue(hud) as Behaviour;
                Behaviour designator = targetDesignatorField == null ? null : targetDesignatorField.GetValue(hud) as Behaviour;
                HideMarkerObject(iconLayer == null ? null : iconLayer.gameObject);
                HideMarkerObject(arrow == null ? null : arrow.gameObject);
                HideMarkerObject(designator == null ? null : designator.gameObject);
            }
        }

        private void HideMarkerObject(GameObject go)
        {
            if (go == null || !go.activeSelf) return;
            int id = go.GetInstanceID();
            if (!_hiddenMarkerObjects.ContainsKey(id))
                _hiddenMarkerObjects.Add(id, new GameObjectRestoreState { Object = go, WasActive = true });
            go.SetActive(false);
        }

        private static bool IsMainMenuObject(GameObject go)
        {
            string scene = go.scene.IsValid() ? go.scene.name : string.Empty;
            if (scene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (Transform t = go.transform; t != null; t = t.parent)
                if (t.name.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }


        internal static void AddDamageMessage(UnitPart part, float oldHealth, float newHealth, bool detached)
        {
            if (!DamageFeed || part == null || !IsPlayerResponsibleForDamage(part))
            {
                return;
            }

            try
            {
                string vehicle = GetVehicleName(part);
                string partName = CleanObjectName(part.name);
                float damage = Mathf.Max(0f, oldHealth - newHealth);
                string message;
                Color color;

                if (detached)
                {
                    message = vehicle + " • " + partName + " DESTROYED";
                    color = new Color(1f, 0.30f, 0.22f, 1f);
                }
                else if (newHealth <= 0f && oldHealth > 0f)
                {
                    message = vehicle + " • " + partName + " DISABLED";
                    color = new Color(1f, 0.48f, 0.20f, 1f);
                }
                else
                {
                    message = vehicle + " • " + partName + " hit  -" + damage.ToString("0.0", CultureInfo.InvariantCulture);
                    color = new Color(1f, 0.86f, 0.35f, 1f);
                }

                AddOrMergeFeed(message, vehicle + "|" + partName + "|" + detached, color);
            }
            catch (Exception ex)
            {
                if (LogSource != null)
                {
                    LogSource.LogDebug("Damage-feed event ignored: " + ex.Message);
                }
            }
        }

        private static string GetVehicleName(UnitPart part)
        {
            Unit unit = GetParentUnit(part);
            if (unit == null)
            {
                return "Vehicle";
            }

            FieldInfo nameField = AccessTools.Field(typeof(Unit), "unitName");
            string unitName = nameField == null ? null : nameField.GetValue(unit) as string;
            if (string.IsNullOrEmpty(unitName))
            {
                unitName = unit.name;
            }
            return CleanObjectName(unitName);
        }

        private static Unit GetParentUnit(UnitPart part)
        {
            FieldInfo parentField = AccessTools.Field(typeof(UnitPart), "parentUnit");
            return parentField == null || part == null ? null : parentField.GetValue(part) as Unit;
        }

        private static bool IsPlayerResponsibleForDamage(UnitPart part)
        {
            Unit victim = GetParentUnit(part);
            if (victim == null) return false;
            Aircraft victimAircraft = victim as Aircraft;
            if (victimAircraft != null && GameManager.IsLocalAircraft(victimAircraft)) return false;

            // Unit records the most recent damage source in v0.34. Resolve common owner/source
            // chains so guns, missiles and their launch aircraft all identify the local player.
            object source = ReadMember(victim, "lastDamagedBy") ?? ReadMember(victim, "damagedBy");
            return ResolvesToLocalAircraft(source, 0, new HashSet<int>());
        }

        private static bool ResolvesToLocalAircraft(object source, int depth, HashSet<int> visited)
        {
            if (source == null || depth > 5) return false;
            UnityEngine.Object unityObject = source as UnityEngine.Object;
            if (unityObject != null && !visited.Add(unityObject.GetInstanceID())) return false;

            Aircraft aircraft = source as Aircraft;
            if (aircraft != null) return GameManager.IsLocalAircraft(aircraft);
            Component component = source as Component;
            if (component != null)
            {
                Aircraft parentAircraft = component.GetComponentInParent<Aircraft>();
                if (parentAircraft != null && GameManager.IsLocalAircraft(parentAircraft)) return true;
            }

            string[] links = { "owner", "attacker", "attachedUnit", "sourceUnit", "unit", "aircraft", "launcher", "firedBy" };
            for (int i = 0; i < links.Length; i++)
            {
                object next = ReadMember(source, links[i]);
                if (next != null && !ReferenceEquals(next, source) && ResolvesToLocalAircraft(next, depth + 1, visited))
                    return true;
            }
            return false;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            Type type = instance.GetType();
            try
            {
                FieldInfo field = AccessTools.Field(type, name);
                if (field != null) return field.GetValue(instance);
                PropertyInfo property = AccessTools.Property(type, name);
                return property != null && property.CanRead ? property.GetValue(instance, null) : null;
            }
            catch { return null; }
        }

        private static string CleanObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Part";
            }

            return value.Replace("(Clone)", string.Empty).Replace('_', ' ').Trim();
        }

        private static void AddOrMergeFeed(string text, string key, Color color)
        {
            lock (FeedLock)
            {
                float now = Time.unscaledTime;
                FeedEntry existing = Feed.FirstOrDefault(e => e.Key == key && now - e.LastUpdate < 0.22f);
                if (existing != null)
                {
                    existing.Text = text;
                    existing.Created = now;
                    existing.LastUpdate = now;
                    existing.Color = color;
                    return;
                }

                Feed.Insert(0, new FeedEntry
                {
                    Text = text,
                    Key = key,
                    Color = color,
                    Created = now,
                    LastUpdate = now
                });

                if (Feed.Count > MaxFeedEntries)
                {
                    Feed.RemoveRange(MaxFeedEntries, Feed.Count - MaxFeedEntries);
                }
            }
        }

        private static void RemoveExpiredFeedEntries()
        {
            lock (FeedLock)
            {
                float now = Time.unscaledTime;
                Feed.RemoveAll(e => now - e.Created > FeedLifetime);
            }
        }

        private void DrawDamageFeed()
        {
            FeedEntry[] entries;
            lock (FeedLock)
            {
                entries = Feed.ToArray();
            }

            float width = Mathf.Min(520f, Screen.width * 0.42f);
            float x = Screen.width - width - 24f;
            float y = 28f;
            float now = Time.unscaledTime;

            for (int i = 0; i < entries.Length; i++)
            {
                FeedEntry entry = entries[i];
                float age = now - entry.Created;
                float alpha = (age < FeedLifetime - 1.25f ? 1f : Mathf.Clamp01((FeedLifetime - age) / 1.25f)) * 0.62f;
                Color color = entry.Color;
                color.a *= alpha;
                Color shadow = new Color(0f, 0f, 0f, 0.45f * alpha);

                _feedShadowStyle.normal.textColor = shadow;
                GUI.Label(new Rect(x + 1f, y + 1f, width, 24f), entry.Text, _feedShadowStyle);
                _feedStyle.normal.textColor = color;
                GUI.Label(new Rect(x, y, width, 24f), entry.Text, _feedStyle);
                y += 22f;
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(10, 10, 24, 10),
                fontSize = 13
            };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.82f, 1f, 1f) }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.85f, 0.88f, 1f) }
            };
            _feedStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            _feedShadowStyle = new GUIStyle(_feedStyle);
        }

        private sealed class FeedEntry
        {
            public string Text;
            public string Key;
            public Color Color;
            public float Created;
            public float LastUpdate;
        }

        private sealed class CanvasRestoreState
        {
            public Canvas Canvas;
            public bool WasEnabled;
        }

        private sealed class GameObjectRestoreState
        {
            public GameObject Object;
            public bool WasActive;
        }

        private enum HudHideMode
        {
            Visible,
            CleanScreen,
            TargetMarkersOnly
        }
    }

    [HarmonyPatch(typeof(HUDUnitMarker), "UpdateVisibility")]
    internal static class HUDUnitMarkerVisibilityPatch
    {
        private static readonly FieldInfo ImageField = AccessTools.Field(typeof(HUDUnitMarker), "image");

        private static void Postfix(HUDUnitMarker __instance)
        {
            if (!NuclearOptionTrainer.HideTargetMarkers || __instance == null || ImageField == null) return;
            Behaviour image = ImageField.GetValue(__instance) as Behaviour;
            if (image != null) image.enabled = false;
        }
    }

    [HarmonyPatch(typeof(TargetMarker), "Show")]
    internal static class TargetMarkerShowPatch
    {
        private static void Prefix(ref bool value)
        {
            if (NuclearOptionTrainer.HideTargetMarkers) value = false;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), "LateUpdate")]
    internal static class CombatHudMarkerLayerPatch
    {
        private static readonly FieldInfo IconLayerField = AccessTools.Field(typeof(CombatHUD), "iconLayer");
        private static readonly FieldInfo TargetArrowField = AccessTools.Field(typeof(CombatHUD), "targetArrow");
        private static readonly FieldInfo TargetDesignatorField = AccessTools.Field(typeof(CombatHUD), "targetDesignator");

        private static void Postfix(CombatHUD __instance)
        {
            if (!NuclearOptionTrainer.HideTargetMarkers || __instance == null) return;
            SetInactive(IconLayerField == null ? null : IconLayerField.GetValue(__instance) as Component);
            SetInactive(TargetArrowField == null ? null : TargetArrowField.GetValue(__instance) as Component);
            SetInactive(TargetDesignatorField == null ? null : TargetDesignatorField.GetValue(__instance) as Component);
        }

        private static void SetInactive(Component component)
        {
            if (component != null && component.gameObject.activeSelf) component.gameObject.SetActive(false);
        }
    }

    internal struct PartDamageState
    {
        public float Health;
        public bool Detached;
    }

    [HarmonyPatch(typeof(UnitPart), "ApplyDamage")]
    internal static class UnitPartApplyDamagePatch
    {
        private static readonly FieldInfo HitPointsField = AccessTools.Field(typeof(UnitPart), "hitPoints");
        private static readonly FieldInfo DetachedField = AccessTools.Field(typeof(UnitPart), "detachedFromUnit");

        private static void Prefix(UnitPart __instance, out PartDamageState __state)
        {
            __state = new PartDamageState
            {
                Health = ReadFloat(HitPointsField, __instance),
                Detached = ReadBool(DetachedField, __instance)
            };
        }

        private static void Postfix(UnitPart __instance, PartDamageState __state)
        {
            float health = ReadFloat(HitPointsField, __instance);
            bool detached = ReadBool(DetachedField, __instance);
            if (health < __state.Health - 0.001f)
            {
                NuclearOptionTrainer.AddDamageMessage(__instance, __state.Health, health,
                    detached && !__state.Detached);
            }
        }

        internal static float ReadFloat(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
            {
                return 0f;
            }
            object value = field.GetValue(instance);
            return value is float ? (float)value : 0f;
        }

        internal static bool ReadBool(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
            {
                return false;
            }
            object value = field.GetValue(instance);
            return value is bool && (bool)value;
        }
    }

    [HarmonyPatch(typeof(UnitPart), "Detach")]
    internal static class UnitPartDetachPatch
    {
        private static readonly FieldInfo HitPointsField = AccessTools.Field(typeof(UnitPart), "hitPoints");
        private static readonly FieldInfo DetachedField = AccessTools.Field(typeof(UnitPart), "detachedFromUnit");

        private static void Prefix(UnitPart __instance, out PartDamageState __state)
        {
            __state = new PartDamageState
            {
                Health = UnitPartApplyDamagePatch.ReadFloat(HitPointsField, __instance),
                Detached = UnitPartApplyDamagePatch.ReadBool(DetachedField, __instance)
            };
        }

        private static void Postfix(UnitPart __instance, PartDamageState __state)
        {
            bool detached = UnitPartApplyDamagePatch.ReadBool(DetachedField, __instance);
            if (!__state.Detached && detached)
            {
                NuclearOptionTrainer.AddDamageMessage(__instance, __state.Health, __state.Health, true);
            }
        }
    }

    [HarmonyPatch]
    internal static class UnlimitedAmmoWeaponPatch
    {
        private static readonly FieldInfo AmmoField = AccessTools.Field(typeof(Weapon), "ammo");
        private static readonly FieldInfo AttachedUnitField = AccessTools.Field(typeof(Weapon), "attachedUnit");
        private static readonly FieldInfo StationField = AccessTools.Field(typeof(Weapon), "weaponStation");

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Assembly assembly = typeof(Weapon).Assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (Type type in types)
            {
                if (type.IsAbstract || !typeof(Weapon).IsAssignableFrom(type))
                {
                    continue;
                }

                MethodInfo[] methods;
                try
                {
                    methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.DeclaredOnly);
                }
                catch
                {
                    continue;
                }

                foreach (MethodInfo method in methods)
                {
                    if (method.Name == "Fire" || method.Name == "RemoteSingleFire")
                    {
                        yield return method;
                    }
                }
            }
        }

        private static void Prefix(object __instance, out int __state)
        {
            __state = -1;
            Weapon weapon = __instance as Weapon;
            if (!NuclearOptionTrainer.UnlimitedAmmo || !IsLocalWeapon(weapon) || AmmoField == null)
            {
                return;
            }

            object value = AmmoField.GetValue(weapon);
            if (value is int)
            {
                __state = (int)value;
            }
        }

        private static void Postfix(object __instance, int __state)
        {
            if (__state < 0 || !NuclearOptionTrainer.UnlimitedAmmo || AmmoField == null)
            {
                return;
            }

            Weapon weapon = __instance as Weapon;
            if (weapon == null)
            {
                return;
            }

            AmmoField.SetValue(weapon, __state);
            WeaponStation station = StationField == null ? null : StationField.GetValue(weapon) as WeaponStation;
            if (station != null)
            {
                station.AccountAmmo();
            }
        }

        internal static bool IsLocalWeapon(Weapon weapon)
        {
            if (weapon == null || AttachedUnitField == null)
            {
                return false;
            }

            Aircraft aircraft = AttachedUnitField.GetValue(weapon) as Aircraft;
            return aircraft != null && GameManager.IsLocalAircraft(aircraft);
        }
    }

    [HarmonyPatch(typeof(Aircraft), "UseFuel")]
    internal static class UnlimitedFuelPatch
    {
        private static bool Prefix(Aircraft __instance, ref bool __result)
        {
            if (!NuclearOptionTrainer.UnlimitedFuel || __instance == null || !GameManager.IsLocalAircraft(__instance))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    internal static class CockpitCameraPatch
    {
        private static void Postfix(CameraCockpitState __instance, CameraStateManager cam)
        {
            if (!NuclearOptionTrainer.CockpitPhysics)
            {
                CockpitHeadMotion.Reset();
                return;
            }

            CockpitHeadMotion.Apply(__instance, cam);
        }
    }

    internal static class CockpitHeadMotion
    {
        private static readonly FieldInfo AircraftField = AccessTools.Field(typeof(CameraCockpitState), "aircraft");

        private enum AirframeMotionClass
        {
            Propeller,
            Rotorcraft,
            Jet,
            Supersonic
        }

        private static int _aircraftId;
        private static Vector3 _previousVelocity;
        private static Vector3 _previousAcceleration;
        private static Vector3 _positionOffset;
        private static Vector3 _positionVelocity;
        private static Vector3 _rotationOffset;
        private static Vector3 _rotationVelocity;
        private static float _noiseTime;
        private static float _previousMach;
        private static float _sonicKick;
        private static float _lastSonicCrossing = -100f;
        private static AirframeMotionClass _motionClass;
        private static IEngine[] _engines = new IEngine[0];
        private static bool _initialized;

        internal static void Apply(CameraCockpitState state, CameraStateManager cam)
        {
            if (state == null || cam == null || AircraftField == null || NuclearOptionTrainer.Instance == null)
            {
                Reset();
                return;
            }

            Aircraft aircraft = AircraftField.GetValue(state) as Aircraft;
            if (aircraft == null || !aircraft.isActiveAndEnabled || !GameManager.IsLocalAircraft(aircraft))
            {
                Reset();
                return;
            }

            Rigidbody body = aircraft.rb;
            Transform cameraTransform = cam.transform;
            if (body == null || cameraTransform == null)
            {
                Reset();
                return;
            }

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            Transform aircraftTransform = aircraft.transform;
            int id = aircraft.GetInstanceID();
            if (!_initialized || _aircraftId != id)
            {
                _aircraftId = id;
                _previousVelocity = body.velocity;
                _previousAcceleration = aircraftTransform.InverseTransformDirection(-Physics.gravity) / 9.80665f;
                _positionOffset = Vector3.zero;
                _positionVelocity = Vector3.zero;
                _rotationOffset = Vector3.zero;
                _rotationVelocity = Vector3.zero;
                _noiseTime = 0f;
                _motionClass = ClassifyAircraft(aircraft);
                _engines = FindEngines(aircraft);
                _previousMach = GetMach(aircraft);
                _sonicKick = 0f;
                _initialized = true;
                return;
            }

            Vector3 worldAcceleration = (body.velocity - _previousVelocity) / dt;
            Vector3 specificAcceleration = worldAcceleration - Physics.gravity;
            Vector3 localG = aircraftTransform.InverseTransformDirection(specificAcceleration) / 9.80665f;
            Vector3 dynamicG = localG - Vector3.up;
            Vector3 localAngularVelocity = aircraftTransform.InverseTransformDirection(body.angularVelocity);
            Vector3 localJerk = (localG - _previousAcceleration) / dt;

            _previousVelocity = body.velocity;
            _previousAcceleration = localG;

            MotionProfile profile = GetMotionProfile(_motionClass);
            float highG = Mathf.Clamp01((Mathf.Abs(localG.y) - 1.8f) / 4.7f);
            float maneuver = Mathf.Clamp01(dynamicG.magnitude / 2.25f);
            float impulse = Mathf.Clamp01(localJerk.magnitude / 85f);
            float positionStrength = Mathf.Clamp(NuclearOptionTrainer.Instance.PositionStrength.Value, 0f, 3f) *
                                     profile.Position * 0.45f;
            float rotationStrength = Mathf.Clamp(NuclearOptionTrainer.Instance.RotationStrength.Value, 0f, 3f) *
                                     profile.Rotation * 0.45f;
            float shakeStrength = Mathf.Clamp(NuclearOptionTrainer.Instance.ShakeStrength.Value, 0f, 3f);

            float mach = GetMach(aircraft);
            if (_previousMach < 0.98f && mach >= 1.0f && Time.unscaledTime - _lastSonicCrossing > 3f)
            {
                _sonicKick = Mathf.Clamp(NuclearOptionTrainer.Instance.SonicBoomShakeStrength.Value, 0f, 3f);
                _lastSonicCrossing = Time.unscaledTime;
                cam.ShakeCamera(0.22f * _sonicKick, 0.16f * _sonicKick);
            }
            _previousMach = mach;
            _sonicKick = Mathf.MoveTowards(_sonicKick, 0f, dt * 2.8f);

            // Local aircraft axes: x right, y up, z forward. The head lags opposite acceleration.
            Vector3 targetPosition = new Vector3(-dynamicG.x * 0.010f, -dynamicG.y * 0.0075f,
                -dynamicG.z * 0.0065f) * positionStrength;
            targetPosition += new Vector3(-localAngularVelocity.y, localAngularVelocity.z,
                localAngularVelocity.x) * (0.005f * positionStrength);
            targetPosition.z -= _sonicKick * 0.018f;

            Vector3 targetRotation = new Vector3(dynamicG.z * 0.85f - localAngularVelocity.x * 1.5f,
                -dynamicG.x * 0.55f - localAngularVelocity.y * 1.1f,
                dynamicG.x * 1.15f - localAngularVelocity.z * 1.4f) * rotationStrength;
            targetRotation.x += _sonicKick * 1.8f;

            float smoothTime = Mathf.Lerp(0.15f, 0.075f, Mathf.Max(highG, impulse));
            _positionOffset = Vector3.SmoothDamp(_positionOffset, targetPosition, ref _positionVelocity,
                smoothTime, 2f, dt);
            _rotationOffset = Vector3.SmoothDamp(_rotationOffset, targetRotation, ref _rotationVelocity,
                smoothTime * 0.85f, 90f, dt);

            float engineActivity = body.velocity.magnitude > 25f ? GetEngineActivity() : 0f;
            float engineVibration = engineActivity * profile.EngineVibration;
            _noiseTime += dt * Mathf.Lerp(profile.NoiseFrequency, profile.NoiseFrequency * 1.65f,
                Mathf.Max(highG, impulse));
            // Deliberately no constant base noise: stable flight and parked aircraft stay still.
            float shakeAmplitude = (maneuver * 0.00028f + highG * 0.00105f + impulse * 0.00135f +
                                    engineVibration + _sonicKick * 0.0032f) * shakeStrength * profile.Shake;
            Vector3 positionNoise = new Vector3(
                SignedNoise(_noiseTime, 0.0f),
                SignedNoise(_noiseTime * 1.13f, 11.7f),
                SignedNoise(_noiseTime * 0.91f, 24.2f)) * shakeAmplitude;
            Vector3 rotationNoise = new Vector3(
                SignedNoise(_noiseTime * 1.21f, 37.1f),
                SignedNoise(_noiseTime * 0.96f, 50.4f),
                SignedNoise(_noiseTime * 1.08f, 63.8f)) * (shakeAmplitude * 72f);

            float maxPosition = Mathf.Clamp(NuclearOptionTrainer.Instance.MaxPositionOffset.Value, 0.01f, 0.3f);
            float maxRotation = Mathf.Clamp(NuclearOptionTrainer.Instance.MaxRotationOffset.Value, 1f, 20f);
            Vector3 finalPosition = Vector3.ClampMagnitude(_positionOffset + positionNoise, maxPosition);
            Vector3 finalRotation = ClampComponents(_rotationOffset + rotationNoise, maxRotation);

            // Applied after the game's own cockpit pose, preserving free-look, padlock and aiming.
            cameraTransform.localPosition += finalPosition;
            cameraTransform.localRotation *= Quaternion.Euler(finalRotation);
        }

        private static float SignedNoise(float x, float seed)
        {
            return Mathf.PerlinNoise(x, seed) * 2f - 1f;
        }

        private static AirframeMotionClass ClassifyAircraft(Aircraft aircraft)
        {
            if (aircraft.GetComponentsInChildren<RotorShaft>(true).Length > 0)
            {
                return AirframeMotionClass.Rotorcraft;
            }
            if (aircraft.GetComponentsInChildren<ConstantSpeedProp>(true).Length > 0 ||
                aircraft.GetComponentsInChildren<PropFan>(true).Length > 0)
            {
                return AirframeMotionClass.Propeller;
            }

            AircraftDefinition definition = aircraft.definition;
            float designedMaxSpeed = definition != null && definition.aircraftParameters != null
                ? definition.aircraftParameters.maxSpeed
                : 0f;
            return designedMaxSpeed > 360f ? AirframeMotionClass.Supersonic : AirframeMotionClass.Jet;
        }

        private static IEngine[] FindEngines(Aircraft aircraft)
        {
            try
            {
                return aircraft.GetComponentsInChildren<MonoBehaviour>(true)
                    .OfType<IEngine>()
                    .ToArray();
            }
            catch
            {
                return new IEngine[0];
            }
        }

        private static float GetEngineActivity()
        {
            if (_engines == null || _engines.Length == 0)
            {
                return 0f;
            }

            float total = 0f;
            int valid = 0;
            for (int i = 0; i < _engines.Length; i++)
            {
                try
                {
                    if (_engines[i] == null) continue;
                    total += Mathf.Clamp01(_engines[i].GetRPMRatio());
                    valid++;
                }
                catch
                {
                    // A damaged/detached engine can disappear between frames.
                }
            }
            return valid == 0 ? 0f : total / valid;
        }

        private static float GetMach(Aircraft aircraft)
        {
            float speedOfSound = LevelInfo.GetSpeedOfSound(aircraft.transform.position.y);
            return speedOfSound > 1f ? aircraft.rb.velocity.magnitude / speedOfSound : 0f;
        }

        private static MotionProfile GetMotionProfile(AirframeMotionClass motionClass)
        {
            switch (motionClass)
            {
                case AirframeMotionClass.Propeller:
                    return new MotionProfile(0.62f, 0.68f, 0.65f, 21f, 0.00016f);
                case AirframeMotionClass.Rotorcraft:
                    return new MotionProfile(0.72f, 0.82f, 0.78f, 12f, 0.00028f);
                case AirframeMotionClass.Supersonic:
                    return new MotionProfile(0.92f, 0.82f, 0.82f, 17f, 0.000035f);
                default:
                    return new MotionProfile(0.78f, 0.72f, 0.68f, 15f, 0.000025f);
            }
        }

        private struct MotionProfile
        {
            public readonly float Position;
            public readonly float Rotation;
            public readonly float Shake;
            public readonly float NoiseFrequency;
            public readonly float EngineVibration;

            public MotionProfile(float position, float rotation, float shake, float noiseFrequency,
                float engineVibration)
            {
                Position = position;
                Rotation = rotation;
                Shake = shake;
                NoiseFrequency = noiseFrequency;
                EngineVibration = engineVibration;
            }
        }

        private static Vector3 ClampComponents(Vector3 value, float limit)
        {
            value.x = Mathf.Clamp(value.x, -limit, limit);
            value.y = Mathf.Clamp(value.y, -limit, limit);
            value.z = Mathf.Clamp(value.z, -limit, limit);
            return value;
        }

        internal static void Reset()
        {
            _aircraftId = 0;
            _previousVelocity = Vector3.zero;
            _previousAcceleration = Vector3.zero;
            _positionOffset = Vector3.zero;
            _positionVelocity = Vector3.zero;
            _rotationOffset = Vector3.zero;
            _rotationVelocity = Vector3.zero;
            _noiseTime = 0f;
            _previousMach = 0f;
            _sonicKick = 0f;
            _engines = new IEngine[0];
            _initialized = false;
        }
    }

    [HarmonyPatch(typeof(AudioSource), "PlayOneShot", new[] { typeof(AudioClip), typeof(float) })]
    internal static class SonicBoomVolumePatch
    {
        private static void Prefix(AudioClip clip, ref float volumeScale)
        {
            if (clip == null || NuclearOptionTrainer.Instance == null)
            {
                return;
            }

            GameAssets assets = GameAssets.i;
            if (assets != null && clip == assets.sonicBoom)
            {
                volumeScale *= Mathf.Clamp(
                    NuclearOptionTrainer.Instance.SonicBoomVolumeMultiplier.Value, 0.25f, 4f);
            }
        }
    }
}
