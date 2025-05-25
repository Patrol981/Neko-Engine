#version 460

#include directional_light
#include point_light

layout(location = 0) in vec2 uv;
layout(location = 1) in vec2 position;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _colorSampler;
layout(set = 0, binding = 1) uniform sampler2D _depthSampler;

layout (push_constant) uniform Push {
  vec2 windowSize;
  float depthMin;
  float depthMax;

  float edgeLow;
  float edgeHigh;
  float contrast;
  float stipple;

  vec3 luminance;
} push;

layout(set = 1, binding = 0) #include global_ubo

layout(set = 2, binding = 0) uniform texture2D _hatchTexture1;
layout(set = 2, binding = 1) uniform sampler _hatchSampler1;
layout(set = 3, binding = 0) uniform texture2D _hatchTexture2;
layout(set = 3, binding = 1) uniform sampler _hatchSampler2;
layout(set = 4, binding = 0) uniform texture2D _hatchTexture3;
layout(set = 4, binding = 1) uniform sampler _hatchSampler3;

const float threshold1 = 0.5;
const float threshold2 = 0.6;

float getProjectedDepthFromTexture(vec2 pTexCoords, mat4 pInverseProjection) {
  float depth = texture(_depthSampler, pTexCoords).r;
  vec3 ndc = vec3(pTexCoords * 2.0 - 1.0, depth);
  vec4 view = pInverseProjection * vec4(ndc, 1.0);
  view.xyz /= view.w;
  return -view.z;
}

float edgeDetection(vec2 pTexCoords) {
    float dx = 1.0 / push.windowSize.x;
    float dy = 1.0 / push.windowSize.y;

    float kernel[9] = float[](
        -1, -1, -1,
        -1,  8, -1,
        -1, -1, -1
    );
    //  float kernel[25] = float[](
    //     -1, -1, -1, -1, -1,
    //     -1,  1,  2,  1, -1,
    //     -1,  2,  4,  2, -1,
    //     -1,  1,  2,  1, -1,
    //     -1, -1, -1, -1, -1
    // );

    float edge = 0.0;
    int index = 0;
    for (int y = -2; y <= 2; ++y) {
        for (int x = -2; x <= 2; ++x) {
            vec2 offset = vec2(float(x) * dx, float(y) * dy);
            vec3 sampleValue = texture(_colorSampler, pTexCoords + offset).rgb;
            edge += kernel[index] * length(sampleValue);
            index++;
        }
    }
    return clamp(edge, 0.0, 1.0);
}

vec3 calculateFog(vec3 pColor) {
  if (ubo.useFog != 1 && ubo.hasImportantEntity != 1) return pColor;

  float depth = texture(_depthSampler, uv).r;
  float z = depth * 2.0 - 1.0;

  vec4 frag_position_ndc = vec4(
    (gl_FragCoord.x / ubo.fog.y) * 2.0 - 1.0,
    (gl_FragCoord.y / ubo.fog.z) * 2.0 - 1.0,
    z,
    1.0
  );

  vec4 frag_position_view = inverse(ubo.projection) * frag_position_ndc;
  frag_position_view /= frag_position_view.w;

  vec3 frag_position_world = (inverse(ubo.view) * frag_position_view).xyz;
  frag_position_world.z -= 2.0f;

  float horizontal_distance = distance(frag_position_world.xz, ubo.importantEntityPosition.xz);

  float normalized_distance = horizontal_distance / ubo.fog.x;

  float fog_visibility = exp(-pow(normalized_distance, 2.0));
  fog_visibility = clamp(fog_visibility, 0.0, 1.0);

  return mix(vec3(1.0), pColor, fog_visibility);
}

void main() {
  vec3 screen_color = texture(_colorSampler, uv).rgb;
  vec3 screen_normal = texture(_colorSampler, uv).rgb * 2.0 - 1.0;
  screen_normal = normalize(screen_normal);  // Ensure normalization

  float edge = edgeDetection(uv);
  vec3 edge_result = mix(screen_color, vec3(0.0), edge);

  // float luminance = dot(screen_color, vec3(0.299, 0.587, 0.114));
  float luminance = dot(screen_color, push.luminance);

  float r_channel = screen_color.r;
  float pow_r = pow(r_channel,0.3);
  float inverse_pow_r = 1.0 - pow_r;
  luminance -= inverse_pow_r;
  luminance = 1.0 - luminance;
  vec3 final_color = vec3(luminance);

  vec3 normal_sample = normalize(screen_normal * 2.0 - 1.0);
  vec2 distorted_uv = uv + normal_sample.xy * ubo.hatchScale;
   if (luminance > 0.001) {
    if (luminance < push.edgeLow) {
      vec3 hatch1_color = 1.0 - texture(sampler2D(_hatchTexture1, _hatchSampler1), distorted_uv * 15.0).rgb;
      final_color *= hatch1_color;
    } else if ( luminance < push.edgeHigh) {
      vec3 hatch2_color = 1.0 - texture(sampler2D(_hatchTexture2, _hatchSampler2), distorted_uv * 15.0).rgb;
      final_color *= hatch2_color;
    } else {
      vec3 hatch3_color = 1.0 - texture(sampler2D(_hatchTexture3, _hatchSampler3), distorted_uv * 15.0).rgb;
      final_color *= hatch3_color;
    }
  }

  final_color = mix(vec3(1.0), vec3(0.0), final_color * push.contrast);
  vec3 mix_result = mix(final_color, screen_color, push.stipple);
  outColor = vec4(mix(mix_result, vec3(0.0), edge), 1.0);
  // outColor = vec4(screen_normal * 0.5 + 0.5, 1.0);
  // outColor = vec4(screen_normal, 1.0);
}