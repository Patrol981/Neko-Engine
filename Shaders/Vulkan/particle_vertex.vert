#version 460

const vec2 OFFSETS[6] = vec2[](
  vec2(-1.0, -1.0),
  vec2(-1.0, 1.0),
  vec2(1.0, -1.0),
  vec2(1.0, -1.0),
  vec2(-1.0, 1.0),
  vec2(1.0, 1.0)
);

layout (push_constant) uniform Push {
  vec4 position;
  vec4 color;
  float scale;
  float rotation;
} push;

layout (location = 0) out vec2 fragOffset;

#include directional_light
layout (set = 0, binding = 0) #include global_ubo

void main() {
  float cosTheta = cos(push.rotation);
  float sinTheta = sin(push.rotation);
  fragOffset = vec2(
    OFFSETS[gl_VertexIndex].x * cosTheta - OFFSETS[gl_VertexIndex].y * sinTheta,
    OFFSETS[gl_VertexIndex].x * sinTheta + OFFSETS[gl_VertexIndex].y * cosTheta
  );

  vec3 cameraRightWorld = {ubo.view[0][0], ubo.view[1][0], ubo.view[2][0]};
  vec3 cameraUpWorld = {ubo.view[0][1], ubo.view[1][1], ubo.view[2][1]};

  vec3 positionWorld = push.position.xyz
    + push.scale * fragOffset.x * cameraRightWorld
    + push.scale * fragOffset.y * -cameraUpWorld;

  gl_Position = ubo.projection * ubo.view * vec4(positionWorld, 1.0);
}