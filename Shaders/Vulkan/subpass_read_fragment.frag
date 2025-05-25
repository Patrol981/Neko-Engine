#version 460

#include directional_light
#include point_light

layout(input_attachment_index = 0, binding = 0) uniform subpassInput inputColor;
layout(input_attachment_index = 1, binding = 1) uniform subpassInput inputDepth;

layout(location = 0) in vec2 uv;

layout(location = 0) out vec4 outColor;

layout(set = 1, binding = 0) #include global_ubo

void main() {
  vec3 color = subpassLoad(inputColor).rgb;

  outColor = vec4(color, 1.0);
}