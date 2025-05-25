#version 460

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 texCoord;

layout (location = 0) out vec4 outColor;

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

layout(set = 2, binding = 0) uniform sampler2D textureAtlas; // 1 0
// layout(set = 0, binding = 2) uniform sampler FontSampler; // 0 1

void main() {
  // vec4 sampled = vec4(1.0, 1.0, texture(textureAtlas, texCoord).r);
	// vec4 sampled = vec4(1.0, 1.0, 1.0, texture(textureAtlas, texCoord).r);
	// outColor = vec4(color.xyz, 1.0) * sampled;
  outColor = vec4(color.xyz, 1.0) * texture(textureAtlas, texCoord);
  // outColor = vec4(1.0, 1.0, 0.0, 1.0);
}