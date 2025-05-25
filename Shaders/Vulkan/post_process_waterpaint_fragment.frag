#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout(set = 1, binding = 0) #include global_ubo

layout(set = 2, binding = 0) uniform texture2D _inputTexture1;
layout(set = 2, binding = 1) uniform sampler _inputSampler1;
layout(set = 3, binding = 0) uniform texture2D _inputTexture2;
layout(set = 3, binding = 1) uniform sampler _inputSampler2;
layout(set = 4, binding = 0) uniform texture2D _inputTexture3;
layout(set = 4, binding = 1) uniform sampler _inputSampler3;

layout (push_constant) uniform Push {
  float edgeLow;
  float edgeHigh;
} push;

const vec3 _outlineColor = vec3(0.0);
const float _outlineStrength = 10.0;

float depthAt(vec2 in_uv) {
  return texture(_depthSampler, in_uv).r;
}

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;

  float dC = depthAt(uv);
  float dU = depthAt(uv + vec2(0.0, -1.0 / ubo.screenSize.y)); // tweak for your resolution
  float dD = depthAt(uv + vec2(0.0,  1.0 / ubo.screenSize.y));
  float dL = depthAt(uv + vec2(-1.0 / ubo.screenSize.x, 0.0));
  float dR = depthAt(uv + vec2( 1.0 / ubo.screenSize.x, 0.0));

  float dx = dR - dL;
  float dy = dD - dU;
  float edgeVal = sqrt(dx*dx + dy*dy);

  float outlineFactor = smoothstep(push.edgeLow, push.edgeHigh, edgeVal);

  outlineFactor *= _outlineStrength;

  vec3 edgeColor = mix(vec3(0.0), _outlineColor, outlineFactor);

  vec3 final_color = screen_color + edgeColor * outlineFactor;

  outColor = vec4(final_color, 1.0);
}