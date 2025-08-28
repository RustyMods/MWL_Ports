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
            ShipmentManager.PrefabsToSearch.Add("MWL_Port", "MWL_PortTrader"); // add port variants to search for
            // this is used by server to iterate through ZDOs and send them to players
            // this is how portals work

            PortUI.PanelPositionConfig = config("3 - UI", "Panel Position", new Vector3(1760f, 850f, 0f), "Set position of UI");
            ShipmentManager.TransitDurationConfig = config("2 - Settings", "Time Per Meter", 2f, "Set seconds per meter for shipment transit");
            ShipmentManager.CurrencyConfig = config("2 - Settings", "Shipment Currency", "Coins", "Set item prefab to use as currency to ship items");
            ShipmentManager.CurrencyConfig.SettingChanged += (_, _) => ShipmentManager._currencyItem = null;
            // this gets created after blueprints
            // it will iterate through children to find prefabs
            // and replace them
            BlueprintLocation location = new BlueprintLocation("portbundle", "MWL_Port_Location");
            location.OnCreated += blueprint =>
            {
                if (blueprint.Location == null) return;
                blueprint.Location.Setup();
                blueprint.Location.Biome = Heightmap.Biome.All;
                blueprint.Location.Placement.Altitude.Min = 10f;
                blueprint.Location.Placement.ClearArea = true;
                blueprint.Location.Placement.Quantity = 100;
                blueprint.Location.Placement.Prioritized = true;
                blueprint.Location.Group.Name = "MWL_Ports";
                blueprint.Location.Placement.DistanceFromSimilar.Min = 300f;
                blueprint.Location.Icon.Enabled = false;
                // blueprint.Location.Icon.Icon = MySprite ----> if you want to use a custom sprite
                // blueprint.Location.Icon.InGameIcon = LocationManager.IconSettings.LocationIcon.Hildir;
            };
            
            // this gets created before blueprint location
            // since this prefab has a ZNetView, we make sure to create it as its own prefab first
            // then the location uses it when creating itself
            Blueprint port = new Blueprint("portbundle", "MWL_Port");
            port.Prefab.AddComponent<Port>();
            port.OnCreated += blueprint =>
            {
                foreach (Transform child in blueprint.Prefab.transform)
                {
                    if (child.gameObject.HasComponent<TerrainModifier>()) continue;
                    child.gameObject.RemoveAllComponents<MonoBehaviour>(false, typeof(SnapToGround));
                }
                
                PrefabManager.RegisterPrefab(blueprint.Prefab);
            };
            
            BlueprintLocation large = new BlueprintLocation("portbundle", "MWL_Port_Location_Large");
            large.OnCreated += blueprint =>
            {
                if (blueprint.Location == null) return;
                blueprint.Location.Setup();
                blueprint.Location.Biome = Heightmap.Biome.All;
                blueprint.Location.Placement.Altitude.Min = 0f;
                blueprint.Location.Placement.Altitude.Max = 60f;
                blueprint.Location.Placement.ClearArea = true;
                blueprint.Location.Placement.Quantity = 100;
                blueprint.Location.Placement.Prioritized = true;
                blueprint.Location.Group.Name = "MWL_Ports";
                blueprint.Location.Placement.DistanceFromSimilar.Min = 300f;
                blueprint.Location.Icon.Enabled = false;
                // blueprint.Location.Icon.Icon = MySprite ----> if you want to use a custom sprite
                // blueprint.Location.Icon.InGameIcon = LocationManager.IconSettings.LocationIcon.Boss;
            };
            
            Blueprint portTrader = new Blueprint("portbundle", "MWL_PortTrader");
            portTrader.Prefab.AddComponent<Port>();
            portTrader.OnCreated += blueprint =>
            {
                foreach (Transform child in blueprint.Prefab.transform)
                {
                    if (child.TryGetComponent(out Trader component))
                    {
                        Debug.LogWarning("Adding port trader component on: " + child.name);
                        child.gameObject.name = "PortTrader";
                        var trader = child.gameObject.AddComponent<PortTrader>();
                        trader.m_standRange = component.m_standRange;
                        trader.m_greetRange = component.m_greetRange;
                        trader.m_byeRange = component.m_byeRange;
                        trader.m_hideDialogDelay = component.m_hideDialogDelay;
                        trader.m_randomTalkInterval = component.m_randomTalkInterval;
                        trader.m_dialogHeight = component.m_dialogHeight;
                        trader.m_randomTalkFX = component.m_randomTalkFX;
                        trader.m_randomGreetFX =  component.m_randomGreetFX;
                        trader.m_randomGoodbyeFX =  component.m_randomGoodbyeFX;
                    }
                    child.gameObject.RemoveAllComponents<MonoBehaviour>(false, typeof(PortTrader));
                }

                
                PrefabManager.RegisterPrefab(blueprint.Prefab);
            };
            
            PortTrader.m_randomGreets.Add("Hello weary traveller!", "Good day for a shipment!", "Well look who wandered in");
            PortTrader.m_randomGoodbye.Add("Till next time!", "You're costing me a fortune!", "Safe travels!");
            PortTrader.m_randomTalk.Add("A Human!", "Where did you come from ?", "What are you ?");
            
            // simple class to clone in-game assets
            Clone piece_chest_wood = new Clone("piece_chest_wood", "port_chest_wood");
            piece_chest_wood.OnCreated += prefab =>
            {
                prefab.RemoveComponent<Piece>();
                prefab.RemoveComponent<WearNTear>();
                prefab.GetComponent<ZNetView>().m_persistent = false;

                Manifest manifest = new Manifest("Wooden Shipment", prefab);
                manifest.Requirements.Add("Wood", 10);
                manifest.Requirements.Add("Coins", 5);
                manifest.Requirements.Add("Resin", 5);
                manifest.Requirements.Add("SurtlingCore", 1);
                
                Manifest manifest1 = new Manifest("Wooden Shipment 2", prefab);
                manifest1.Requirements.Add("Wood", 10);
                manifest1.Requirements.Add("Coins", 5);
                manifest1.Requirements.Add("Resin", 5);
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