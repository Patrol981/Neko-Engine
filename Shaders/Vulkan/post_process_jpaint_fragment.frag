#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout (push_constant) uniform Push {
  float edgeLow;
  float edgeHigh;
  float contrast;
  float dither_levels;

  float luminanceX;
  float luminanceY;
  float luminanceZ;
  float texture_repeat;

  float levels;
  float hue;
  float saturation;
  float blendFactor;

  float shadowTintX;
  float shadowTintY;
  float shadowTintZ;
  float shadowBlend;

  float midtoneTintX;
  float midtoneTintY;
  float midtoneTintZ;
  float midtoneBlend;

  float highlightTintX;
  float highlightTintY;
  float highlightTintZ;
  float highlightBlend;

  float shadowThreshold;
  float highlightThreshold;

} push;

layout(set = 1, binding = 0) #include global_ubo

layout(set = 2, binding = 0) uniform texture2D _hatchTexture1;
layout(set = 2, binding = 1) uniform sampler _hatchSampler1;
layout(set = 3, binding = 0) uniform texture2D _hatchTexture2;
layout(set = 3, binding = 1) uniform sampler _hatchSampler2;
layout(set = 4, binding = 0) uniform texture2D _hatchTexture3;
layout(set = 4, binding = 1) uniform sampler _hatchSampler3;

const mat3 kx = mat3(
	vec3(-1, 0, 1),
	vec3(-2, 0, 2),
	vec3(-1, 0, 1)
);
// y direction kernel
const mat3 ky = mat3(
	vec3(-1, -2, -1),
	vec3(0, 0, 0),
	vec3(1, 2, 1)
);

float detectEdgeSobel(sampler2D pTexture, vec2 pUv, vec2 pTexelSize) {
  float Gx[9] = float[](
    -1.0,  0.0,  1.0,
    -2.0,  0.0,  2.0,
    -1.0,  0.0,  1.0
  );

  float Gy[9] = float[](
    -1.0, -2.0, -1.0,
    0.0,  0.0,  0.0,
    1.0,  2.0,  1.0
  );

  vec3 smp[9];

  for(int i = 0; i < 3; i++) {
    for(int j = 0; j < 3; j++) {
      vec2 offset = vec2(float(i - 1), float(j - 1)) * pTexelSize;
      smp[i * 3 + j] = texture(pTexture, uv + offset).rgb;
    }
  }

  float edgeX = 0.0;
  float edgeY = 0.0;

  for(int i = 0; i < 9; i++) {
    // float intensity = dot(smp[i], vec3(0.299, 0.587, 0.114)); // Convert to grayscale
    float intensity = dot(smp[i], vec3(1, 1, 1));
    edgeX += intensity * Gx[i];
    edgeY += intensity * Gy[i];
  }

  return length(vec2(edgeX, edgeY));
}

vec3 dither(vec2 pUv, vec3 pColor, sampler2D pScreenTexture) {
  ivec2 texSize = textureSize(pScreenTexture, 0);
  ivec2 pixel = ivec2(pUv * texSize);

  vec3 quantizedColor = round(pColor * (push.dither_levels - 1.0)) / (push.dither_levels - 1.0);

  vec3 quantError = pColor - quantizedColor;

  if (pixel.x + 1 < texSize.x) {
    vec3 right = texture(pScreenTexture, (pixel + ivec2(1, 0)) / vec2(texSize)).rgb;
    right += quantError * (7.0 / 16.0);
    right = clamp(right, 0.0, 1.0);
  }

  if (pixel.y + 1 < texSize.y) {
    if (pixel.x > 0) {
      vec3 bottomLeft = texture(pScreenTexture, (pixel + ivec2(-1, 1)) / vec2(texSize)).rgb;
      bottomLeft += quantError * (3.0 / 16.0);
      bottomLeft = clamp(bottomLeft, 0.0, 1.0);
    }

    vec3 bottom = texture(pScreenTexture, (pixel + ivec2(0, 1)) / vec2(texSize)).rgb;
    bottom += quantError * (5.0 / 16.0);
    bottom = clamp(bottom, 0.0, 1.0);

    if (pixel.x + 1 < texSize.x) {
      vec3 bottomRight = texture(pScreenTexture, (pixel + ivec2(1, 1)) / vec2(texSize)).rgb;
      bottomRight += quantError * (1.0 / 16.0);
      bottomRight = clamp(bottomRight, 0.0, 1.0);
    }
  }

  return quantizedColor;
}

vec3 posterize(vec3 color, float levels) {
    return floor(color * levels) / levels;
}

vec3 adjustSaturation(vec3 color, float intensity) {
    float grayscale = dot(color, vec3(0.3, 0.59, 0.11));
    return mix(vec3(grayscale), color, intensity);
}

vec3 applyPaperTexture(vec3 pColor, vec2 pUv) {
    float noise = texture(sampler2D(_hatchTexture1, _hatchSampler1), pUv * push.texture_repeat).r; // Scale to repeat texture
    return pColor * (0.9 + 0.1 * noise); // Subtle variation
}

float grainNoise(vec2 pUv) {
    return fract(sin(dot(pUv, vec2(12.9898, 78.233))) * 43758.5453);
}

float rgbToGrayscale(vec3 color) {
    return dot(color, vec3(0.2126, 0.7152, 0.0722)); // Standard luminance coefficients
}

float getLuminance(vec3 c) {
  return dot(c, vec3(push.luminanceX, push.luminanceY, push.luminanceZ));
}

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;
  vec3 screen_normal = texture(_colorSampler, uv).rgb * 2.0 - 1.0;
  screen_normal = normalize(screen_normal);

  float gray = rgbToGrayscale(screen_color);
  vec3 redTintedColor = mix(vec3(gray), screen_color, 0.7);
  redTintedColor = vec3(redTintedColor.r * 1.2, redTintedColor.g * 0.4, redTintedColor.b * 0.3);

  redTintedColor = (redTintedColor - 0.5) * push.contrast + 0.5;

  // vec3 hs = rgb2hs(screen_color);
  // hs.x = fract(hs.x + push.hue);
  // hs.y *= push.saturation;
  // vec3 shifted = hs2rgb(hs);
  // vec3 hueResult = mix(screen_color, shifted, push.blendFactor);

  vec2 texelSize = vec2(1.0 / ubo.screenSize.x, 1.0 / ubo.screenSize.y);
  float edge = detectEdgeSobel(_depthSampler, uv, texelSize) * push.edgeLow;
  edge = pow(edge, 0.6);
  // vec3 mix_result = mix(hueResult, vec3(0.0), edge);
  vec3 posterized_color = posterize(screen_color, push.dither_levels);
  posterized_color = adjustSaturation(posterized_color, push.levels);

  vec3 dithered_color = dither(uv, posterized_color, _colorSampler);
  dithered_color *= (0.95 + 0.05 * grainNoise(uv * push.texture_repeat));
  // vec3 final_color = applyPaperTexture(dithered_color, uv);
  vec3 final_color = mix(dithered_color, redTintedColor, push.blendFactor);
  final_color = mix(final_color, vec3(0.0), edge);

  vec2 halfUv = uv - 0.5;
  float dist = length(halfUv);

  float vignetteStart    = 0.4; // radius where vignette begins
  float vignetteEnd      = 0.7; // radius where vignette is fully applied
  float vignetteStrength = 0.5; // how strongly to tint
  vec3  vignetteColor    = vec3(0.3, 0.0, 0.0); // a dark, reddish color

  float vignetteFactor = smoothstep(vignetteStart, vignetteEnd, dist);
  final_color = mix(final_color, mix(final_color, vignetteColor, vignetteStrength), vignetteFactor);

  outColor = vec4(final_color, 1.0);
}