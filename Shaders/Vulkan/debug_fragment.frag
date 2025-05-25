#version 460

layout (location = 0) in vec3 fragColor;
layout (location = 1) in vec3 fragPositionWorld;

layout (location = 0) out vec4 outColor;

#include directional_light
#include point_light

layout (set = 0, binding = 0) #include global_ubo

layout (push_constant) uniform Push {
  mat4 transform;
} push;

void main() {
  outColor = vec4(fragColor, 1.0);
}