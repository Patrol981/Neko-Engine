#version 460

layout (location = 0) in vec2 texCoord;
layout (location = 1) in flat int id;
layout (location = 2) in vec2 noiseCoord;

layout (location = 0) out vec4 outColor;

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

const uint MAX_TEXTURES = 128;
layout (set = 2, binding = 0) uniform texture2D _texture[MAX_TEXTURES];
layout (set = 2, binding = 1) uniform sampler _sampler;

const vec2 scale = vec2(1.0);
const float strength = 1.0;
const float fps = 20.0;

// #define offset_multiplier vec2(PI, E)

void main() {
  vec2 cellSize = vec2(1.0) / vec2(spriteBuffer.spriteData[id].spriteSheetData.xy);

  int col = int(spriteBuffer.spriteData[id].spriteSheetData.z) % int(spriteBuffer.spriteData[id].spriteSheetData.x);
  int row = int(spriteBuffer.spriteData[id].spriteSheetData.z) / int(spriteBuffer.spriteData[id].spriteSheetData.x);

  vec2 offset = vec2(col, row) * cellSize;

  vec2 adjustedTexCoord = texCoord;
    if (spriteBuffer.spriteData[id].spriteSheetData.w > 0.0f)
        adjustedTexCoord.x = 1.0 - adjustedTexCoord.x;

  vec2 spriteUV = offset + adjustedTexCoord * cellSize;

  float time = spriteBuffer.spriteData[id].spriteSheetData2.z;
  vec2 noise_offset = vec2(floor(time * fps)) * vec2(3.14, 1.401298E-45);
	// float noise_sample = texture(noise, noise_uv + noise_offset).r * 4.0 * 3.14;
	// vec2 direction = vec2(cos(noise_sample), sin(noise_sample));
	// vec2 squiggle_uv = UV + direction * strength * 0.005;

  vec4 noiseTex = texture(
    sampler2D(_texture[int(spriteBuffer.spriteData[id].spriteSheetData2.w)], _sampler),
    noiseCoord + noise_offset
  );

  float noise_sample = noiseTex.r * 4.0 * 3.14;
  vec2 direction = vec2(cos(noise_sample), sin(noise_sample));
  vec2 squiggle_uv = texCoord + direction * strength * 0.005;

  vec4 spriteTex = texture(
    sampler2D(_texture[int(spriteBuffer.spriteData[id].spriteSheetData2.y)], _sampler),
    spriteUV
  );

  outColor = spriteTex;
  // outColor = vec4(1,1,1,1);
}