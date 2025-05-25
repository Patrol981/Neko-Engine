#version 460

const vec2 OFFSETS[6] = vec2[](
  vec2(-1.0, -1.0),
  vec2(-1.0, 1.0),
  vec2(1.0, -1.0),
  vec2(1.0, -1.0),
  vec2(-1.0, 1.0),
  vec2(1.0, 1.0)
);

layout (location = 0) out vec2 fragOffset;

layout (push_constant) uniform Push {
  mat4 transform;
  int guizmoType;
  float colorX;
  float colorY;
  float colorZ;
} push;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

void main() {
  fragOffset = OFFSETS[gl_VertexIndex];
  vec3 cameraRightWorld = {ubo.view[0][0], ubo.view[1][0], ubo.view[2][0]};
  vec3 cameraUpWorld = {ubo.view[0][1], ubo.view[1][1], ubo.view[2][1]};

  vec3 transformWorld = fragOffset.x * cameraRightWorld + fragOffset.y * -cameraUpWorld;
  vec4 positionWorld = push.transform * vec4(transformWorld, 1.0);

  gl_Position = ubo.projection * ubo.view * positionWorld;
}