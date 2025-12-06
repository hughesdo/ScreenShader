using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.GLControl;

namespace ScreenShader
{
    public class WallpaperForm : Form
    {
        // Windows API for click-through window
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private GLControl _gl;
        private int _program;
        private int _vao;
        private int _vbo;
        private int _vertexShader;
        private Stopwatch _timer = new Stopwatch();
        private int _uTime;
        private int _uResolution;
        private bool _clickThrough = false;

        // Shader switching
        private ShaderLoader _shaderLoader = new();
        private System.Windows.Forms.Timer _shaderSwitchTimer;
        private System.Windows.Forms.Timer _renderTimer;
        private const int SHADER_SWITCH_INTERVAL_MS = 60000; // 1 minute
        private const int RENDER_INTERVAL_MS = 16; // ~60 FPS

        // Specific shader to use (if set, only this shader is loaded)
        public string? SpecificShader { get; set; }
        // If true, skip loading shaders from files and use only the built-in default
        public bool UseDefaultShaderOnly { get; set; }

        // System tray
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _isPaused = false;

        public WallpaperForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;  // Hide from taskbar, use system tray instead
            StartPosition = FormStartPosition.Manual;
            Text = "ScreenShader";

            _gl = new GLControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            Controls.Add(_gl);

            _gl.Load += OnLoad;
            _gl.Paint += OnRender;
            _gl.Resize += OnResize;

            // Use timer-based rendering instead of Application.Idle
            // (Application.Idle doesn't work when parented to WorkerW)
            _renderTimer = new System.Windows.Forms.Timer();
            _renderTimer.Interval = RENDER_INTERVAL_MS;
            _renderTimer.Tick += OnRenderTimer;

            // Setup shader switch timer
            _shaderSwitchTimer = new System.Windows.Forms.Timer();
            _shaderSwitchTimer.Interval = SHADER_SWITCH_INTERVAL_MS;

            // Setup system tray
            SetupSystemTray();
            _shaderSwitchTimer.Tick += OnShaderSwitchTimer;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            Logger.Log($"OnLoad: GLControl size = {_gl.Width}x{_gl.Height}");

            GL.Disable(EnableCap.DepthTest);
            GL.Viewport(0, 0, _gl.Width, _gl.Height);

            // Log OpenGL info
            Logger.Log($"OpenGL Version: {GL.GetString(StringName.Version)}");
            Logger.Log($"OpenGL Renderer: {GL.GetString(StringName.Renderer)}");
            Logger.Log($"OpenGL Vendor: {GL.GetString(StringName.Vendor)}");

            // Compile vertex shader once (reused for all fragment shaders)
            Logger.Log("Compiling vertex shader...");
            _vertexShader = CompileShader(ShaderType.VertexShader, ShaderSources.VertexShader);
            Logger.Log($"Vertex shader compiled: ID={_vertexShader}");

            // Load shaders from directory (unless using default only)
            if (!UseDefaultShaderOnly)
            {
                string shaderDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diatribes_ShadersV2");
                Logger.Log($"Looking for shaders in: {shaderDir}");
                if (!Directory.Exists(shaderDir))
                {
                    Logger.LogWarning($"Directory not found, trying current directory...");
                    shaderDir = Path.Combine(Directory.GetCurrentDirectory(), "diatribes_ShadersV2");
                    Logger.Log($"Trying: {shaderDir}");
                }

                if (!string.IsNullOrEmpty(SpecificShader))
                    Logger.Log($"Loading specific shader: {SpecificShader}");

                _shaderLoader.LoadShadersFromDirectory(shaderDir, SpecificShader);
            }
            else
            {
                Logger.Log("Using default shader only - skipping file loading");
            }

            // Setup VAO/VBO
            Logger.Log("Setting up VAO/VBO...");
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            float[] vertices =
            {
                -1f, -1f,
                 3f, -1f,
                -1f,  3f
            };

            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.BindVertexArray(0);
            Logger.Log($"VAO={_vao}, VBO={_vbo}");

            // Load initial shader
            LoadCurrentShader();

            _timer.Start();
            _shaderSwitchTimer.Start();
            _renderTimer.Start();
            Logger.Log("OnLoad complete, all timers started");
        }

        private void LoadCurrentShader()
        {
            var shader = _shaderLoader.CurrentShader;
            string fragmentSource = shader?.FragmentSource ?? ShaderSources.DefaultFragmentShader;
            string shaderName = shader?.Name ?? "Default";
            string shaderFile = shader?.FilePath ?? "N/A";

            Logger.Log($"Loading shader: {shaderName} (file: {Path.GetFileName(shaderFile)})");

            if (!TryCompileProgram(fragmentSource, out int newProgram))
            {
                Logger.LogError($"Failed to compile shader: {shaderName}, trying next...");
                // Try next shader
                _shaderLoader.GetNextShader();
                shader = _shaderLoader.CurrentShader;
                fragmentSource = shader?.FragmentSource ?? ShaderSources.DefaultFragmentShader;
                shaderName = shader?.Name ?? "Default";

                if (!TryCompileProgram(fragmentSource, out newProgram))
                {
                    // Fall back to default
                    Logger.LogWarning("Falling back to default shader");
                    TryCompileProgram(ShaderSources.DefaultFragmentShader, out newProgram);
                    shaderName = "Default";
                }
            }

            // Delete old program if exists
            if (_program != 0)
            {
                GL.DeleteProgram(_program);
            }

            _program = newProgram;
            _uTime = GL.GetUniformLocation(_program, "iTime");
            _uResolution = GL.GetUniformLocation(_program, "iResolution");

            Logger.Log($"Shader loaded: {shaderName}, program={_program}, uTime={_uTime}, uResolution={_uResolution}");

        }

        private bool TryCompileProgram(string fragmentSource, out int program)
        {
            program = 0;

            int fsId = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fsId, fragmentSource);
            GL.CompileShader(fsId);
            GL.GetShader(fsId, ShaderParameter.CompileStatus, out int ok);

            if (ok == 0)
            {
                string error = GL.GetShaderInfoLog(fsId);
                Logger.LogError($"Fragment shader compile error: {error}");
                GL.DeleteShader(fsId);
                return false;
            }

            program = GL.CreateProgram();
            GL.AttachShader(program, _vertexShader);
            GL.AttachShader(program, fsId);
            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);

            GL.DetachShader(program, fsId);
            GL.DeleteShader(fsId);

            if (linked == 0)
            {
                string error = GL.GetProgramInfoLog(program);
                Logger.LogError($"Program link error: {error}");
                GL.DeleteProgram(program);
                program = 0;
                return false;
            }

            Logger.Log($"Program compiled successfully: {program}");
            return true;
        }

        private void OnShaderSwitchTimer(object? sender, EventArgs e)
        {
            Logger.Log("Shader switch timer fired");
            _shaderLoader.GetNextShader();
            LoadCurrentShader();
        }

        private void OnResize(object? sender, EventArgs e)
        {
            Logger.Log($"OnResize: {_gl.Width}x{_gl.Height}");
            GL.Viewport(0, 0, _gl.Width, _gl.Height);
        }

        private static int _frameCount = 0;
        private static DateTime _lastLogTime = DateTime.Now;

        private void OnRenderTimer(object? sender, EventArgs e)
        {
            // Make context current and render directly
            _gl.MakeCurrent();
            RenderFrame();
            _gl.SwapBuffers();
        }

        private void OnRender(object? sender, PaintEventArgs e)
        {
            RenderFrame();
        }

        private void RenderFrame()
        {
            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_program);

            float time = (float)_timer.Elapsed.TotalSeconds;
            GL.Uniform1(_uTime, time);
            GL.Uniform3(_uResolution, new Vector3(_gl.Width, _gl.Height, 1.0f));

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.BindVertexArray(0);

            // Log every 5 seconds
            _frameCount++;
            if ((DateTime.Now - _lastLogTime).TotalSeconds >= 5)
            {
                Logger.Log($"Rendering: {_frameCount} frames, time={time:F2}s, resolution={_gl.Width}x{_gl.Height}");
                _frameCount = 0;
                _lastLogTime = DateTime.Now;
            }
        }

        private static int CompileShader(ShaderType type, string src)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
                throw new Exception($"Shader compile error ({type}): {GL.GetShaderInfoLog(id)}");
            return id;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // Don't use WS_EX_TOOLWINDOW anymore - we want it in taskbar for safety
                return cp;
            }
        }

        private void SetupSystemTray()
        {
            _trayMenu = new ContextMenuStrip();

            var pauseItem = new ToolStripMenuItem("⏸ Pause", null, OnPauseClick);
            pauseItem.Name = "pauseItem";
            _trayMenu.Items.Add(pauseItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("❌ Exit", null, OnExitClick);

            var appIcon = CreateShaderIcon();
            Icon = appIcon;  // Set form icon

            _trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "ScreenShader - Running",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => OnPauseClick(s, e);
            Logger.Log("System tray icon created");
        }

        /// <summary>
        /// Creates a colorful shader-themed icon programmatically
        /// </summary>
        private Icon CreateShaderIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Create a colorful gradient background (shader-like)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;

                    // Rainbow-ish shader pattern
                    int r = (int)(128 + 127 * Math.Sin(u * Math.PI * 2));
                    int g2 = (int)(128 + 127 * Math.Sin(v * Math.PI * 2 + Math.PI / 3));
                    int b = (int)(128 + 127 * Math.Sin((u + v) * Math.PI + Math.PI * 2 / 3));

                    bitmap.SetPixel(x, y, Color.FromArgb(255, r, g2, b));
                }
            }

            // Draw a stylized "S" for Shader
            using var pen = new Pen(Color.White, 2.5f);
            g.DrawArc(pen, 6, 4, 16, 12, 180, 180);
            g.DrawArc(pen, 10, 16, 16, 12, 0, 180);

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void OnPauseClick(object? sender, EventArgs e)
        {
            _isPaused = !_isPaused;

            var pauseItem = _trayMenu.Items["pauseItem"] as ToolStripMenuItem;
            if (_isPaused)
            {
                _renderTimer.Stop();
                _shaderSwitchTimer.Stop();
                pauseItem!.Text = "▶ Play";
                _trayIcon.Text = "ScreenShader - Paused";
                Logger.Log("Paused");
            }
            else
            {
                _renderTimer.Start();
                _shaderSwitchTimer.Start();
                pauseItem!.Text = "⏸ Pause";
                _trayIcon.Text = "ScreenShader - Running";
                Logger.Log("Resumed");
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            Logger.Log("Exit clicked from system tray");
            _trayIcon.Visible = false;
            Close();
        }

        /// <summary>
        /// Make the window click-through so mouse events pass to windows behind it
        /// Uses WM_NCHITTEST instead of WS_EX_LAYERED (which breaks OpenGL)
        /// </summary>
        public void MakeClickThrough()
        {
            if (_clickThrough) return;
            _clickThrough = true;
            Logger.Log("Window is now click-through (via WM_NCHITTEST)");
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        protected override void WndProc(ref Message m)
        {
            // Make all mouse hits pass through to windows behind
            if (_clickThrough && m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderTimer?.Stop();
                _renderTimer?.Dispose();
                _shaderSwitchTimer?.Stop();
                _shaderSwitchTimer?.Dispose();
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
