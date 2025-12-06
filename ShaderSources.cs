namespace ScreenShader
{
    internal static class ShaderSources
    {
        public static readonly string VertexShader = @"
#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 fragCoord;

uniform vec3 iResolution;

void main()
{
    // Convert from [-1,1] to screen coordinates
    fragCoord = (aPos * 0.5 + 0.5) * iResolution.xy;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        // Default fallback shader in case no shaders are loaded
        public static readonly string DefaultFragmentShader = @"
#version 330 core
out vec4 FragColor;
in vec2 fragCoord;

uniform float iTime;
uniform vec3 iResolution;
uniform sampler2D iChannel0;

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    vec2 uv = fragCoord / iResolution.xy;
    vec3 col = 0.5 + 0.5 * cos(iTime + uv.xyx + vec3(0, 2, 4));
    fragColor = vec4(col, 1.0);
}

void main() {
    mainImage(FragColor, fragCoord);
}";
    }
}
