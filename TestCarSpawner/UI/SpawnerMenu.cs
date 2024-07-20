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
        private Dictionary<Vehicle, int> vehicleMarkers = new Dictionary<Vehicle, int>();
        private bool showMarkersAllTheTime = false; // Default to showing markers all the time

        public STRPVAddonSpawner()
        {
            Logging.ClearLogFile();
            Logging.Log("Constructor started");

            ReadSettingsFromIniFile();

            menuPool = new ObjectPool();

            CreateMainMenu();
            CreateVehicleSubMenu();
            CreateSettingsMenu();
            CreateSpawnedVehiclesMenu();
            Logging.Log("Menus created");

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

            Logging.Log("Constructor finished");
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

            Logging.Log("Main menu created");
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

            Logging.Log("Vehicle sub-menu created with items: " + string.Join(", ", addOnVehicleNamesToModels.Keys));
        }

        private void OnTick(object sender, EventArgs e)
        {
            menuPool.Process();

            // Check visibility of the main spawned vehicles menu or any vehicle sub-menu
            bool anyVehicleMenuVisible = spawnedVehiclesMenu.Visible || menuPool.OfType<NativeMenu>().Any(menu => menu.Visible && menu != mainMenu && menu != vehicleSubMenu && menu != settingsMenu);

            if (!showMarkersAllTheTime && !anyVehicleMenuVisible)
            {
                // Remove all markers when the menu is closed and setting is off
                foreach (var veh in spawnedVehicles)
                {
                    HighlightVehicle(veh, false, null, showHandle: false);
                }
                return;
            }

            if (anyVehicleMenuVisible || showMarkersAllTheTime)
            {
                UpdateMarkers();
            }

            foreach (var item in spawnedVehiclesMenu.Items.OfType<NativeItem>())
            {
                try
                {
                    string[] parts = item.Title.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var vehicleName = parts[0].Trim();
                    var handlePart = parts[1].Trim();

                    if (!int.TryParse(handlePart, out int vehicleHandle))
                    {
                        continue;
                    }

                    var vehicle = spawnedVehicles.FirstOrDefault(v => v.Handle == vehicleHandle);

                    if (vehicle != null)
                    {
                        if (item == spawnedVehiclesMenu.SelectedItem)
                        {
                            HighlightVehicle(vehicle, true, vehicleName, showHandle: true);
                        }
                        else
                        {
                            HighlightVehicle(vehicle, showMarkersAllTheTime, vehicleName, showHandle: showMarkersAllTheTime);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log($"Error processing menu item {item.Title}: {ex.Message}");
                }
            }
        }






        private void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == menuOpenKey)
            {
                if (!menuPool.Any(x => x.Visible))
                {
                    mainMenu.Visible = true;
                }
                else
                {
                    foreach (var menu in menuPool.OfType<NativeMenu>())
                    {
                        menu.Visible = false;
                    }
                    // Remove all markers when the menu is closed
                    foreach (var veh in spawnedVehicles)
                    {
                        HighlightVehicle(veh, false, null);
                    }
                }
                Logging.Log($"Menu visibility toggled: {mainMenu.Visible}");
            }
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
                var showMarkersItem = new NativeCheckboxItem("Show Markers All the Time", showMarkersAllTheTime)
                {
                    Description = "Toggle to show or hide markers all the time."
                };

                var maxVehiclesItem = new NativeListItem<string>("Max Vehicles", "1", "2", "3", "4", "5", "Unlimited")
                {
                    Description = "Set the maximum number of vehicles you can spawn. (Removes oldest one if not Unlimited)"
                };

                settingsMenu.Add(spawnInsideVehicleItem);
                settingsMenu.Add(preventVehicleDespawnItem);
                settingsMenu.Add(addBlipToSpawnedCarItem);
                settingsMenu.Add(showMarkersItem);
                settingsMenu.Add(maxVehiclesItem);

                // Log the options being added
                Logging.Log($"Adding Max Vehicles Options: {string.Join(", ", maxVehiclesItem.Items)}");

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

                showMarkersItem.CheckboxChanged += (sender, args) =>
                {
                    showMarkersAllTheTime = showMarkersItem.Checked;
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
                    Logging.Log($"Initial value '{initialValue}' not found. Defaulting to '1'.");
                }

                Logging.Log("Settings menu created successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log($"Error in CreateSettingsMenu: {ex.Message}");
                Logging.Log($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void CreateSpawnedVehiclesMenu()
        {
            spawnedVehiclesMenu = new NativeMenu("", "Spawned Vehicles Controller");

            Logging.Log("Spawned vehicles menu created");
        }

        private void HighlightVehicle(Vehicle vehicle, bool highlight, string vehicleName, bool showHandle = true)
        {
            Logging.Log($"HighlightVehicle called for {vehicle.DisplayName} (Handle: {vehicle.Handle}) with highlight = {highlight}");
            if (highlight)
            {
                DrawMarkerAboveVehicle(vehicle, vehicleName, showHandle);
            }
            else
            {
                // If the logic to remove markers is needed, it can be handled here
                // Example: Function.Call(Hash.REMOVE_MARKER, vehicle.Handle);
            }
        }


        private void DrawMarkerAboveVehicle(Vehicle vehicle, string vehicleName, bool showHandle)
        {
            Vector3 position = vehicle.Position + new Vector3(0, 0, 2.0f); // Adjust height as necessary
            Vector3 textPosition = vehicle.Position + new Vector3(0, 0, 3.2f); // Position the text above the marker

            // Draw marker (arrow pointing down)
            Function.Call(Hash.DRAW_MARKER, 36, position.X, position.Y, position.Z, 0, 0, 0, 0, 0, 0, 1.0f, 1.0f, 1.0f, 255, 215, 0, 255, false, true, 2, false, null, null, false);

            // Combine vehicle name and handle number if showHandle is true
            string displayText = showHandle ? $"{vehicleName} [{vehicle.Handle}]" : vehicleName;

            // Draw vehicle name above the marker
            Function.Call(Hash.SET_DRAW_ORIGIN, textPosition.X, textPosition.Y, textPosition.Z, 0);
            Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.5f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 215, 0, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, displayText);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.0f, 0.0f);
            Function.Call(Hash.CLEAR_DRAW_ORIGIN);

            Logging.Log($"Marker drawn above vehicle {vehicle.DisplayName} (Handle: {vehicle.Handle}).");
        }


        private void UpdateMarkers()
        {
            NativeItem selectedItem = spawnedVehiclesMenu.SelectedItem as NativeItem;
            if (selectedItem != null)
            {
                try
                {
                    string[] parts = selectedItem.Title.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) return; // Ensure there are enough parts

                    var vehicleName = parts[0].Trim();
                    var handlePart = parts[1].Trim();

                    Logging.Log($"Processing selected item: {selectedItem.Title}");
                    if (!int.TryParse(handlePart, out int vehicleHandle))
                    {
                        Logging.Log($"Failed to parse vehicle handle from {handlePart}");
                        return;
                    }

                    var vehicle = spawnedVehicles.FirstOrDefault(v => v.Handle == vehicleHandle);

                    // Highlight the selected vehicle
                    if (vehicle != null)
                    {
                        Logging.Log($"Highlighting vehicle: {vehicle.DisplayName} (Handle: {vehicle.Handle})");
                        HighlightVehicle(vehicle, true, vehicleName, showHandle: true);
                    }

                    // Remove markers from other vehicles
                    foreach (var veh in spawnedVehicles)
                    {
                        if (veh.Handle != vehicleHandle)
                        {
                            HighlightVehicle(veh, showMarkersAllTheTime, null, showHandle: showMarkersAllTheTime);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log($"Error processing menu item {selectedItem.Title}: {ex.Message}");
                }
            }
            else
            {
                // If no item is selected, remove all markers
                foreach (var veh in spawnedVehicles)
                {
                    HighlightVehicle(veh, showMarkersAllTheTime, null, showHandle: showMarkersAllTheTime);
                }
            }
        }






        private void OnAddOnMenuItemSelect(object sender, NativeItem selectedItem)
        {
            string vehicleName = selectedItem.Title;
            string modelName = addOnVehicleNamesToModels[vehicleName];

            Model model = new Model(modelName);
            if (!model.IsInCdImage || !model.IsVehicle)
            {
                Logging.Log($"The Model {modelName} is not valid! You have added the dlcpack to the dlclist.xml ?!");
                return;
            }

            Vehicle vehicle = World.CreateVehicle(model, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 5);
            if (vehicle == null)
            {
                Logging.Log($"Failed to create vehicle {modelName}");
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
                RemoveVehicleFromGameAndMenu(oldestVehicle);
            }

            spawnedVehicles.Add(vehicle);
            AddVehicleToSpawnedVehiclesMenu(vehicle, modelName);

            Logging.Log($"Vehicle {vehicle.DisplayName} (Handle: {vehicle.Handle}) added to spawned vehicles.");

            // Draw marker if showMarkersAllTheTime is enabled
            if (showMarkersAllTheTime)
            {
                HighlightVehicle(vehicle, true, vehicleName, showHandle: false);
            }
        }

        private void AddVehicleToSpawnedVehiclesMenu(Vehicle vehicle, string modelName)
        {
            string vehicleName = addOnVehicleNamesToModels.FirstOrDefault(x => x.Value == modelName).Key;

            if (vehicleName == null)
            {
                Logging.Log($"Vehicle name not found for model: {modelName}");
                return;
            }

            // Create a menu for the vehicle options
            NativeMenu vehicleMenu = new NativeMenu("", "Options for this vehicle");
            vehicleMenu.Banner = new ScaledTexture(PointF.Empty, new SizeF(512, 128), "vcompanionmenu", "STRPVAddonSpawner");


            // Add a teleport option to the vehicle menu
            NativeItem teleportItem = new NativeItem("Teleport To Vehicle");
            teleportItem.Activated += (sender, args) =>
            {
                TeleportToVehicle(vehicle);
                vehicleMenu.Visible = false;  // Hide the submenu after teleporting
                spawnedVehiclesMenu.Visible = true;  // Show the parent menu
            };

            vehicleMenu.Add(teleportItem);

            // Add a teleport into vehicle option to the vehicle menu
            NativeItem teleportIntoItem = new NativeItem("Teleport Into Vehicle");
            teleportIntoItem.Activated += (sender, args) =>
            {
                TeleportIntoVehicle(vehicle);
                vehicleMenu.Visible = false;  // Hide the submenu after teleporting
                spawnedVehiclesMenu.Visible = true;  // Show the parent menu
            };

            vehicleMenu.Add(teleportIntoItem);

            // Add a delete option to the vehicle menu
            NativeItem deleteItem = new NativeItem("Delete Vehicle");
            deleteItem.Activated += (sender, args) =>
            {
                DeleteVehicle(vehicle);
                vehicleMenu.Visible = false;  // Hide the submenu after deleting
                spawnedVehiclesMenu.Visible = true;  // Show the parent menu
            };

            vehicleMenu.Add(deleteItem);


            // Add the vehicle menu as a submenu item to the spawned vehicles menu
            NativeItem vehicleSubmenuItem = new NativeItem(vehicleName + " [" + vehicle.Handle + "] >>>");
            vehicleSubmenuItem.Activated += (sender, args) =>
            {
                vehicleMenu.Visible = true;
                spawnedVehiclesMenu.Visible = false;
                Logging.Log($"Activating submenu for vehicle: {vehicleName}");
            };

            spawnedVehiclesMenu.Add(vehicleSubmenuItem);
            menuPool.Add(vehicleMenu);  // Ensure the new menu is added to the pool for processing

            Logging.Log($"Added vehicle to spawned vehicles menu: {vehicleName}");
        }

        private void RemoveVehicleFromGameAndMenu(Vehicle vehicle)
        {
            RemoveVehicleFromSpawnedVehiclesMenu(vehicle);
            vehicle.Delete();
            spawnedVehicles.Remove(vehicle);
            Logging.Log($"Deleted vehicle: {vehicle.DisplayName}. Remaining vehicles: {spawnedVehicles.Count}");
        }

        private void DeleteVehicle(Vehicle vehicle)
        {
            RemoveVehicleFromSpawnedVehiclesMenu(vehicle);
            vehicle.Delete();
            spawnedVehicles.Remove(vehicle);
            Logging.Log($"Deleted vehicle: {vehicle.DisplayName}. Remaining vehicles: {spawnedVehicles.Count}");
        }

        private void RemoveVehicleFromSpawnedVehiclesMenu(Vehicle vehicle)
        {
            var vehicleItem = spawnedVehiclesMenu.Items.OfType<NativeItem>()
                .FirstOrDefault(item => item.Title.Contains("[" + vehicle.Handle + "]"));

            if (vehicleItem != null)
            {
                spawnedVehiclesMenu.Remove(vehicleItem);
                Logging.Log($"Removed vehicle from spawned vehicles menu: {vehicleItem.Title}");
            }
        }

        private void TeleportToVehicle(Vehicle vehicle)
        {
            // Calculate the position beside the driver door
            Vector3 driverDoorOffset = vehicle.Position + vehicle.RightVector * -1.5f; // Adjust the offset as necessary

            // Set the player's position to the calculated offset
            Game.Player.Character.Position = driverDoorOffset;
            Logging.Log($"Teleported to vehicle: {vehicle.DisplayName}");
        }


        private void TeleportIntoVehicle(Vehicle vehicle)
        {
            Game.Player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
            Logging.Log($"Teleported into vehicle: {vehicle.DisplayName}");
        }


        private void MarkVehicleOnMap(Vehicle vehicle)
        {
            Blip blip = vehicle.AddBlip();
            blip.Name = vehicle.DisplayName;
            Logging.Log($"Marked vehicle on map: {vehicle.DisplayName}");
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
