#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec2 uv;

layout (location = 0) out vec3 fragColor;
layout (location = 1) out vec3 texCoord;

layout (push_constant) uniform Push {
  mat4 transform;
  vec3 color;
} push;

layout (set = 1, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} ubo;

void main() {
  vec4 positionWorld = vec4(position, 1.0);
  mat4 viewMat = mat4(mat3(ubo.view));
  gl_Position = ubo.projection * viewMat * vec4(position, 1.0);

  fragColor = color;
  texCoord = position;
}