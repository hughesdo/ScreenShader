using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScreenShader
{
    public class ShaderInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string User { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FragmentSource { get; set; } = "";
    }

    public class ShaderLoader
    {
        private readonly List<ShaderInfo> _shaders = new();
        private int _currentIndex = 0;
        private readonly Random _random = new();

        public int ShaderCount => _shaders.Count;
        public int CurrentIndex => _currentIndex;
        public ShaderInfo? CurrentShader => _shaders.Count > 0 ? _shaders[_currentIndex] : null;

        public void LoadShadersFromDirectory(string directoryPath, string? specificShaderName = null)
        {
            _shaders.Clear();

            if (!Directory.Exists(directoryPath))
            {
                Logger.LogError($"Shader directory not found: {directoryPath}");
                return;
            }

            // If a specific shader is requested, try to find it
            if (!string.IsNullOrEmpty(specificShaderName))
            {
                var files = Directory.GetFiles(directoryPath, "*.txt");
                var matchingFile = files.FirstOrDefault(f =>
                    Path.GetFileName(f).Contains(specificShaderName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                {
                    Logger.Log($"Loading specific shader: {matchingFile}");
                    var shader = LoadShaderFromFile(matchingFile);
                    if (shader != null)
                    {
                        _shaders.Add(shader);
                        Logger.Log($"Loaded specific shader: {shader.Name}");
                        return;
                    }
                }
                Logger.LogWarning($"Specific shader '{specificShaderName}' not found, loading all shaders");
            }

            // Check for reviewed_working.txt first (manually reviewed), then working_shaders.txt (auto-tested)
            var reviewedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reviewed_working.txt");
            var autoTestedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working_shaders.txt");
            HashSet<string>? workingShaders = null;

            if (File.Exists(reviewedPath))
            {
                workingShaders = new HashSet<string>(
                    File.ReadAllLines(reviewedPath)
                        .Where(l => !string.IsNullOrWhiteSpace(l)),
                    StringComparer.OrdinalIgnoreCase);
                Logger.Log($"Found reviewed_working.txt with {workingShaders.Count} manually reviewed shaders");
            }
            else if (File.Exists(autoTestedPath))
            {
                workingShaders = new HashSet<string>(
                    File.ReadAllLines(autoTestedPath)
                        .Where(l => !string.IsNullOrWhiteSpace(l)),
                    StringComparer.OrdinalIgnoreCase);
                Logger.Log($"Found working_shaders.txt with {workingShaders.Count} auto-tested shaders");
            }
            else
            {
                Logger.LogWarning("No shader list found - loading all shaders (use ShaderReviewer or --test first)");
            }

            var allFiles = Directory.GetFiles(directoryPath, "*.txt");
            Logger.Log($"Found {allFiles.Length} total shader files in {directoryPath}");

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);

                // Skip if we have a working list and this shader isn't in it
                if (workingShaders != null && !workingShaders.Contains(fileName))
                    continue;

                try
                {
                    var shader = LoadShaderFromFile(file);
                    if (shader != null)
                    {
                        _shaders.Add(shader);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error loading shader {file}: {ex.Message}");
                }
            }

            // Shuffle the shaders for variety
            ShuffleShaders();

            Logger.Log($"Loaded {_shaders.Count} working shaders from {directoryPath}");
        }

        private ShaderInfo? LoadShaderFromFile(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');

            var shader = new ShaderInfo { FilePath = filePath };

            // Parse header comments
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("// ID:"))
                    shader.Id = trimmed.Substring(6).Trim();
                else if (trimmed.StartsWith("// Name:"))
                    shader.Name = trimmed.Substring(8).Trim();
                else if (trimmed.StartsWith("// User:"))
                    shader.User = trimmed.Substring(8).Trim();
            }

            // Find the shader code after "// ===== PASS 0: Image ====="
            var passIndex = content.IndexOf("// ===== PASS 0: Image =====");
            string shaderCode;
            if (passIndex >= 0)
            {
                shaderCode = content.Substring(passIndex + "// ===== PASS 0: Image =====".Length).Trim();
            }
            else
            {
                // If no pass marker, use everything after the header
                shaderCode = content;
            }

            // Wrap the shader code with GLSL version and uniforms
            shader.FragmentSource = WrapShaderCode(shaderCode);

            if (string.IsNullOrEmpty(shader.Name))
                shader.Name = Path.GetFileNameWithoutExtension(filePath);

            return shader;
        }

        private string WrapShaderCode(string shaderCode)
        {
            return $@"#version 330 core
out vec4 FragColor;
in vec2 fragCoord;

uniform float iTime;
uniform vec3 iResolution;
uniform sampler2D iChannel0;

{shaderCode}

void main() {{
    mainImage(FragColor, fragCoord);
}}";
        }

        public ShaderInfo? GetNextShader()
        {
            if (_shaders.Count == 0) return null;
            _currentIndex = (_currentIndex + 1) % _shaders.Count;
            return _shaders[_currentIndex];
        }

        public ShaderInfo? GetPreviousShader()
        {
            if (_shaders.Count == 0) return null;
            _currentIndex = (_currentIndex - 1 + _shaders.Count) % _shaders.Count;
            return _shaders[_currentIndex];
        }

        public ShaderInfo? GetRandomShader()
        {
            if (_shaders.Count == 0) return null;
            _currentIndex = _random.Next(_shaders.Count);
            return _shaders[_currentIndex];
        }

        public ShaderInfo? GetShaderByIndex(int index)
        {
            if (index < 0 || index >= _shaders.Count) return null;
            _currentIndex = index;
            return _shaders[_currentIndex];
        }

        private void ShuffleShaders()
        {
            int n = _shaders.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (_shaders[k], _shaders[n]) = (_shaders[n], _shaders[k]);
            }
        }

        public IReadOnlyList<ShaderInfo> GetAllShaders() => _shaders.AsReadOnly();
    }
}

