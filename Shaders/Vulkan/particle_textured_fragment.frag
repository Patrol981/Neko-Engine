#version 460

layout (location = 0) in vec2 fragOffset;
layout (location = 1) in vec2 texCoords;

layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  vec4 position;
  vec4 color;
  float scale;
  float rotation;
} push;

#include directional_light

layout(set = 0, binding = 0) uniform texture2D _texture;
layout(set = 0, binding = 1) uniform sampler _sampler;

layout (set = 1, binding = 0) #include global_ubo

void main() {
  outColor = texture(sampler2D(_texture, _sampler), texCoords) * vec4(push.color.xyz, 1.0);
}