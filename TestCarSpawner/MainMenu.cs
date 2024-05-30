using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;
using NativeUI;

namespace GTAModding
{
    public class VehicleSpawner : Script
    {
        private MenuPool menuPool;
        private UIMenu mainMenu;
        private UIMenu startMenu;
        private UIMenu creditsMenu;
        private UIMenu settingsMenu;
        private int maxVehicleCount = 5;
        private List<Vehicle> spawnedVehicles = new List<Vehicle>();
        private bool spawnInsideVehicle = false;

        private readonly string[] vehicleModels = { "adder", "blista", "comet2", "dominator" };
        private readonly string[] vehicleNames = { "Adder", "Blista", "Comet", "Dominator" };

        private const string StartMenuTitle = "STRP Spawner";
        private const string StartMenuSubtitle = "Welcome to STRP Spawner!";
        private const string SpawnVehiclesItemText = "Spawn Add-On Vehicles";
        private const string SpawnVehiclesItemDescription = "Spawn vehicles from the add-on list.";
        private const string SettingsItemText = "Settings";
        private const string SettingsItemDescription = "Customize STRP Spawner settings.";
        private const string CreditsItemText = "Credits";
        private const string CreditsItemDescription = "View credits and information.";
        private const string SpawnInsideVehicleText = "Spawn Inside Vehicle";
        private const string SpawnInsideVehicleDescription = "Toggle whether to spawn inside the vehicle or not.";

        private const string MainMenuTitle = "STRP Spawner";
        private const string MainMenuSubtitle = "Select a vehicle to spawn:";

        private const string SettingsNotification = "Settings menu will be implemented later.";
        private const string CreditsNotification = "STRP Spawner by STRP x DEVS";
        private const string CreditsMenuTitle = "Credits";
        private const string CreditsMenuSubtitle = "Developed by STRP x DEVS (@CRANKV2)";
        private const string DonationWebsiteItemText = "Donation Website";

        private const string SuccessNotificationColor = "#00FF00";
        private const string ErrorNotificationColor = "#FF0000";

        private const Keys ToggleMenuKey = Keys.F5;

        public VehicleSpawner()
        {
            menuPool = new MenuPool();

            CreateSettingsMenu();  // Move this line to before CreateStartMenu()
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

        private void CreateStartMenu()
        {
            startMenu = new UIMenu(StartMenuTitle, StartMenuSubtitle);

            UIMenuItem spawnVehiclesItem = new UIMenuItem(SpawnVehiclesItemText, SpawnVehiclesItemDescription);
            UIMenuItem settingsItem = new UIMenuItem(SettingsItemText, SettingsItemDescription);
            UIMenuItem creditsItem = new UIMenuItem(CreditsItemText, CreditsItemDescription);

            startMenu.AddItem(spawnVehiclesItem);
            startMenu.AddItem(settingsItem);  // Add settingsItem to startMenu before binding
            startMenu.BindMenuToItem(settingsMenu, settingsItem);  // Bind after adding settingsItem to startMenu
            startMenu.AddItem(creditsItem);

            startMenu.OnItemSelect += OnStartMenuItemSelect;
        }

        private void CreateMainMenu()
        {
            mainMenu = new UIMenu(MainMenuTitle, MainMenuSubtitle);

            foreach (var vehicleName in vehicleNames)
            {
                UIMenuItem menuItem = new UIMenuItem(vehicleName);
                mainMenu.AddItem(menuItem);
            }

            mainMenu.OnItemSelect += OnMainMenuItemSelect;
        }

        private void CreateSettingsMenu()
        {
            settingsMenu = new UIMenu("Settings", "Customize STRP Spawner settings.");

            UIMenuCheckboxItem spawnInsideVehicleItem = new UIMenuCheckboxItem(SpawnInsideVehicleText, spawnInsideVehicle, SpawnInsideVehicleDescription);
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
            creditsMenu = new UIMenu(CreditsMenuTitle, CreditsMenuSubtitle);

            UIMenuItem donationWebsiteItem = new UIMenuItem(DonationWebsiteItemText, "Visit our donation website to support us.");

            creditsMenu.AddItem(donationWebsiteItem);

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
            startMenu.Visible = !startMenu.Visible;

            if (!startMenu.Visible)
            {
                GTA.UI.Hud.HideComponentThisFrame(HudComponent.WantedStars);
            }
        }

        private void OnStartMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            if (selectedItem.Text == SpawnVehiclesItemText)
            {
                ShowMainMenu();
            }
            else if (selectedItem.Text == CreditsItemText)
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
            if (selectedItem.Text == DonationWebsiteItemText)
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
                        ShowNotification($"Spawned <font color='{SuccessNotificationColor}'>{modelName}</font> and entered the vehicle.", true);
                    }
                    else
                    {
                        ShowNotification($"Spawned <font color='{SuccessNotificationColor}'>{modelName}</font> in front of the player.", true);
                    }
                }
                else
                {
                    ShowNotification($"<font color='{ErrorNotificationColor}'>Failed to spawn vehicle.</font>", false);
                }
                model.MarkAsNoLongerNeeded();
            }
            else
            {
                ShowNotification($"<font color='{ErrorNotificationColor}'>Invalid model.</font>", false);
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