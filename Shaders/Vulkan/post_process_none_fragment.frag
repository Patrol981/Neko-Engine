#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout(set = 1, binding = 0) #include global_ubo

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;

  outColor = vec4(screen_color, 1.0);
}