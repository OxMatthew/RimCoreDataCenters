using System;
using System.Drawing;
using System.IO;

namespace AssetGen
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1 || !Directory.Exists(args[0]))
            {
                Console.Error.WriteLine("usage: AssetGen <repository root>");
                return 1;
            }
            string root = args[0];
            string tex = Path.Combine(root, "Mod", "Textures", "RCDC", "Things");
            string building = Path.Combine(tex, "Building");
            string item = Path.Combine(tex, "Item");

            using (Bitmap rack = Sprites.ServerRack())
            {
                Sprites.SaveRotations(rack, building, "ServerRack");
            }
            using (Bitmap core = Sprites.NetworkCore())
            {
                Sprites.SaveRotations(core, building, "NetworkCore");
            }
            using (Bitmap cooler = Sprites.PrecisionCooler())
            {
                Sprites.SaveRotations(cooler, building, "PrecisionCooler");
            }
            using (Bitmap console = Sprites.OperationsConsole())
            {
                Sprites.SaveRotations(console, building, "OperationsConsole");
            }
            using (Bitmap ups = Sprites.UpsUnit())
            {
                Sprites.Save(ups, Path.Combine(building, "UpsUnit.png"));
            }
            using (Bitmap leaf = Sprites.BiometricDoorLeaf())
            {
                Sprites.Save(leaf, Path.Combine(building, "BiometricDoor_Mover.png"));
                using (Bitmap icon = Sprites.DoorMenuIcon(leaf))
                {
                    Sprites.Save(icon, Path.Combine(building, "BiometricDoor_MenuIcon.png"));
                }
            }
            using (Bitmap leaf = Sprites.MetalDetectorLeaf())
            {
                Sprites.Save(leaf, Path.Combine(building, "MetalDetector_Mover.png"));
                using (Bitmap icon = Sprites.DoorMenuIcon(leaf))
                {
                    Sprites.Save(icon, Path.Combine(building, "MetalDetector_MenuIcon.png"));
                }
            }
            using (Bitmap ai = Sprites.AiCore())
            {
                Sprites.SaveRotations(ai, building, "AiCore");
            }
            string ui = Path.Combine(root, "Mod", "Textures", "RCDC", "UI");
            using (Bitmap directive = Sprites.AiIconDirective())
            {
                Sprites.Save(directive, Path.Combine(ui, "AiDirective.png"));
            }
            using (Bitmap report = Sprites.AiIconReport())
            {
                Sprites.Save(report, Path.Combine(ui, "AiReport.png"));
            }
            using (Bitmap specialization = Sprites.SpecializationIcon())
            {
                Sprites.Save(specialization, Path.Combine(ui, "Specialization.png"));
            }
            using (Bitmap cartridge = Sprites.DataCartridge())
            {
                Sprites.Save(cartridge, Path.Combine(item, "DataCartridge.png"));
            }
            using (Bitmap research = Sprites.DataCartridgeResearch())
            {
                Sprites.Save(research, Path.Combine(item, "DataCartridgeResearch.png"));
            }
            using (Bitmap financial = Sprites.DataCartridgeFinancial())
            {
                Sprites.Save(financial, Path.Combine(item, "DataCartridgeFinancial.png"));
            }
            using (Bitmap medical = Sprites.DataCartridgeMedical())
            {
                Sprites.Save(medical, Path.Combine(item, "DataCartridgeMedical.png"));
            }

            string sounds = Path.Combine(root, "Mod", "Sounds", "RCDC");
            Audio.WriteWav(Path.Combine(sounds, "ServerHum.wav"), Audio.ServerHum());
            Audio.WriteWav(Path.Combine(sounds, "CoolerFan.wav"), Audio.CoolerFan());
            Audio.WriteWav(Path.Combine(sounds, "CartridgeReady.wav"), Audio.CartridgeReady());
            Audio.WriteWav(Path.Combine(sounds, "RackAlarm.wav"), Audio.RackAlarm());
            Audio.WriteWav(Path.Combine(sounds, "AccessDenied.wav"), Audio.AccessDenied());
            Audio.WriteWav(Path.Combine(sounds, "AiChime.wav"), Audio.AiChime());
            Audio.WriteWav(Path.Combine(sounds, "EspionageAlert.wav"), Audio.EspionageAlert());

            if (args.Length > 1 && args[1] == "--preview")
            {
                string previewPath = Path.Combine(root, "Mod", "About", "Preview.png");
                using (Bitmap preview = Preview.Render())
                {
                    Sprites.Save(preview, previewPath);
                }
                Console.WriteLine("Wrote preview: " + previewPath);
            }

            Console.WriteLine("Asset generation complete.");
            return 0;
        }
    }
}
