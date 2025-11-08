#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec3 color;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec2 uv;

layout (location = 0) out vec2 texCoord;
layout (location = 1) out flat int id;
layout (location = 2) out vec2 noiseCoord;

struct SpriteData {
  mat4 transform;
  vec4 spriteSheetData;
  vec4 spriteSheetData2;
};

layout (set = 0, binding = 0) uniform GlobalUbo {
  mat4 view;
  mat4 projection;
  vec3 lightPosition;
  vec4 lightColor;
  vec4 ambientLightColor;
  vec3 cameraPosition;
  int layer;
} globalUBO;

layout (std140, set = 1, binding = 0) readonly buffer SpriteBuffer {
  SpriteData spriteData[];
} spriteBuffer;


void main() {
  texCoord = uv;
  noiseCoord = (position.xy - spriteBuffer.spriteData[id].transform[3].xy);
  id = gl_BaseInstance;
  vec4 positionWorld = spriteBuffer.spriteData[id].transform * vec4(position, 1.0);
  gl_Position = globalUBO.projection * globalUBO.view * positionWorld;
}