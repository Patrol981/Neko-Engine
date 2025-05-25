#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec4 fragColor;
layout (location = 1) out vec2 texCoord;

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} globalUBO;

void main() {
  vec4 positionWorld = vec4(position.x, position.y, 0.0, 1.0);
	gl_Position = globalUBO.projection * positionWorld;
	texCoord = uv;
	fragColor = vec4(color.xyz, 1.0);
}