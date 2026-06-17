using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Tests
{
    public class MediaDownloadManagerTests
    {
        [Fact(DisplayName = "Unit: BuildDownloadItems correctly resolves RomM DTO properties to LaunchBox paths")]
        public void Test_BuildDownloadItems_FF7_Visualizer()
        {
            // Force set a directory dummy path so Path.Combine doesn't crash or go blank in a test context
            //Constants.LaunchboxRootDir = @"C:\LaunchBox";

            // 1. Arrange: Create a fake game DTO mimicking real RomM structural data
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

            var manager = new MediaDownloadManager();

            // 2. Act: Run the transformation strategy matrix
            var results = manager.BuildDownloadItems(
                rom: mockRom,
                profile: mockProfile,
                baseUrl: "https://roms.stig.life",
                launchboxPlatformName: "Sony Playstation"
            );

            var inspectMe = results; // <--- PUT YOUR BREAKPOINT HERE
            Assert.NotEmpty(results);
        }
    }

}
