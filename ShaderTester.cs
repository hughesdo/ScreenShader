using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.GLControl;

namespace ScreenShader
{
    public class ShaderTester
    {
        public List<string> WorkingShaders { get; } = new();
        public List<string> FailedShaders { get; } = new();
        public Dictionary<string, string> FailureReasons { get; } = new();

        private int _vertexShader;

        public void TestAllShaders(string shaderDirectory)
        {
            Console.WriteLine($"Testing shaders in: {shaderDirectory}");

            // Create a hidden form with GLControl for OpenGL context
            using var form = new Form { Visible = false, Width = 100, Height = 100 };
            var settings = new GLControlSettings { Profile = OpenTK.Windowing.Common.ContextProfile.Core };
            using var gl = new GLControl(settings) { Dock = DockStyle.Fill };
            form.Controls.Add(gl);
            form.Show();
            form.Hide();

            // Force context creation
            gl.MakeCurrent();

            Console.WriteLine($"OpenGL: {GL.GetString(StringName.Version)}");

            // Compile vertex shader once
            _vertexShader = CompileShader(ShaderType.VertexShader, ShaderSources.VertexShader);

            var files = Directory.GetFiles(shaderDirectory, "*.txt");
            Console.WriteLine($"Found {files.Length} shader files");
            Console.WriteLine();

            int count = 0;
            foreach (var file in files)
            {
                count++;
                var fileName = Path.GetFileName(file);
                Console.Write($"[{count}/{files.Length}] {fileName,-60} ");

                try
                {
                    var result = TestShader(file);
                    if (result.success)
                    {
                        WorkingShaders.Add(fileName);
                        Console.WriteLine("✓ OK");
                    }
                    else
                    {
                        FailedShaders.Add(fileName);
                        FailureReasons[fileName] = result.error;
                        Console.WriteLine($"✗ FAIL");
                    }
                }
                catch (Exception ex)
                {
                    FailedShaders.Add(fileName);
                    FailureReasons[fileName] = ex.Message;
                    Console.WriteLine($"✗ ERROR: {ex.Message}");
                }
            }

            GL.DeleteShader(_vertexShader);

            Console.WriteLine();
            Console.WriteLine("=" .PadRight(70, '='));
            Console.WriteLine($"RESULTS: {WorkingShaders.Count} working, {FailedShaders.Count} failed");
            Console.WriteLine("=" .PadRight(70, '='));
        }

        private (bool success, string error) TestShader(string filePath)
        {
            var content = File.ReadAllText(filePath);

            // Find shader code after pass marker
            var passIndex = content.IndexOf("// ===== PASS 0: Image =====");
            string shaderCode = passIndex >= 0
                ? content.Substring(passIndex + "// ===== PASS 0: Image =====".Length).Trim()
                : content;

            // Wrap with GLSL boilerplate
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

            // Try to compile fragment shader
            int fsId = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fsId, fragmentSource);
            GL.CompileShader(fsId);
            GL.GetShader(fsId, ShaderParameter.CompileStatus, out int ok);

            if (ok == 0)
            {
                string error = GL.GetShaderInfoLog(fsId);
                GL.DeleteShader(fsId);
                return (false, error);
            }

            // Try to link program
            int program = GL.CreateProgram();
            GL.AttachShader(program, _vertexShader);
            GL.AttachShader(program, fsId);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);

            GL.DetachShader(program, fsId);
            GL.DeleteShader(fsId);

            if (linked == 0)
            {
                string error = GL.GetProgramInfoLog(program);
                GL.DeleteProgram(program);
                return (false, error);
            }

            GL.DeleteProgram(program);
            return (true, "");
        }

        private static int CompileShader(ShaderType type, string src)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new Exception($"Shader error: {GL.GetShaderInfoLog(id)}");
            return id;
        }

        public void SaveResults(string workingFile, string failedFile)
        {
            File.WriteAllLines(workingFile, WorkingShaders);
            Console.WriteLine($"Saved {WorkingShaders.Count} working shaders to: {workingFile}");

            var failedLines = new List<string>();
            foreach (var f in FailedShaders)
                failedLines.Add($"{f}: {FailureReasons.GetValueOrDefault(f, "Unknown")}");
            File.WriteAllLines(failedFile, failedLines);
            Console.WriteLine($"Saved {FailedShaders.Count} failed shaders to: {failedFile}");
        }
    }
}

