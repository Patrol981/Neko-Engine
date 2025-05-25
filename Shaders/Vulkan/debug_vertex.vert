#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec3 fragPositionWorld;

#include directional_light
#include point_light

layout (set = 0, binding = 0) #include global_ubo

layout (push_constant) uniform Push {
  mat4 transform;
} push;

void main() {
  vec4 positionWorld =  push.transform * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;

  fragPositionWorld = positionWorld.xyz;
  fragColor = color;
}