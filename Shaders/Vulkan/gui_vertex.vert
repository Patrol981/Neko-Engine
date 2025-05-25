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

layout (push_constant) uniform Push {
  mat4 transform;
} push;

vec2 positions[3] = vec2[](
  vec2(0.0, -0.5),
  vec2(0.5, 0.5),
  vec2(-0.5, 0.5)
);

void main() {
  // vec4 positionWorld = vec4(position.x, position.y, 0.0, 1.0);
  vec4 initPosition = vec4(position.x, position.y, 0.0, 1.0);
  vec4 positionWorld = push.transform * initPosition;
	// gl_Position = globalUBO.projection * globalUBO.view * positionWorld;
  // gl_Position = globalUBO.projection * vec4(position.x, position.y, 0.0, 1.0);
  // gl_Position = globalUBO.projection * push.transform * vec4(position.x, position.y, 0.0, 1.0);

  // git gud
  // gl_Position = globalUBO.projection * vec4(positions[gl_VertexIndex], -1.0, 1.0);
  gl_Position = globalUBO.projection * push.transform * vec4(position.x, -position.y, -1.0, 1.0);

  // gl_Position = push.transform * vec4(position, 1.0);
  // gl_Position.y = -gl_Position.y;
  // gl_Position = vec4(position.x, position.y, 0.0, 1.0);
	texCoord = uv;
	fragColor = vec4(color.xyz, 1.0);
}