#version 460

layout (location = 0) in vec2 fragOffset;

layout (location = 0) out vec4 outColor;

#include directional_light
#include point_light

layout (set = 0, binding = 0) #include global_ubo

void main() {
  float dis = sqrt(dot(fragOffset, fragOffset));
  if(dis > 1.0) discard;
  outColor = vec4(ubo.directionalLight.lightColor.xyz, 1.0);
}