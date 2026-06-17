using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Properties;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace RommStar.Core.Temp
{
    internal class Tests
    {
        internal static void AddNewLaunchboxPlatform()
        {
            IPlatform newPLatform = PluginHelper.DataManager.AddNewPlatform("TestPLatform");
            newPLatform.ScrapeAs = "TestPLatScrapeAs";
        }

        private class MockPlatformFolder : Unbroken.LaunchBox.Plugins.Data.IPlatformFolder
        {
            public string FolderPath { get; set; } = string.Empty;
            public string MediaType { get; set; } = string.Empty;
            public string Platform { get; set; } = string.Empty;
        }

        public static List<MediaDownloadItem> MediaDownloadManagerTest()
        {
            // Set a base directory for the test context so relative paths have something to resolve against
            //Constants.LaunchboxRootDir = @"C:\LaunchBoxRoot";

            // 1. Arrange: Your exact real-world RomM data mock
            var mockRom = new RomDTO
            {
                Name = "Final Fantasy 7",
                MediaManual = "roms/445/8864/manual/8864.pdf",
                MediaVideo = "roms/445/8864/video/video.mp4",
                MediaBoxFront = "/assets/romm/resources/roms/445/8864/cover/big.png?ts=2026-06-16 13:49:16",
                MergedScreenshots = new List<string>
                {
                    "/assets/romm/resources/roms/445/8864/screenshots/0.jpg",
                    "/assets/romm/resources/roms/445/8864/screenshots/1.jpg"
                },
                ScreenscrapeMetadata = new RomScreenscraperMetadataDTO
                {
                    MediaBox3D = "roms/445/8864/box3d/box3d.png",
                    MediaLogo = "roms/445/8864/logo/logo.png"
                }
            };

            var mockProfile = new MediaSelectionProfile();
            mockProfile.EnabledTypes.Clear();
            mockProfile.EnabledTypes.Add(MediaType.BoxFront);
            mockProfile.EnabledTypes.Add(MediaType.Box3D);
            mockProfile.EnabledTypes.Add(MediaType.Video);
            mockProfile.EnabledTypes.Add(MediaType.Logo);
            mockProfile.EnabledTypes.Add(MediaType.Screenshot);

            // Construct the mock platform folders array covering all variants of LaunchBox paths
            var mediaFolders = new Unbroken.LaunchBox.Plugins.Data.IPlatformFolder[]
            {
                // Scenario A: Absolute Local Path (Will ignore the LaunchBox Root completely)
                new MockPlatformFolder { Platform = "Sony Playstation", MediaType = "Box - Front", FolderPath = @"C:\Temp\Sony Playstation\Box - Front" },
                
                // Scenario B: Relative Directory Traversal (Will step backward out of C:\LaunchBoxRoot)
                new MockPlatformFolder { Platform = "Sony Playstation", MediaType = "Box - Front - 3D", FolderPath = @"..\..\temp\project tests\RommStar\roms\psx" },
                
                // Scenario C: Standard Relative Path (Appends cleanly to C:\LaunchBoxRoot)
                new MockPlatformFolder { Platform = "Sony Playstation", MediaType = "Video", FolderPath = @"Videos\Sony Playstation" },
                
                // Scenario D: Network Server UNC Share Path (Maintains network URI format entirely)
                new MockPlatformFolder { Platform = "Sony Playstation", MediaType = "Clear Logo", FolderPath = @"\\HPServer\Temp\Sony Playstation\Box - Front" },
                
                // Scenario E: Another Standard Relative Path
                new MockPlatformFolder { Platform = "Sony Playstation", MediaType = "Screenshot - Gameplay", FolderPath = @"Images\Sony Playstation\Screenshot - Gameplay" }
            };

            var manager = new MediaDownloadManager();

            // 2. Act: Run your actual engine code directly in the main assembly pipeline
            // 2. Act: Run the code with Rom Filename selection and Media Priority Override active
            var results = manager.BuildDownloadItems(
                rom: mockRom,
                profile: mockProfile,
                baseUrl: "https://roms.stig.life",
                launchboxPlatformName: "Sony Playstation",
                launchboxMediaFolders: mediaFolders,
                romFilename: "ff7_disc1_usa", // Clean filename from the platform storage
                useRomFilenameForMedia: true,  // Use filename instead of Game Title
                forceMediaPriority: true       // Force append -00 overrides
            );

            // 3. Output Window Fallback: Check the Output window to verify path transformations instantly
            Debug.WriteLine($"=== MANUAL TESTING RESULTS: Found {results.Count} Items ===");
            foreach (var item in results)
            {
                Debug.WriteLine($"[{item.MediaType}] URL: {item.DownloadUrl} -> PATH: {item.TargetLocalPath}");
            }

            return results; // <--- PUT YOUR BREAKPOINT RIGHT HERE
        }
    }

}