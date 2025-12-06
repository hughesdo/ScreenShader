using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScreenShader
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Parse command line args
            bool enableLogging = args.Contains("--log") || args.Contains("-l");
            bool windowMode = args.Contains("--window") || args.Contains("-w");
            bool bottomMode = args.Contains("--bottom") || args.Contains("-b");
            bool testMode = args.Contains("--test") || args.Contains("-t");

            // Check for --shader parameter to use a specific shader
            // Use "default" to use only the built-in default shader
            string? specificShader = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--shader" || args[i] == "-s")
                {
                    specificShader = args[i + 1];
                    break;
                }
            }
            bool useDefaultOnly = specificShader?.ToLower() == "default";

            // TEST MODE: Test all shaders and generate working/failed lists
            if (testMode)
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();

                string shaderDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diatribes_ShadersV2");
                if (!Directory.Exists(shaderDir))
                    shaderDir = Path.Combine(Directory.GetCurrentDirectory(), "diatribes_ShadersV2");

                var tester = new ShaderTester();
                tester.TestAllShaders(shaderDir);
                tester.SaveResults(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working_shaders.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "failed_shaders.txt")
                );
                return;
            }

            Logger.Initialize(enableLogging);

            Logger.Log("Starting ScreenShader...");
            Logger.Log($"Window mode: {windowMode}, Bottom mode: {bottomMode}");

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var form = new WallpaperForm();

            // Set specific shader if requested
            if (useDefaultOnly)
            {
                form.UseDefaultShaderOnly = true;
                Logger.Log("Using DEFAULT shader only (built-in gradient)");
            }
            else if (!string.IsNullOrEmpty(specificShader))
            {
                form.SpecificShader = specificShader;
                Logger.Log($"Using specific shader: {specificShader}");
            }

            if (windowMode)
            {
                // Run as a normal window for testing
                Logger.Log("Running in WINDOW mode (not attached to wallpaper)");
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.Text = "ScreenShader - Window Mode";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Size = new System.Drawing.Size(1280, 720);
                form.Show();
            }
            else if (bottomMode)
            {
                // Attach to wallpaper layer (WorkerW or Progman fallback)
                Logger.Log("Running in BOTTOM mode - attaching to wallpaper layer");
                Logger.Log("Press ESCAPE to close, SPACE/N for next shader, P for previous");

                // Hide Windows wallpaper so our shader shows through
                WallpaperHelper.HideWindowsWallpaper();

                // Attach BEFORE showing - this reparents the window
                WallpaperHelper.AttachToWallpaper(form.Handle);

                var screen = WallpaperHelper.GetVirtualScreenBounds();
                form.SetBounds(0, 0, screen.Width, screen.Height); // Relative to parent
                form.TopMost = false;
                form.Show();

                // Restore wallpaper when form closes
                form.FormClosed += (s, e) => WallpaperHelper.RestoreWindowsWallpaper();
            }
            else
            {
                // Attach to wallpaper using WorkerW
                Logger.Log("Attaching to wallpaper...");
                try
                {
                    WallpaperHelper.AttachToWallpaper(form.Handle);

                    // Use all monitors instead of just the primary
                    var screen = WallpaperHelper.GetVirtualScreenBounds();
                    Logger.Log($"Virtual screen bounds: X={screen.X}, Y={screen.Y}, W={screen.Width}, H={screen.Height}");
                    form.SetBounds(screen.X, screen.Y, screen.Width, screen.Height);

                    form.TopMost = false;
                    form.Show();
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogWarning($"WorkerW method failed: {ex.Message}");
                    Logger.Log("Falling back to bottom-most click-through window mode...");
                    var screen = WallpaperHelper.GetVirtualScreenBounds();
                    form.SetBounds(screen.X, screen.Y, screen.Width, screen.Height);
                    form.Show();
                    WallpaperHelper.MakeBottomMost(form.Handle);
                    form.MakeClickThrough();  // CRITICAL: Make window click-through!
                }
            }

            Logger.Log("Form shown, running application...");

            Application.Run(form);



            /*
            using var form = new WallpaperForm();
            WallpaperHelper.AttachToWallpaper(form.Handle);

            var screen = Screen.PrimaryScreen.Bounds;
            form.SetBounds(screen.X, screen.Y, screen.Width, screen.Height);
            form.TopMost = false;
            form.Show();

            Application.Run(form);
            */

            
        }
    }
}
