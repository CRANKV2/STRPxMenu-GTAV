using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;
using NativeUI;
using System.IO;

namespace GTAModding
{
    public class VehicleSpawner : Script
    {
        private MenuPool menuPool;
        private UIMenu mainMenu;
        private UIMenu startMenu;
        private UIMenu creditsMenu;
        private UIMenu settingsMenu;
        private int maxVehicleCount;
        private List<Vehicle> spawnedVehicles = new List<Vehicle>();
        private bool spawnInsideVehicle;

        private readonly string[] vehicleModels = { "adder", "blista", "comet2", "dominator" };
        private readonly string[] vehicleNames = { "Adder", "Blista", "Comet", "Dominator" };

        private const Keys ToggleMenuKey = Keys.F5;

        public VehicleSpawner()
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

        private void ReadSettingsFromIniFile()
        {
            string[] lines = File.ReadAllLines("./scripts/STRPMenu.ini");

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

            foreach (var vehicleName in vehicleNames)
            {
                UIMenuItem menuItem = new UIMenuItem(vehicleName);
                mainMenu.AddItem(menuItem);
            }

            mainMenu.OnItemSelect += OnMainMenuItemSelect;
        }

        private void CreateSettingsMenu()
        {
            settingsMenu = new UIMenu(StringConstants.SettingsItemText, StringConstants.SettingsItemDescription);

            UIMenuCheckboxItem spawnInsideVehicleItem = new UIMenuCheckboxItem(StringConstants.SpawnInsideVehicleText, spawnInsideVehicle, StringConstants.SpawnInsideVehicleDescription);
            settingsMenu.AddItem(spawnInsideVehicleItem);

            settingsMenu.OnCheckboxChange += (menu, item, checked_) =>
            {
                if (item == spawnInsideVehicleItem)
                {
                    spawnInsideVehicle = checked_;
                }
            };
        }

        private void CreateCreditsMenu()
        {
            creditsMenu = new UIMenu(StringConstants.CreditsMenuTitle, StringConstants.CreditsMenuSubtitle);

            UIMenuItem donationWebsiteItem = new UIMenuItem(StringConstants.DonationWebsiteItemText, "Visit our donation website to support us.");
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
            if (e.KeyCode == ToggleMenuKey)
            {
                ToggleStartMenuVisibility();
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

        private void OnStartMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (selectedItem.Text == StringConstants.SpawnVehiclesItemText)
            {
                ShowMainMenu();
            }
            else if (selectedItem.Text == StringConstants.CreditsItemText)
            {
                ShowCreditsMenu();
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
                ShowNotification("Thank you for your support! Please visit: https://strp.cloud/strp/links.html", true);
            }
        }

        private void OnMainMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            SpawnVehicle(vehicleModels[index]);
        }

        private void SpawnVehicle(string modelName)
        {
            Model model = new Model(modelName);
            if (model.IsValid && model.IsInCdImage)
            {
                model.Request();
                while (!model.IsLoaded) Script.Wait(100);
                Vehicle vehicle = World.CreateVehicle(model, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 5);
                if (vehicle != null)
                {
                    vehicle.PlaceOnGround();
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