using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using NativeUI;
using System.IO;
using System.Linq;

namespace STRPxDEVS
{
    public class STRPxMENU : Script
    {
        private MenuPool menuPool;
        private UIMenu mainMenu;
        private UIMenu startMenu;
        private UIMenu creditsMenu;
        private UIMenu settingsMenu;
        private int maxVehicleCount;
        private List<Vehicle> spawnedVehicles = new List<Vehicle>();
        private bool spawnInsideVehicle;
        private bool preventVehicleDespawn;
        private Dictionary<string, string> addOnVehicleNamesToModels = new Dictionary<string, string>();
        private VehicleColor vehicleColor;
        private SpawnLocation vehicleSpawnLocation;
        private bool vehicleInvincibility;
        private string vehicleLicensePlate;

        private readonly string[] vehicleModels = { "adder", "blista", "comet2", "dominator" };
        private readonly string[] vehicleNames = { "Adder", "Blista", "Comet", "Dominator" };

        private enum SpawnLocation
        {
            Front,
            Back,
            Left,
            Right
        }

        public STRPxMENU()
        {
            ReadSettingsFromIniFile();

            menuPool = new MenuPool();

            CreateSettingsMenu();
            CreateStartMenu();
            CreateMainMenu();
            CreateCreditsMenu();

            menuPool.Add(startMenu);
            menuPool.Add(mainMenu);
            menuPool.Add(creditsMenu);
            menuPool.Add(settingsMenu);

            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        private Keys ToggleMenuKey;


        private void ReadSettingsFromIniFile()
        {
            string[] lines = File.ReadAllLines("./scripts/STRPMenuFiles/STRPMenu.ini");

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
                    else if (key == "MaxVehicleCount")
                    {
                        int.TryParse(value, out maxVehicleCount);
                    }
                    else if (key == "ToggleMenuKey")
                    {
                        if (Enum.TryParse(value, out Keys parsedKey))
                        {
                            ToggleMenuKey = parsedKey;
                        }
                        else
                        {
                            // If the key is not valid, default to F5
                            ToggleMenuKey = Keys.F5;
                        }
                    }
                    else if (key == "PreventVehicleDespawn")
                    {
                        if (bool.TryParse(value, out bool parsedValue))
                        {
                            preventVehicleDespawn = parsedValue;
                        }
                        else
                        {
                            // Default value if the setting is not valid
                            preventVehicleDespawn = false;
                        }
                    }
                    else if (key == "VehicleColor")
                    {
                        if (Enum.TryParse(value, out VehicleColor parsedColor))
                        {
                            vehicleColor = parsedColor;
                        }
                        else
                        {
                            // Default to black if the setting is not valid
                            vehicleColor = VehicleColor.MetallicBlack;
                        }
                    }
                    else if (key == "VehicleSpawnLocation")
                    {
                        if (Enum.TryParse(value, out SpawnLocation parsedLocation))
                        {
                            vehicleSpawnLocation = parsedLocation;
                        }
                        else
                        {
                            // Default to spawning in front of the player if the setting is not valid
                            vehicleSpawnLocation = SpawnLocation.Front;
                        }
                    }
                    else if (key == "VehicleInvincibility")
                    {
                        if (bool.TryParse(value, out bool parsedValue))
                        {
                            vehicleInvincibility = parsedValue;
                        }
                        else
                        {
                            // Default value if the setting is not valid
                            vehicleInvincibility = false;
                        }
                    }
                    else if (key == "VehicleLicensePlate")
                    {
                        vehicleLicensePlate = value;
                    }
                }
            }
        }

      

        private void CreateStartMenu()
        {
            startMenu = new UIMenu(StringConstants.StartMenuTitle, StringConstants.StartMenuSubtitle);

            UIMenuItem spawnVehiclesItem = new UIMenuItem(StringConstants.SpawnVehiclesItemText, StringConstants.SpawnVehiclesItemDescription);
            UIMenuItem settingsItem = new UIMenuItem(StringConstants.SettingsItemText, StringConstants.SettingsItemDescription);
            UIMenuItem creditsItem = new UIMenuItem(StringConstants.CreditsItemText, StringConstants.CreditsItemDescription);

            startMenu.AddItem(spawnVehiclesItem);
            startMenu.AddItem(settingsItem);
            startMenu.BindMenuToItem(settingsMenu, settingsItem);
            startMenu.AddItem(creditsItem);

            startMenu.OnItemSelect += OnStartMenuItemSelect;
        }

        private void CreateMainMenu()
        {
            mainMenu = new UIMenu(StringConstants.MainMenuTitle, StringConstants.MainMenuSubtitle);

            // Create submenu for in-game vehicles
            UIMenu inGameVehiclesMenu = menuPool.AddSubMenu(mainMenu, StringConstants.InGameVehiclesMenuTitle);
            foreach (var vehicleName in vehicleNames)
            {
                UIMenuItem menuItem = new UIMenuItem(vehicleName);
                inGameVehiclesMenu.AddItem(menuItem);
            }
            inGameVehiclesMenu.OnItemSelect += OnMainMenuItemSelect;

            // Create submenu for add-on vehicles
            UIMenu addOnVehiclesMenu = menuPool.AddSubMenu(mainMenu, StringConstants.AddOnVehiclesMenuTitle);
            string[] addOnVehicleLines = File.ReadAllLines("./scripts/STRPMenuFiles/AddOnCars.ini");
            foreach (var line in addOnVehicleLines)
            {
                // Ignore comment and section header lines
                if (line.StartsWith("#") || line.StartsWith("["))
                {
                    continue;
                }

                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string modelName = parts[0].Trim();
                    string realName = parts[1].Trim();
                    UIMenuItem menuItem = new UIMenuItem(realName);
                    addOnVehiclesMenu.AddItem(menuItem);
                    addOnVehicleNamesToModels[realName] = modelName; // Store the model name in the dictionary
                }
            }
            addOnVehiclesMenu.OnItemSelect += OnAddOnMenuItemSelect;
        }


        private void CreateSettingsMenu()
        {
            settingsMenu = new UIMenu(StringConstants.SettingsItemText, StringConstants.SettingsItemDescription);

            UIMenuCheckboxItem spawnInsideVehicleItem = new UIMenuCheckboxItem(StringConstants.SpawnInsideVehicleText, spawnInsideVehicle, StringConstants.SpawnInsideVehicleDescription);
            UIMenuCheckboxItem preventVehicleDespawnItem = new UIMenuCheckboxItem(StringConstants.PreventVehicleDespawnText, preventVehicleDespawn, StringConstants.PreventVehicleDespawnDescription);
            UIMenuListItem vehicleColorItem = new UIMenuListItem("Vehicle Color", Enum.GetNames(typeof(VehicleColor)).Cast<object>().ToList(), 0);
            UIMenuListItem spawnLocationItem = new UIMenuListItem("Vehicle Spawn Location", Enum.GetNames(typeof(SpawnLocation)).Cast<object>().ToList(), 0);
            UIMenuCheckboxItem vehicleInvincibilityItem = new UIMenuCheckboxItem("Vehicle Invincibility", vehicleInvincibility);
            UIMenuItem vehicleLicensePlateItem = new UIMenuItem("Vehicle License Plate", vehicleLicensePlate);

            settingsMenu.AddItem(spawnInsideVehicleItem);
            settingsMenu.AddItem(preventVehicleDespawnItem);
            settingsMenu.AddItem(vehicleColorItem);
            settingsMenu.AddItem(spawnLocationItem);
            settingsMenu.AddItem(vehicleInvincibilityItem);
            settingsMenu.AddItem(vehicleLicensePlateItem);

            settingsMenu.OnCheckboxChange += (menu, item, checked_) =>
            {
                if (item == spawnInsideVehicleItem)
                {
                    spawnInsideVehicle = checked_;
                }
                else if (item == preventVehicleDespawnItem)
                {
                    preventVehicleDespawn = checked_;
                }
                else if (item == vehicleInvincibilityItem)
                {
                    vehicleInvincibility = checked_;
                }

                WriteSettingsToIniFile(); // Write the updated settings to the INI file
            };

            settingsMenu.OnListChange += (menu, item, index) =>
            {
                if (item == vehicleColorItem)
                {
                    if (Game.Player.Character.IsInVehicle())
                    {
                        Vehicle currentVehicle = Game.Player.Character.CurrentVehicle;
                        VehicleColor selectedColor = (VehicleColor)Enum.Parse(typeof(VehicleColor), item.Items[index].ToString());
                        currentVehicle.Mods.PrimaryColor = selectedColor;
                        currentVehicle.Mods.SecondaryColor = selectedColor;
                        vehicleColor = selectedColor; // Update the setting
                    }
                }
                else if (item == spawnLocationItem)
                {
                    vehicleSpawnLocation = (SpawnLocation)Enum.Parse(typeof(SpawnLocation), item.Items[index].ToString());
                }

                WriteSettingsToIniFile(); // Write the updated settings to the INI file
            };

            settingsMenu.OnItemSelect += (menu, item, index) =>
            {
                if (item == vehicleLicensePlateItem)
                {
                    // Prompt the user to enter a new license plate
                    string newLicensePlate = Game.GetUserInput(vehicleLicensePlate);
                    if (!string.IsNullOrEmpty(newLicensePlate))
                    {
                        vehicleLicensePlate = newLicensePlate;
                        item.Description = newLicensePlate;
                    }

                    WriteSettingsToIniFile(); // Write the updated settings to the INI file
                }
            };
        }

        private void CreateCreditsMenu()
        {
            creditsMenu = new UIMenu(StringConstants.CreditsMenuTitle, StringConstants.CreditsMenuSubtitle);

            UIMenuItem donationWebsiteItem = new UIMenuItem(StringConstants.DonationWebsiteItemText, StringConstants.DonationWebsiteItemDescription);
            UIMenuItem developerInfoItem = new UIMenuItem(StringConstants.DeveloperInfoItemText, StringConstants.DeveloperInfoItemDescription);
            UIMenuItem versionInfoItem = new UIMenuItem(StringConstants.VersionInfoItemText, StringConstants.VersionInfoItemDescription);
            UIMenuItem acknowledgementsItem = new UIMenuItem(StringConstants.AcknowledgementsItemText, StringConstants.AcknowledgementsItemDescription);
            UIMenuItem websiteLinkItem = new UIMenuItem(StringConstants.WebsiteLinkItemText, StringConstants.WebsiteLinkItemDescription);

            creditsMenu.AddItem(donationWebsiteItem);
            creditsMenu.AddItem(developerInfoItem);
            creditsMenu.AddItem(versionInfoItem);
            creditsMenu.AddItem(acknowledgementsItem);
            creditsMenu.AddItem(websiteLinkItem);

            creditsMenu.OnItemSelect += OnCreditsMenuItemSelect;
        }

        private void OnTick(object sender, EventArgs e)
        {
            menuPool.ProcessMenus();

            DespawnExcessVehicles();
        }

        private void DespawnExcessVehicles()
        {
            while (spawnedVehicles.Count > maxVehicleCount)
            {
                Vehicle vehicleToDespawn = spawnedVehicles[0];
                vehicleToDespawn.Delete();
                spawnedVehicles.RemoveAt(0);
            }
        }


        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == ToggleMenuKey && !menuPool.IsAnyMenuOpen())
            {
                startMenu.Visible = !startMenu.Visible;
            }
        }

        private void ToggleStartMenuVisibility()
        {
            if (!startMenu.Visible && !mainMenu.Visible && !creditsMenu.Visible && !settingsMenu.Visible)
            {
                startMenu.Visible = true;
            }
            else if (startMenu.Visible)
            {
                startMenu.Visible = false;
                GTA.UI.Hud.HideComponentThisFrame(HudComponent.WantedStars);
            }
        }

        private void OnStartMenuItemSelect(UIMenu menu, UIMenuItem selectedItem, int index)
        {
            if (selectedItem.Text == StringConstants.SpawnVehiclesItemText)
            {
                mainMenu.Visible = true;
            }
            else if (selectedItem.Text == StringConstants.CreditsItemText)
            {
                creditsMenu.Visible = true;
            }
        }

        private void OnAddOnMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (addOnVehicleNamesToModels.ContainsKey(selectedItem.Text))
            {
                string modelName = addOnVehicleNamesToModels[selectedItem.Text];
                SpawnVehicle(modelName);
            }
        }

        private void ShowMainMenu()
        {
            mainMenu.Visible = true;
            startMenu.Visible = false;
        }

        private void ShowCreditsMenu()
        {
            creditsMenu.Visible = true;
            startMenu.Visible = false;
        }

        private void OnCreditsMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (selectedItem.Text == StringConstants.DonationWebsiteItemText)
            {
                ShowNotification(StringConstants.DonationWebsiteNotificationMessage, true);
            }
        }

        private void OnMainMenuItemSelect(UIMenu menu, UIMenuItem selectedItem, int index)
        {
            string selectedVehicleModel = vehicleModels[Array.IndexOf(vehicleNames, selectedItem.Text)];
            SpawnVehicle(selectedVehicleModel);
        }

        private void SpawnVehicle(string modelName)
        {
            Model model = new Model(modelName);
            if (model.IsValid && model.IsInCdImage)
            {
                model.Request();
                while (!model.IsLoaded) Script.Wait(100);

                // Calculate spawn position based on the selected spawn location
                Vector3 spawnPosition = Game.Player.Character.Position;
                switch (vehicleSpawnLocation)
                {
                    case SpawnLocation.Front:
                        spawnPosition += Game.Player.Character.ForwardVector * 5;
                        break;
                    case SpawnLocation.Back:
                        spawnPosition -= Game.Player.Character.ForwardVector * 5;
                        break;
                    case SpawnLocation.Left:
                        spawnPosition -= Game.Player.Character.RightVector * 5;
                        break;
                    case SpawnLocation.Right:
                        spawnPosition += Game.Player.Character.RightVector * 5;
                        break;
                }

                Vehicle vehicle = World.CreateVehicle(model, spawnPosition);
                if (vehicle != null)
                {
                    vehicle.PlaceOnGround();
                    vehicle.IsPersistent = preventVehicleDespawn; // Use the setting preventVehicleDespawn
                    vehicle.IsInvincible = vehicleInvincibility; // Apply the invincibility setting
                    vehicle.Mods.LicensePlate = vehicleLicensePlate; // Apply the license plate setting
                    spawnedVehicles.Add(vehicle);

                    if (spawnInsideVehicle)
                    {
                        Game.Player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                        ShowNotification($"Spawned <font color='{StringConstants.SuccessNotificationColor}'>{modelName}</font> and entered the vehicle.", true);
                    }
                    else
                    {
                        ShowNotification($"Spawned <font color='{StringConstants.SuccessNotificationColor}'>{modelName}</font> in front of the player.", true);
                    }
                }
                else
                {
                    ShowNotification($"<font color='{StringConstants.ErrorNotificationColor}'>Failed to spawn vehicle.</font>", false);
                }
                model.MarkAsNoLongerNeeded();
            }
            else
            {
                ShowNotification($"<font color='{StringConstants.ErrorNotificationColor}'>Invalid model.</font>", false);
            }
        }

        private void WriteSettingsToIniFile()
        {
            List<string> lines = new List<string>
    {
        $"SpawnInsideVehicle = {spawnInsideVehicle}",
        $"MaxVehicleCount = {maxVehicleCount}",
        $"ToggleMenuKey = {ToggleMenuKey}",
        $"PreventVehicleDespawn = {preventVehicleDespawn}",
        $"VehicleColor = {vehicleColor}",
        $"VehicleSpawnLocation = {vehicleSpawnLocation}",
        $"VehicleInvincibility = {vehicleInvincibility}",
        $"VehicleLicensePlate = {vehicleLicensePlate}"
    };

            File.WriteAllLines("./scripts/STRPMenuFiles/STRPMenu.ini", lines);
        }

        private void ShowNotification(string message, bool isSuccess)
        {
            if (isSuccess)
            {
                GTA.UI.Notification.Show(message, true);
            }
            else
            {
                GTA.UI.Notification.Show(message, false);
            }
        }
    }
}