#version 460

layout (location = 0) in vec4 fragColor;
layout (location = 1) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

layout (set = 0, binding = 0) uniform sampler2D textureSampler;

void main() {
  // gl_FragColor = v_color * texture2D(TextureSampler, v_texCoords);
  outColor = texture(textureSampler, texCoord) * fragColor;
}