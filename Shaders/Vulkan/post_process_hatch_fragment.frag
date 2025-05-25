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
  float wiggleFrequency;
  float wiggleAmplitude;
  float stipple;
} push;

layout(set = 1, binding = 0) #include global_ubo

layout(set = 2, binding = 0) uniform texture2D _hatchTexture1;
layout(set = 2, binding = 1) uniform sampler _hatchSampler1;
layout(set = 3, binding = 0) uniform texture2D _hatchTexture2;
layout(set = 3, binding = 1) uniform sampler _hatchSampler2;
layout(set = 4, binding = 0) uniform texture2D _hatchTexture3;
layout(set = 4, binding = 1) uniform sampler _hatchSampler3;

const float zNear = 0.05;
const float zFar = 100;
const float outlineThickness = 1.5;
const vec3 outlineColor = vec3(0.0);
// const float push.wiggleFrequency = 0.08;
// const float push.wiggleAmplitude = 2.0;

const mat3 Sy = mat3(
	vec3(1.0, 0.0, -1.0),
	vec3(2.0, 0.0, -2.0),
	vec3(1.0, 0.0, -1.0)
);

const mat3 Sx = mat3(
	vec3(1.0, 2.0, 1.0),
	vec3(0.0, 0.0, 0.0),
	vec3(-1.0, -2.0, -1.0)
);

float depth(sampler2D depth_texture, vec2 screen_uv,  mat4 inv_projection_matrix){
	float raw_depth = texture(depth_texture, screen_uv)[0];
	vec3 ndc = vec3(screen_uv * 2.0 - 1.0, raw_depth);
    vec4 view_space = inv_projection_matrix * vec4(ndc, 1.0);
	view_space.xyz /= view_space.w;
	float linear_depth = view_space.z;
	float scaled_depth = (zFar-zNear)/(zNear  + linear_depth*(zNear -zFar));
	return scaled_depth;
}

float sobel_depth(in vec2 uv,  in vec2 offset,  mat4 inv_projection_matrix) {
	float d00 = depth(_depthSampler, uv + offset * vec2(-1,-1),inv_projection_matrix);
	float d01 = depth(_depthSampler, uv + offset * vec2(-1, 0),inv_projection_matrix);
	float d02 = depth(_depthSampler, uv + offset * vec2(-1, 1),inv_projection_matrix);

	float d10 = depth(_depthSampler, uv + offset * vec2( 0,-1),inv_projection_matrix);
	float d11 = depth(_depthSampler, uv + offset * vec2( 0, 0),inv_projection_matrix);
	float d12 = depth(_depthSampler, uv + offset * vec2( 0, 1),inv_projection_matrix);

	float d20 = depth(_depthSampler, uv + offset * vec2( 1,-1),inv_projection_matrix);
	float d21 = depth(_depthSampler, uv + offset * vec2( 1, 0),inv_projection_matrix);
	float d22 = depth(_depthSampler, uv + offset * vec2( 1, 1),inv_projection_matrix);

	float xSobelDepth =
	Sx[0][0] * d00 + Sx[1][0] * d10 + Sx[2][0] * d20 +
	Sx[0][1] * d01 + Sx[1][1] * d11 + Sx[2][1] * d21 +
	Sx[0][2] * d02 + Sx[1][2] * d12 + Sx[2][2] * d22;

	float ySobelDepth =
	Sy[0][0] * d00 + Sy[1][0] * d10 + Sy[2][0] * d20 +
	Sy[0][1] * d01 + Sy[1][1] * d11 + Sy[2][1] * d21 +
	Sy[0][2] * d02 + Sy[1][2] * d12 + Sy[2][2] * d22;
	return  sqrt(pow(xSobelDepth, 2.0) + pow(ySobelDepth, 2.0));
}

float luminance(vec3 color) {
	const vec3 magic = vec3(0.2125, 0.7154, 0.0721);
	return dot(magic, color);
}

// Compute edges detection from normals using x,y sobel filters
float sobel_normal(in vec2 uv,  in vec2 offset) {
	float normal00 = luminance(texture(_colorSampler, uv + offset * vec2(-1,-1)).rgb);
	float normal01 = luminance(texture(_colorSampler, uv + offset * vec2(-1, 0)).rgb);
	float normal02 = luminance(texture(_colorSampler, uv + offset * vec2(-1, 1)).rgb);

	float normal10 = luminance(texture(_colorSampler, uv + offset * vec2( 0,-1)).rgb);
	float normal11 = luminance(texture(_colorSampler, uv + offset * vec2( 0, 0)).rgb);
	float normal12 = luminance(texture(_colorSampler, uv + offset * vec2( 0, 1)).rgb);

	float normal20 = luminance(texture(_colorSampler, uv + offset * vec2( 1,-1)).rgb);
	float normal21 = luminance(texture(_colorSampler, uv + offset * vec2( 1, 0)).rgb);
	float normal22 = luminance(texture(_colorSampler, uv + offset * vec2( 1, 1)).rgb);

	float xSobelNormal =
	Sx[0][0] * normal00 + Sx[1][0] * normal10 + Sx[2][0] * normal20 +
	Sx[0][1] * normal01 + Sx[1][1] * normal11 + Sx[2][1] * normal21 +
	Sx[0][2] * normal02 + Sx[1][2] * normal12 + Sx[2][2] * normal22;

	float ySobelNormal =
	Sy[0][0] * normal00 + Sy[1][0] * normal10 + Sy[2][0] * normal20 +
	Sy[0][1] * normal01 + Sy[1][1] * normal11 + Sy[2][1] * normal21 +
	Sy[0][2] * normal02 + Sy[1][2] * normal12 + Sy[2][2] * normal22;
	return  sqrt(pow(xSobelNormal, 2.0) + pow(ySobelNormal, 2.0));
}

float hash(vec2 p){
	vec3 p3  = fract(vec3(p.xyx) * .1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void main() {
  vec2 offset = outlineThickness / push.windowSize;

	// vec2 displ =  vec2((hash(texture(_colorSampler, uv).xy) * sin(texture(_colorSampler, uv).y * push.wiggleFrequency)) ,
	// (hash(texture(_colorSampler, uv).xy) * cos(texture(_colorSampler, uv).x * push.wiggleFrequency))) * push.wiggleAmplitude / push.windowSize;

  vec2 displ =  vec2((hash(position.xy) * sin(position.y * push.wiggleFrequency)) ,
	(hash(position.xy) * cos(position.x * push.wiggleFrequency))) * push.wiggleAmplitude / push.windowSize;

  // Access the depth buffer
	float depth = depth(_depthSampler, uv, inverse(ubo.projection));

  if(depth<0.01){
		discard ;
	}

  vec3 pixelColor = texture(_colorSampler, uv).rgb;
	float pixelLuma = luminance(pixelColor);
	float modVal = 11.0;

  if(pixelLuma <= 0.35) {
		if (mod((uv.y + displ.y) * push.windowSize.y , modVal)  < outlineThickness) {
			pixelColor = outlineColor;
		}
	}

  if (pixelLuma <= 0.45 ) {
		 if (mod((uv.x + displ.x) * push.windowSize.x , modVal)  < outlineThickness) {
			pixelColor = mix(pixelColor, outlineColor, 0.25);
		}
	}

  if (pixelLuma <= 0.80) {
     if (mod((uv.x + displ.x) * push.windowSize.y + (uv.y + displ.y) * push.windowSize.x, modVal) <= outlineThickness) {
			pixelColor = mix(pixelColor, outlineColor, 0.5);
		}
	}

  // Edge detection using depth buffer
	float edgeDepth = sobel_depth(uv+displ, offset, inverse(ubo.projection));
	// Edge detection normal buffer
	float edgeNormal = sobel_normal(uv+displ, offset);
	// Mix both edge detection
	float outline = smoothstep(0.0,1.0, 25.0*edgeDepth + edgeNormal);
  vec3 result = mix(pixelColor, outlineColor, outline);
  // result = mix(vec3(1.0), vec3(0.0), result * push.stipple);
  vec3 screen_color = texture(_colorSampler, uv).rgb;
  // vec3 mix_result = mix(result, screen_color, ubo.hatchScale);

  // fade
  vec2 center = vec2(0.5, 0.5);
  float dist = length(uv - center);
  float fadeFactor = smoothstep(0.2, 1.0, dist * 1.2);
  // mix_result = mix(mix_result, vec3(0.0), fadeFactor);
  // vec3 faded_color = mix(mix_result, vec3(1.0), push.stipple);

	// Mix color and edges
	outColor = vec4(result, 1.0);
}