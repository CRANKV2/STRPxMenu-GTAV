using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using GTA;
using LemonUI;
using LemonUI.Elements;
using LemonUI.Menus;



internal class Logging
    {

    public static void ClearLogFile()
    {
        string logFilePath = "./scripts/STRPVAddon/AddonCars.log";

        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }
    }

    public static void Log(string message)
    {
        string logFilePath = "./scripts/STRPVAddon/AddonCars.log";

        // Append the log file if it exists
        using (StreamWriter writer = new StreamWriter(logFilePath, true)) // 'true' to append
        {
            writer.WriteLine($"{DateTime.Now}: {message}");
        }
    }





}

