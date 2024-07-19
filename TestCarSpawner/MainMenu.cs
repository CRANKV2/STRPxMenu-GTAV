using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.Elements;
using LemonUI.Menus;
using System.IO;

namespace STRPVAddonSpawner
{
    public class STRPVAddonSpawner : Script
    {
        private ObjectPool menuPool;
        private NativeMenu mainMenu;
        private NativeMenu vehicleSubMenu;
        private NativeMenu settingsMenu;
        private NativeMenu spawnedVehiclesMenu;
        private Dictionary<string, string> addOnVehicleNamesToModels = new Dictionary<string, string>();
        private bool spawnInsideVehicle;
        private bool preventVehicleDespawn;
        private bool addBlipToSpawnedCar;
        private System.Windows.Forms.Keys menuOpenKey;
        private int maxVehicles = 1; // Default to 1 vehicle
        private List<Vehicle> spawnedVehicles = new List<Vehicle>();

        public STRPVAddonSpawner()
        {
            ClearLogFile();
            Log("Constructor started");

            ReadSettingsFromIniFile();

            menuPool = new ObjectPool();

            CreateMainMenu();
            CreateVehicleSubMenu();
            CreateSettingsMenu();
            CreateSpawnedVehiclesMenu();
            Log("Menus created");

            menuPool.Add(mainMenu);
            menuPool.Add(vehicleSubMenu);
            menuPool.Add(settingsMenu);
            menuPool.Add(spawnedVehiclesMenu);

            // Load the texture dictionary
            string textureDictionary = "vcompanionmenu";
            string textureName = "STRPVAddonSpawner"; // Ensure this matches the texture name inside the YTD file

            // Apply the banner to all menus in the pool
            menuPool.ForEach<NativeMenu>(x => x.Banner = new ScaledTexture(PointF.Empty, new SizeF(512, 128), textureDictionary, textureName));

            Tick += OnTick;
            KeyDown += OnKeyDown;

            Log("Constructor finished");
        }

        private void ClearLogFile()
        {
            string logFilePath = "./scripts/STRPVAddon/AddonCars.log";

            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }
        }

        private void Log(string message)
        {
            string logFilePath = "./scripts/STRPVAddon/AddonCars.log";

            // Append the log file if it exists
            using (StreamWriter writer = new StreamWriter(logFilePath, true)) // 'true' to append
            {
                writer.WriteLine($"{DateTime.Now}: {message}");
            }
        }

        private void ReadSettingsFromIniFile()
        {
            string[] lines = File.ReadAllLines("./scripts/STRPVAddon/AddonCars.ini");

            foreach (string line in lines)
            {
                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (key == "SpawnInsideVehicle")
                    {
                        bool.TryParse(value, out spawnInsideVehicle);
                    }
                    else if (key == "PreventVehicleDespawn")
                    {
                        bool.TryParse(value, out preventVehicleDespawn);
                    }
                    else if (key == "AddBlipToSpawnedCar")
                    {
                        bool.TryParse(value, out addBlipToSpawnedCar);
                    }
                    else if (key == "MenuOpenKey")
                    {
                        if (Enum.TryParse(value, out System.Windows.Forms.Keys parsedKey))
                        {
                            menuOpenKey = parsedKey;
                        }
                        else
                        {
                            menuOpenKey = System.Windows.Forms.Keys.F5; // Default to F5 if parsing fails
                        }
                    }
                    else if (key == "MaxVehicles")
                    {
                        if (value.ToLower() == "unlimited")
                        {
                            maxVehicles = -1; // Use -1 to represent unlimited
                        }
                        else
                        {
                            int.TryParse(value, out maxVehicles);
                        }
                    }
                }
            }
        }

        private void CreateMainMenu()
        {
            mainMenu = new NativeMenu("", "Select an option");

            NativeItem spawnVehiclesItem = new NativeItem("Spawn Addon Vehicles");
            NativeItem settingsItem = new NativeItem("Settings");
            NativeItem spawnedVehiclesItem = new NativeItem("Spawned Vehicles");

            mainMenu.Add(spawnVehiclesItem);
            mainMenu.Add(settingsItem);
            mainMenu.Add(spawnedVehiclesItem);

            spawnVehiclesItem.Activated += (sender, args) =>
            {
                vehicleSubMenu.Visible = true;
                mainMenu.Visible = false;
            };

            settingsItem.Activated += (sender, args) =>
            {
                settingsMenu.Visible = true;
                mainMenu.Visible = false;
            };

            spawnedVehiclesItem.Activated += (sender, args) =>
            {
                spawnedVehiclesMenu.Visible = true;
                mainMenu.Visible = false;
            };

            Log("Main menu created");
        }

        private void CreateVehicleSubMenu()
        {
            vehicleSubMenu = new NativeMenu("", "Select a vehicle to spawn");

            string[] addOnVehicleLines = File.ReadAllLines("./scripts/STRPVAddon/AddOnCars.txt");
            foreach (var line in addOnVehicleLines)
            {
                if (line.StartsWith("#") || line.StartsWith("["))
                {
                    continue;
                }

                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string modelName = parts[0].Trim();
                    string realName = parts[1].Trim();
                    NativeItem menuItem = new NativeItem(realName);
                    vehicleSubMenu.Add(menuItem);
                    addOnVehicleNamesToModels[realName] = modelName;

                    menuItem.Activated += (sender, args) => OnAddOnMenuItemSelect(sender, menuItem);
                }
            }

            Log("Vehicle sub-menu created with items: " + string.Join(", ", addOnVehicleNamesToModels.Keys));
        }

        private void CreateSettingsMenu()
        {
            try
            {
                settingsMenu = new NativeMenu("", "Configure your preferences");

                var spawnInsideVehicleItem = new NativeCheckboxItem("Spawn Inside Vehicle", spawnInsideVehicle)
                {
                    Description = "When checked, you will spawn inside the vehicle."
                };
                var preventVehicleDespawnItem = new NativeCheckboxItem("Prevent Vehicle Despawn", preventVehicleDespawn)
                {
                    Description = "Prevents the vehicle from despawning after you leave it no matter where you go."
                };
                var addBlipToSpawnedCarItem = new NativeCheckboxItem("Add Blip to Spawned Car", addBlipToSpawnedCar)
                {
                    Description = "Adds a map blip for the spawned vehicle with its Real name on the Pause Map."
                };

                var maxVehiclesItem = new NativeListItem<string>("Max Vehicles", "1", "2", "3", "4", "5", "Unlimited")
                {
                    Description = "Set the maximum number of vehicles you can spawn. (Removes oldest one if not Unlimited)"
                };

                settingsMenu.Add(spawnInsideVehicleItem);
                settingsMenu.Add(preventVehicleDespawnItem);
                settingsMenu.Add(addBlipToSpawnedCarItem);
                settingsMenu.Add(maxVehiclesItem);

                // Log the options being added
                Log($"Adding Max Vehicles Options: {string.Join(", ", maxVehiclesItem.Items)}");

                spawnInsideVehicleItem.CheckboxChanged += (sender, args) =>
                {
                    spawnInsideVehicle = spawnInsideVehicleItem.Checked;
                    WriteSettingsToIniFile();
                };

                preventVehicleDespawnItem.CheckboxChanged += (sender, args) =>
                {
                    preventVehicleDespawn = preventVehicleDespawnItem.Checked;
                    WriteSettingsToIniFile();
                };

                addBlipToSpawnedCarItem.CheckboxChanged += (sender, args) =>
                {
                    addBlipToSpawnedCar = addBlipToSpawnedCarItem.Checked;
                    WriteSettingsToIniFile();
                };

                maxVehiclesItem.ItemChanged += (sender, args) =>
                {
                    string selectedValue = maxVehiclesItem.SelectedItem;
                    maxVehicles = selectedValue == "Unlimited" ? -1 : int.Parse(selectedValue);
                    WriteSettingsToIniFile();
                };

                // Set the initial value for the max vehicles item
                string initialValue = maxVehicles == -1 ? "Unlimited" : maxVehicles.ToString();
                if (!maxVehiclesItem.Items.Contains("1"))
                {
                    maxVehiclesItem.Items.Add("1"); // Ensure "1" is always available
                }

                if (maxVehiclesItem.Items.Contains(initialValue))
                {
                    maxVehiclesItem.SelectedItem = initialValue;
                }
                else
                {
                    maxVehiclesItem.SelectedItem = "1"; // Default to "1"
                    Log($"Initial value '{initialValue}' not found. Defaulting to '1'.");
                }

                Log("Settings menu created successfully.");
            }
            catch (Exception ex)
            {
                Log($"Error in CreateSettingsMenu: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void CreateSpawnedVehiclesMenu()
        {
            spawnedVehiclesMenu = new NativeMenu("", "Spawned Vehicles");

            Log("Spawned vehicles menu created");
        }

        private void OnTick(object sender, EventArgs e)
        {
            menuPool.Process();
        }

        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == menuOpenKey)
            {
                mainMenu.Visible = !mainMenu.Visible;
                Log($"Menu visibility toggled: {mainMenu.Visible}");
            }
        }

        private void OnAddOnMenuItemSelect(object sender, NativeItem selectedItem)
        {
            string vehicleName = selectedItem.Title;
            string modelName = addOnVehicleNamesToModels[vehicleName];

            Model model = new Model(modelName);
            if (!model.IsInCdImage || !model.IsVehicle)
            {
                Log($"Model {modelName} is not valid");
                return;
            }

            Vehicle vehicle = World.CreateVehicle(model, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 5);
            if (vehicle == null)
            {
                Log($"Failed to create vehicle {modelName}");
                return;
            }

            if (spawnInsideVehicle)
            {
                Game.Player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
            }

            if (addBlipToSpawnedCar)
            {
                Blip blip = vehicle.AddBlip();
                blip.Name = vehicleName;
            }

            // Manage the maximum number of vehicles spawned
            if (maxVehicles != -1 && spawnedVehicles.Count >= maxVehicles)
            {
                Vehicle oldestVehicle = spawnedVehicles.First();
                oldestVehicle.Delete();
                spawnedVehicles.RemoveAt(0);
            }

            spawnedVehicles.Add(vehicle);
            AddVehicleToSpawnedVehiclesMenu(vehicle, modelName);
        }

        private void AddVehicleToSpawnedVehiclesMenu(Vehicle vehicle, string modelName)
        {
            string vehicleName = addOnVehicleNamesToModels.FirstOrDefault(x => x.Value == modelName).Key;

            if (vehicleName == null)
            {
                Log($"Vehicle name not found for model: {modelName}");
                return;
            }

            NativeMenu vehicleMenu = new NativeMenu(vehicleName, "Options for " + vehicleName);

            NativeItem deleteItem = new NativeItem("Delete");
            deleteItem.Activated += (sender, args) => DeleteVehicle(vehicle);

            vehicleMenu.Add(deleteItem);

            // Now correctly create a submenu item using the vehicleMenu
            NativeSubmenuItem vehicleSubmenuItem = new NativeSubmenuItem(vehicleMenu);
            spawnedVehiclesMenu.Add(vehicleSubmenuItem);

            Log($"Added vehicle to spawned vehicles menu: {vehicleName}");
        }

        private void DeleteVehicle(Vehicle vehicle)
        {
            vehicle.Delete();
            spawnedVehicles.Remove(vehicle);
            RemoveVehicleFromSpawnedVehiclesMenu(vehicle);
            Log($"Deleted vehicle: {vehicle.DisplayName}. Remaining vehicles: {spawnedVehicles.Count}");
        }

        private void RemoveVehicleFromSpawnedVehiclesMenu(Vehicle vehicle)
        {
            string vehicleName = vehicle.DisplayName; // Adjust as necessary for your logic
            var vehicleItem = spawnedVehiclesMenu.Items.OfType<NativeSubmenuItem>()
                .FirstOrDefault(item => item.Title == vehicleName);

            if (vehicleItem != null)
            {
                spawnedVehiclesMenu.Remove(vehicleItem);
                Log($"Removed vehicle from spawned vehicles menu: {vehicleName}");
            }
        }

        private void TeleportToVehicle(Vehicle vehicle)
        {
            Game.Player.Character.Position = vehicle.Position;
            Log($"Teleported to vehicle: {vehicle.DisplayName}");
        }

        private void MarkVehicleOnMap(Vehicle vehicle)
        {
            Blip blip = vehicle.AddBlip();
            blip.Name = vehicle.DisplayName;
            Log($"Marked vehicle on map: {vehicle.DisplayName}");
        }

        private void WriteSettingsToIniFile()
        {
            string iniFilePath = "./scripts/STRPVAddon/AddonCars.ini";

            using (StreamWriter writer = new StreamWriter(iniFilePath, false)) // 'false' to overwrite
            {
                writer.WriteLine($"SpawnInsideVehicle={spawnInsideVehicle}");
                writer.WriteLine($"PreventVehicleDespawn={preventVehicleDespawn}");
                writer.WriteLine($"AddBlipToSpawnedCar={addBlipToSpawnedCar}");
                writer.WriteLine($"MenuOpenKey={menuOpenKey}");
                writer.WriteLine($"MaxVehicles={(maxVehicles == -1 ? "Unlimited" : maxVehicles.ToString())}");
            }
        }
    }
}
