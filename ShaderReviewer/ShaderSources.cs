namespace ShaderReviewer
{
    public static class ShaderSources
    {
        public const string VertexShader = @"#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 fragCoord;
uniform vec3 iResolution;

void main() {
    gl_Position = vec4(aPos, 0.0, 1.0);
    fragCoord = (aPos * 0.5 + 0.5) * iResolution.xy;
}";

        public const string DefaultFragmentShader = @"#version 330 core
out vec4 FragColor;
in vec2 fragCoord;
uniform float iTime;
uniform vec3 iResolution;

void mainImage(out vec4 fragColor, in vec2 fc) {
    vec2 uv = fc / iResolution.xy;
    fragColor = vec4(0.2, 0.2, 0.3, 1.0);
}

void main() {
    mainImage(FragColor, fragCoord);
}";
    }
}

