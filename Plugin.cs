using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using MWL_Ports.Managers;
using ServerSync;
using UnityEngine;

namespace MWL_Ports
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class MWL_PortsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "MWL_Ports";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods_Warp";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource MWL_PortsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static MWL_PortsPlugin instance = null!;
        public static GameObject root = null!;
        public void Awake()
        {
            instance = this;
            
            // create a root object to contain all clones, necessary to hold reference to -int game objects
            root = new GameObject("root");
            DontDestroyOnLoad(root);
            root.SetActive(false);
            
            PortNames.Setup();
            Commands.Setup();
            
            // make shipment manager a monobehavior to keep functions within it's scope while taking advantages of monobehaviors
            gameObject.AddComponent<ShipmentManager>();

            PortUI.posConfig = config("2 - Settings", "Pos", new Vector3(1760f, 850f, 0f), "Set Pos");
            
            BlueprintLocation location = new BlueprintLocation("portbundle", "MWL_Port_Location");
            location.OnCreated += blueprint =>
            {
                if (blueprint.Location == null) return;
                blueprint.Location.Setup();
                blueprint.Location.Biome = Heightmap.Biome.All;
                blueprint.Location.Placement.Altitude.Min = 10f;
                blueprint.Location.Placement.ClearArea = true;
                blueprint.Location.Placement.Quantity = 200;
                blueprint.Location.Placement.Prioritized = true;
                blueprint.Location.Group.Name = "MWL_Ports";
                blueprint.Location.Placement.DistanceFromSimilar.Min = 300f;
                blueprint.Location.Icon.Enabled = true;
                blueprint.Location.Icon.InGameIcon = LocationManager.IconSettings.LocationIcon.Hildir;
            };
            
            Blueprint port = new Blueprint("portbundle", "MWL_Port");
            port.Prefab.AddComponent<Port>();
            port.OnCreated += blueprint =>
            {
                foreach (Transform child in blueprint.Prefab.transform)
                {
                    if (child.gameObject.HasComponent<TerrainModifier>()) continue;
                    child.gameObject.RemoveAllComponents<MonoBehaviour>(typeof(SnapToGround));
                }
                
                PrefabManager.RegisterPrefab(blueprint.Prefab);
            };
            
            Clone piece_chest_wood = new Clone("piece_chest_wood", "port_chest_wood");
            piece_chest_wood.OnCreated += prefab =>
            {
                prefab.RemoveComponent<Piece>();
                prefab.RemoveComponent<WearNTear>();
                prefab.GetComponent<ZNetView>().m_persistent = false;
            };

            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                MWL_PortsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                MWL_PortsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                MWL_PortsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }
    }
}