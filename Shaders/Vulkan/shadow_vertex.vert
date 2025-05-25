#version 460

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec2 uv;
layout(location = 4) in ivec4 jointIndices;
layout(location = 5) in vec4 jointWeights;

layout(location = 0) out vec2 localPos;

#include directional_light
#include point_light
layout(set = 0, binding = 0) #include global_ubo

layout (push_constant) uniform Push {
  mat4 transform;
  float radius;
} push;

void main() {
  localPos = position.xz;

  vec4 positionWorld = push.transform * vec4(position, 1.0);
  gl_Position = ubo.projection * ubo.view * positionWorld;
}