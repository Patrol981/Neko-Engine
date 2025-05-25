#version 460

layout (location = 0) in vec3 fragColor;
layout (location = 1) in vec3 texCoord;

layout (location = 0) out vec4 outColor;

layout (push_constant) uniform Push {
  mat4 transform;
  vec3 color;
} push;

layout (set = 0, binding = 0) uniform samplerCube samplerCubeMap;

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
  outColor = texture(samplerCubeMap, texCoord);
}