using System;
using System.Windows.Forms;

namespace ShaderReviewer
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string shaderDir = "";
            bool resetReviews = false;

            // Parse arguments
            foreach (var arg in args)
            {
                if (arg == "--reset" || arg == "-r")
                    resetReviews = true;
                else if (!arg.StartsWith("-"))
                    shaderDir = arg;
            }

            if (string.IsNullOrEmpty(shaderDir))
            {
                // Try several common locations
                var candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "diatribes_ShadersV2"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diatribes_ShadersV2"),
                    Path.Combine(Directory.GetCurrentDirectory(), "diatribes_ShadersV2"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "diatribes_ShadersV2"),
                    @"C:\Users\Downstairs\Desktop\Don Apps\ScreenShader\diatribes_ShadersV2"
                };

                foreach (var candidate in candidates)
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (Directory.Exists(fullPath))
                    {
                        shaderDir = fullPath;
                        break;
                    }
                }
            }

            if (!Directory.Exists(shaderDir))
            {
                MessageBox.Show($"Shader directory not found!\n\nTried multiple locations.\nRun with path argument:\nShaderReviewer.exe \"path\\to\\diatribes_ShadersV2\"",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Handle reset flag
            if (resetReviews)
            {
                var workingFile = Path.Combine(Path.GetDirectoryName(shaderDir)!, "reviewed_working.txt");
                var brokenFile = Path.Combine(Path.GetDirectoryName(shaderDir)!, "reviewed_broken.txt");

                if (File.Exists(workingFile)) File.Delete(workingFile);
                if (File.Exists(brokenFile)) File.Delete(brokenFile);

                MessageBox.Show("Review files cleared! Starting fresh.", "Reset Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Application.Run(new ReviewerForm(shaderDir));
        }
    }
}

