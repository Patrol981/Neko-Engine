#version 460

layout(location = 0) in vec2 localPos;

layout(location = 0) out vec4 outColor;

#include directional_light
#include point_light
layout(set = 0, binding = 0) #include global_ubo

layout (push_constant) uniform Push {
  mat4 transform;
  float radius;
} push;

void main() {
  float dist = length(localPos);
  if (dist > push.radius) {
    discard;
  }

  float edgeStart = push.radius * 0.8;
  float edgeEnd   = push.radius;

  float alpha = 1.0 - smoothstep(edgeStart, edgeEnd, dist);
  // float inner = smoothstep(push.radius * 0.2, push.radius * 0.4, dist);
  // float outer = smoothstep(push.radius * 0.8, push.radius, dist);
  // float alpha = (1.0 - outer) * inner;

  vec3 shadowColor = vec3(0.0);
  outColor = vec4(shadowColor, alpha * 0.5);
}