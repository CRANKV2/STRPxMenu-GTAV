
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
        private int maxVehicleCount = 4;
        private List<Vehicle> spawnedVehicles = new List<Vehicle>();

        private string[] vehicleModels = { "adder", "blista", "comet2", "dominator" };
        private string[] vehicleNames = { "Adder", "Blista", "Comet", "Dominator" };
        private int currentIndex = 0;

        public VehicleSpawner()
        {
            menuPool = new MenuPool();

            // Create start menu
            startMenu = new UIMenu("STRP Spawner", "Welcome to STRP Spawner!");

            // Add menu items to start menu
            UIMenuItem spawnVehiclesItem = new UIMenuItem("Spawn Add-On Vehicles", "Spawn vehicles from the add-on list.");
            UIMenuItem settingsItem = new UIMenuItem("Settings", "Customize STRP Spawner settings.");
            UIMenuItem creditsItem = new UIMenuItem("Credits", "View credits and information.");

            // Add menu items to start menu
            startMenu.AddItem(spawnVehiclesItem);
            startMenu.AddItem(settingsItem);
            startMenu.AddItem(creditsItem);

            // Subscribe to start menu item events
            startMenu.OnItemSelect += OnStartMenuItemSelect;

            // Add start menu to menu pool
            menuPool.Add(startMenu);

            // Create main menu
            mainMenu = new UIMenu("STRP Spawner", "Select a vehicle to spawn:");

            // Add vehicles as menu items to the main menu
            foreach (var vehicleName in vehicleNames)
            {
                UIMenuItem menuItem = new UIMenuItem(vehicleName);
                mainMenu.AddItem(menuItem);
            }

            // Subscribe to main menu item events
            mainMenu.OnItemSelect += OnMainMenuItemSelect;

            // Add main menu to menu pool
            menuPool.Add(mainMenu);

            // Register script events
            Tick += OnTick;
            KeyDown += OnKeyDown;
        }

        private void OnTick(object sender, EventArgs e)
        {
            menuPool.ProcessMenus();
            // Despawn excess vehicles if more than the maximum count
            while (spawnedVehicles.Count > maxVehicleCount)
            {
                Vehicle vehicleToDespawn = spawnedVehicles[0];
                vehicleToDespawn.Delete();
                spawnedVehicles.RemoveAt(0);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Toggle start menu visibility with the F5 key
            if (e.KeyCode == Keys.F5)
            {
                startMenu.Visible = !startMenu.Visible;
                if (!startMenu.Visible)
                {
                    GTA.UI.Hud.HideComponentThisFrame(HudComponent.WantedStars);
                }
            }
        }

        private void OnStartMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            // Handle start menu item selection
            if (selectedItem.Text == "Spawn Add-On Vehicles")
            {
                // Show main menu to spawn vehicles
                mainMenu.Visible = true;
                // Hide the start menu
                startMenu.Visible = false;
            }
            else if (selectedItem.Text == "Settings")
            {
                // Implement settings functionality
                GTA.UI.Notification.Show("Settings menu will be implemented later.", false);
            }
            else if (selectedItem.Text == "Credits")
            {
                // Display credits information
                GTA.UI.Notification.Show("STRP Spawner by STRP x DEVS", true);
            }
        }

        private void OnMainMenuItemSelect(UIMenu sender, UIMenuItem selectedItem, int index)
        {
            // Spawn the selected vehicle
            SpawnVehicle(vehicleModels[index]);
        }

        private void SpawnVehicle(string modelName)
        {
            // Spawn vehicle logic
            Model model = new Model(modelName);
            if (model.IsValid && model.IsInCdImage)
            {
                model.Request();
                while (!model.IsLoaded) Script.Wait(100);
                Vehicle vehicle = World.CreateVehicle(model, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 5);
                if (vehicle != null)
                {
                    vehicle.PlaceOnGround();
                    Game.Player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
                    ShowNotification($"Spawned <font colors='#00FF00'>{modelName}</font> and entered the vehicle.", true);
                    spawnedVehicles.Add(vehicle);
                }
                else
                {
                    ShowNotification($"<font color='#FF0000'>Failed to spawn vehicle.</font>", false);
                }
                model.MarkAsNoLongerNeeded();
            }
            else
            {
                ShowNotification($"<font color='#FF0000'>Invalid model.</font>", false);
            }
        }

        private void ShowNotification(string message, bool isSuccess)
        {
            if (isSuccess)
            {
                // Show success notification in green color
                GTA.UI.Notification.Show(message, true);
            }
            else
            {
                // Show error notification in red color
                GTA.UI.Notification.Show(message, false);
            }
        }
    }
}