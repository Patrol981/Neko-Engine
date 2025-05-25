#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout(set = 1, binding = 0) #include global_ubo

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

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;
  vec3 screen_normal = texture(_colorSampler, uv).rgb * 2.0 - 1.0;
  screen_normal = normalize(screen_normal);

  vec2 texelSize = vec2(1.0 / ubo.screenSize.x, 1.0 / ubo.screenSize.y);
  float edge = detectEdgeSobel(_depthSampler, uv, texelSize);
  edge = pow(edge, 0.6);
  vec3 final_color = mix(screen_color, vec3(0.0), edge);

  outColor = vec4(final_color, 1.0);
}