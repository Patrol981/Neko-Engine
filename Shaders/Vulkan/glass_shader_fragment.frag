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
#include directional_light
#include point_light
#include fog
#include object_data
#include sobel  // (optional: if you want edge boosting)

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

/* =======================
   GLASS – knobs & wiring
   ======================= */

// REQUIRED: set this to the index where your scene color buffer is bound.
#ifndef SCENE_COLOR_TEX_ID
#define SCENE_COLOR_TEX_ID 0
#endif

// If you already do transparency blending in the pipeline, set 0.
// If you want this shader to pre-composite against the scene buffer and output opaque, set 1.
#ifndef GLASS_PRECOMPOSITE
#define GLASS_PRECOMPOSITE 1
#endif

// If you can provide 1.0/width, 1.0/height from your UBO, use that instead.
#ifndef INV_FRAMEBUFFER_SIZE
const vec2 INV_FRAMEBUFFER_SIZE = vec2(1.0/1920.0, 1.0/1080.0);
#else
const vec2 INV_FRAMEBUFFER_SIZE = INV_FRAMEBUFFER_SIZE;
#endif

// Material defaults (you can pipe these from your ObjectData if you prefer).
const float IOR             = 1.50;   // Index of refraction (glass ~1.5)
const float ROUGHNESS       = 0.08;   // 0 (perfect) … 1 (frosted)
const float REFRACT_STRENGTH= 0.020;  // screen-space refraction scale
const float REFLECT_STRENGTH= 0.015;  // screen-space reflection parallax
const float DISPERSION      = 0.0015; // chromatic aberration strength
const float THICKNESS       = 0.35;   // affects absorption; arbitrary units
const float ABSORPTION_PWR  = 2.0;    // stronger => more color absorption

/* =======================
   Helpers
   ======================= */

vec4 sampleScene(vec2 uv) {
  uv = clamp(uv, 0.0, 1.0);
  return texture(sampler2D(_texture[SCENE_COLOR_TEX_ID], _sampler), uv);
}

vec3 boxBlurScene(vec2 uv, float radiusPx) {
  // 9-tap box blur in screen space
  vec2 stepUV = INV_FRAMEBUFFER_SIZE * radiusPx;
  vec3 c = vec3(0.0);
  c += sampleScene(uv + stepUV * vec2(-1.0, -1.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 0.0, -1.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 1.0, -1.0)).rgb;
  c += sampleScene(uv + stepUV * vec2(-1.0,  0.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 0.0,  0.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 1.0,  0.0)).rgb;
  c += sampleScene(uv + stepUV * vec2(-1.0,  1.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 0.0,  1.0)).rgb;
  c += sampleScene(uv + stepUV * vec2( 1.0,  1.0)).rgb;
  return c / 9.0;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0) {
  return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

/* =======================
   Main
   ======================= */
void main() {
  vec3 N = normalize(fragNormalWorld);
  vec3 V = normalize(ubo.cameraPosition - fragPositionWorld);

  // Use your per-object colors as a glass tint (customize to your packing).
  // Here I use diffuse as tint and specular as metal tint contribution.
  vec3 tint = clamp(objectBuffer.objectData[id].diffuseAndTexId1.xyz, 0.0, 1.0);
  vec3 specTint = clamp(objectBuffer.objectData[id].specularAndShininess.xyz, 0.0, 1.0);

  // Fresnel base reflectance from IOR; mix in your specular tint a bit for flexibility.
  float f0Scalar = pow((IOR - 1.0) / (IOR + 1.0), 2.0);
  vec3  F0 = mix(vec3(f0Scalar), specTint, 0.35);

  // Refract/reflect directions (world space is fine for this screen-space hack).
  vec3 R  = reflect(-V, N);
  vec3 T  = refract(-V, N, 1.0 / IOR);   // incoming in air (n1=1)

  // Convert to screen-space offsets. This is an approximation:
  vec2 uv = screenTexCoord;
  vec2 refrOff = T.xy * REFRACT_STRENGTH;
  vec2 reflOff = R.xy * REFLECT_STRENGTH;

  // Chromatic dispersion for refraction (tiny RGB offsets along T).
  vec3 refrRGB;
  refrRGB.r = sampleScene(uv + refrOff * (1.0 + DISPERSION)).r;
  refrRGB.g = sampleScene(uv + refrOff).g;
  refrRGB.b = sampleScene(uv + refrOff * (1.0 - DISPERSION)).b;

  // Roughness-driven blur (in pixels). Scale as you like.
  float blurPx = mix(0.0, 6.0, clamp(ROUGHNESS, 0.0, 1.0));
  vec3 refraced = mix(refrRGB, boxBlurScene(uv + refrOff, blurPx), step(0.001, blurPx));

  // Screen-space reflection (also blurred by roughness).
  vec3 reflected = mix(
      sampleScene(uv + reflOff).rgb,
      boxBlurScene(uv + reflOff, blurPx * 1.25),
      step(0.001, blurPx)
  );

  // Beer-Lambert-ish absorption using tint (darker tint -> more absorption).
  vec3 absorption = max(vec3(0.0), vec3(1.0) - tint) * ABSORPTION_PWR;
  vec3 transmit = exp(-absorption * THICKNESS);
  refraced *= tint * transmit;

  // Fresnel blend
  float cosTheta = clamp(dot(N, V), 0.0, 1.0);
  vec3  F = fresnelSchlick(cosTheta, F0);
  vec3  color = mix(refraced, reflected, F);

  // (Optional) add analytic specular highlight from your lights, on top of reflection
  // — looks nice for very crisp edges. Keep it subtle.
  {
    vec3 specAdd = vec3(0.0);
    float shininess = max(1.0, objectBuffer.objectData[id].specularAndShininess.w);
    specAdd += calc_dir_light(
      ubo.directionalLight, N, V, shininess,
      /*diffuse*/ vec3(0.0), /*spec*/ vec3(1.0)
    );
    for (int i = 0; i < ubo.pointLightLength; i++) {
      specAdd += calc_point_light(
        pointLightBuffer.pointLights[i], N, V, shininess,
        /*ambient*/ vec3(0.0), /*spec*/ vec3(1.0)
      );
    }
    color += specAdd * 0.08; // very subtle
  }

  // Fog
  vec3 fogged = mix(ubo.fogColor.rgb, color, fogVisiblity);
  color = fogged;

  // Alpha handling (precomposite vs traditional blending)
#if GLASS_PRECOMPOSITE
  float alpha = 1.0;            // we've already composited against the scene buffer
#else
  // For standard blending (enable premultiplied alpha in pipeline: src=ONE, dst=ONE_MINUS_SRC_ALPHA)
  // Make glass mostly transparent except at grazing angles (Fresnel).
  float alpha = clamp(mix(0.05, 0.35, luma(F)), 0.0, 1.0);
#endif

  // Keep your “important entity” rule
  if (ubo.hasImportantEntity == 1 && filterFlag == 1) {
    float radiusHorizontal = 1.0;
    float fragToCamera = distance(fragPositionWorld, ubo.cameraPosition);
    float entityToCamera = distance(ubo.importantEntityPosition, ubo.cameraPosition);
    if (fragToCamera <= entityToCamera && entityToFragDistance < radiusHorizontal) {
      alpha = min(alpha, 0.5);
    }
  }

  outColor = vec4(color, alpha);
}
