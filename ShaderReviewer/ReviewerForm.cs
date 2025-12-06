using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.GLControl;

namespace ShaderReviewer
{
    public class ReviewerForm : Form
    {
        private GLControl _gl;
        private Panel _overlayPanel;
        private Label _infoLabel;
        private Label _statusLabel;

        private string _shaderDir;
        private List<string> _allFiles = new();
        private HashSet<string> _workingSet = new();
        private HashSet<string> _brokenSet = new();
        private int _currentIndex = 0;

        private int _program;
        private int _vao, _vbo, _vertexShader;
        private Stopwatch _timer = new();
        private int _uTime, _uResolution;
        private System.Windows.Forms.Timer _renderTimer;

        private string _workingFile;
        private string _brokenFile;

        public ReviewerForm(string shaderDir)
        {
            _shaderDir = shaderDir;
            _workingFile = Path.Combine(Path.GetDirectoryName(shaderDir)!, "reviewed_working.txt");
            _brokenFile = Path.Combine(Path.GetDirectoryName(shaderDir)!, "reviewed_broken.txt");

            Text = "Shader Reviewer";
            Size = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            _gl = new GLControl { Dock = DockStyle.Fill, BackColor = Color.Black };
            _gl.KeyDown += OnKeyDown;  // Also handle keys on GLControl
            Controls.Add(_gl);

            // Overlay panel for instructions
            _overlayPanel = new Panel
            {
                BackColor = Color.FromArgb(180, 0, 0, 0),
                Dock = DockStyle.Top,
                Height = 80
            };

            _infoLabel = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Consolas", 14, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _statusLabel = new Label
            {
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 11),
                Dock = DockStyle.Bottom,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "G = Good/Working  |  B = Bad/Broken  |  → = Skip  |  ESC = Save & Exit"
            };

            _overlayPanel.Controls.Add(_infoLabel);
            _overlayPanel.Controls.Add(_statusLabel);
            Controls.Add(_overlayPanel);
            _overlayPanel.BringToFront();

            _gl.Load += OnLoad;
            _gl.Paint += OnRender;
            _gl.Resize += OnResize;

            _renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _renderTimer.Tick += (s, e) => _gl.Invalidate();

            LoadShaderList();
            LoadPreviousReviews();
        }

        private void LoadShaderList()
        {
            _allFiles = new List<string>(Directory.GetFiles(_shaderDir, "*.txt"));
            _allFiles.Sort();
        }

        private void LoadPreviousReviews()
        {
            if (File.Exists(_workingFile))
                foreach (var line in File.ReadAllLines(_workingFile))
                    if (!string.IsNullOrWhiteSpace(line)) _workingSet.Add(line.Trim());

            if (File.Exists(_brokenFile))
                foreach (var line in File.ReadAllLines(_brokenFile))
                    if (!string.IsNullOrWhiteSpace(line)) _brokenSet.Add(line.Trim());

            // Skip already reviewed shaders
            SkipToNextUnreviewed();
        }

        private void SkipToNextUnreviewed()
        {
            while (_currentIndex < _allFiles.Count)
            {
                var name = Path.GetFileName(_allFiles[_currentIndex]);
                if (!_workingSet.Contains(name) && !_brokenSet.Contains(name))
                    break;
                _currentIndex++;
            }
        }

        private void SaveReviews()
        {
            File.WriteAllLines(_workingFile, _workingSet);
            File.WriteAllLines(_brokenFile, _brokenSet);
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            _vertexShader = CompileShader(ShaderType.VertexShader, ShaderSources.VertexShader);
            SetupVAO();
            _timer.Start();
            _renderTimer.Start();
            LoadCurrentShader();
        }

        private void LoadCurrentShader()
        {
            if (_currentIndex >= _allFiles.Count)
            {
                _infoLabel.Text = "ALL SHADERS REVIEWED!";
                return;
            }

            var file = _allFiles[_currentIndex];
            var name = Path.GetFileName(file);
            var reviewed = _workingSet.Count + _brokenSet.Count;
            var remaining = _allFiles.Count - reviewed;

            _infoLabel.Text = $"[{_currentIndex + 1}/{_allFiles.Count}] {name}\n" +
                              $"✓ {_workingSet.Count} working | ✗ {_brokenSet.Count} broken | {remaining} remaining";

            TryLoadShader(file);
        }

        private void TryLoadShader(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var passIndex = content.IndexOf("// ===== PASS 0: Image =====");
            string shaderCode = passIndex >= 0
                ? content.Substring(passIndex + "// ===== PASS 0: Image =====".Length).Trim()
                : content;

            var fragmentSource = $@"#version 330 core
out vec4 FragColor;
in vec2 fragCoord;
uniform float iTime;
uniform vec3 iResolution;
uniform sampler2D iChannel0;

{shaderCode}

void main() {{
    mainImage(FragColor, fragCoord);
}}";

            int fsId = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fsId, fragmentSource);
            GL.CompileShader(fsId);
            GL.GetShader(fsId, ShaderParameter.CompileStatus, out int ok);

            if (ok == 0)
            {
                string error = GL.GetShaderInfoLog(fsId);
                GL.DeleteShader(fsId);
                _statusLabel.ForeColor = Color.Red;
                _statusLabel.Text = $"COMPILE ERROR: {error.Split('\n')[0]}";
                _program = 0;
                return;
            }

            if (_program > 0) GL.DeleteProgram(_program);

            _program = GL.CreateProgram();
            GL.AttachShader(_program, _vertexShader);
            GL.AttachShader(_program, fsId);
            GL.LinkProgram(_program);
            GL.DetachShader(_program, fsId);
            GL.DeleteShader(fsId);

            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
            {
                _statusLabel.ForeColor = Color.Red;
                _statusLabel.Text = $"LINK ERROR: {GL.GetProgramInfoLog(_program)}";
                GL.DeleteProgram(_program);
                _program = 0;
                return;
            }

            _uTime = GL.GetUniformLocation(_program, "iTime");
            _uResolution = GL.GetUniformLocation(_program, "iResolution");
            _statusLabel.ForeColor = Color.Lime;
            _statusLabel.Text = "G = Good/Working  |  B = Bad/Broken  |  → = Skip  |  ESC = Save & Exit";
        }

        // Override ProcessCmdKey for reliable keyboard capture
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var name = _currentIndex < _allFiles.Count ? Path.GetFileName(_allFiles[_currentIndex]) : null;

            if (keyData == Keys.G && name != null)
            {
                _workingSet.Add(name);
                _brokenSet.Remove(name);
                _currentIndex++;
                SkipToNextUnreviewed();
                LoadCurrentShader();
                return true;
            }
            else if (keyData == Keys.B && name != null)
            {
                _brokenSet.Add(name);
                _workingSet.Remove(name);
                _currentIndex++;
                SkipToNextUnreviewed();
                LoadCurrentShader();
                return true;
            }
            else if (keyData == Keys.Right && name != null)
            {
                _currentIndex++;
                LoadCurrentShader();
                return true;
            }
            else if (keyData == Keys.Left && _currentIndex > 0)
            {
                _currentIndex--;
                LoadCurrentShader();
                return true;
            }
            else if (keyData == Keys.Escape)
            {
                SaveReviews();
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Kept for compatibility but ProcessCmdKey handles everything
        }

        private void OnRender(object? sender, PaintEventArgs e)
        {
            GL.ClearColor(0.1f, 0.1f, 0.15f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            if (_program > 0)
            {
                GL.UseProgram(_program);
                GL.Uniform1(_uTime, (float)_timer.Elapsed.TotalSeconds);
                GL.Uniform3(_uResolution, (float)_gl.Width, (float)_gl.Height, 1f);
                GL.BindVertexArray(_vao);
                GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            }

            _gl.SwapBuffers();
        }

        private void OnResize(object? sender, EventArgs e)
        {
            GL.Viewport(0, 0, _gl.Width, _gl.Height);
        }

        private void SetupVAO()
        {
            float[] verts = { -1, -1, 1, -1, -1, 1, 1, 1 };
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);
        }

        private int CompileShader(ShaderType type, string src)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new Exception($"Shader error: {GL.GetShaderInfoLog(id)}");
            return id;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveReviews();
                _renderTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
