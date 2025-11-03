#version 460

#extension GL_EXT_samplerless_texture_functions : require

layout(location = 0) in vec3 fragColor;
layout(location = 1) in vec3 fragPositionWorld;
layout(location = 2) in vec3 fragNormalWorld;
layout(location = 3) in vec2 texCoord;
layout(location = 4) in flat int filterFlag;
layout(location = 5) in float entityToFragDistance;
layout(location = 6) in float fogVisiblity;
layout(location = 7) in vec2 screenTexCoord;
layout(location = 8) in flat int id;

layout(location = 0) out vec4 outColor;

#include material
#include skin_data
#include fog
#include directional_light
#include point_light
#include object_data
#include sobel
#include light_calc

const uint MAX_TEXTURES = 128;
layout (set = 0, binding = 0) uniform texture2D _texture[MAX_TEXTURES];
layout (set = 0, binding = 1) uniform sampler _sampler;
layout (set = 1, binding = 0) #include global_ubo
layout(std140, set = 2, binding = 0) readonly buffer ObjectBuffer {
    ObjectData objectData[];
} objectBuffer;
layout(std140, set = 3, binding = 0) readonly buffer PointLightBuffer {
  PointLight pointLights[];
} pointLightBuffer;

// holo specific
const vec4 hologram_color = vec4(0.0, 0.6, 1.0, 1.0);
const float transparency = 0.5;
const float glitch_strength = 0.3;
const float glow_intensity  = 0.9;
const float time_factor = 1.0;
const float noise_amount = 0.05;
const float vertical_shift_speed = 0.1;

float random_noise(vec2 in_uv) {
  return fract(sin(dot(in_uv, vec2(12.9898, 78.233))) * 43758.5453);
}

void main() {
  float time = objectBuffer.objectData[id].diffuseAndTexId1.x;
  vec2 cust_uv = texCoord.xy * screenTexCoord.xy;
  cust_uv.y += time * vertical_shift_speed;
  float noise = random_noise(cust_uv + time * 0.1);
  cust_uv.x += noise * glitch_strength * 0.05;

  float glitch = sin(cust_uv.x * 10.0 + time * time_factor) * glitch_strength;
  cust_uv.x += glitch * 0.05;
  float glow = sin(time * 2.0) * glow_intensity;
  // vec4 color = texture(texture_sampler, cust_uv);
  vec4 color = texture(sampler2D(_texture[int(objectBuffer.objectData[id].diffuseAndTexId1.w)], _sampler), cust_uv).rgba;
  color.rgb += glow * hologram_color.rgb * 0.5;
  color.r += sin(time + cust_uv.y * 3.0) * 0.1 * glitch_strength; 
  color.g += cos(time + cust_uv.x * 2.0) * 0.1 * glitch_strength;
  color.rgb *= hologram_color.rgb;
  color.a *= transparency;
  color.rgb += random_noise(cust_uv) * noise_amount;

  outColor = color;
}

void main_() {
  vec3 surfaceNormal = normalize(fragNormalWorld);
  vec3 viewDir = normalize(ubo.cameraPosition - fragPositionWorld);

  vec3 result = vec3(0,0,0);

  // result += calc_dir_light(
  //   ubo.directionalLight,
  //   surfaceNormal,
  //   viewDir,
  //   objectBuffer.objectData[id].specularAndShininess.w,
  //   objectBuffer.objectData[id].diffuseAndTexId1.xyz,
  //   objectBuffer.objectData[id].specularAndShininess.xyz
  // );
  // for(int i = 0; i < ubo.pointLightLength; i++) {
  //   PointLight light = pointLightBuffer.pointLights[i];
  //   result += calc_point_light(
  //     light,
  //     surfaceNormal,
  //     viewDir,
  //     objectBuffer.objectData[id].specularAndShininess.w,
  //     objectBuffer.objectData[id].ambientAndTexId0.xyz,
  //     objectBuffer.objectData[id].specularAndShininess.xyz
  //   );
  // }

  float alpha = 1.0;

  if (ubo.hasImportantEntity == 1 && filterFlag == 1) {
    float radiusHorizontal = 1.0;

    float fragToCamera = distance(fragPositionWorld, ubo.cameraPosition);
    float entityToCamera = distance(ubo.importantEntityPosition, ubo.cameraPosition);

    if(fragToCamera <= entityToCamera && entityToFragDistance < radiusHorizontal) {
      alpha = 0.5;
    }
  }

  vec4 texColor = texture(sampler2D(_texture[int(objectBuffer.objectData[id].ambientAndTexId0.w)], _sampler), texCoord).rgba;
  vec4 shaderColor = texture(sampler2D(_texture[int(objectBuffer.objectData[id].diffuseAndTexId1.w)], _sampler), texCoord).rgba;
  texColor = mix(texColor, shaderColor, 0.5);

  outColor = texColor;
  outColor = mix(ubo.fogColor, outColor, fogVisiblity);
}