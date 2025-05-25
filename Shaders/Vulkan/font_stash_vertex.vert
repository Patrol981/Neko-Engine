#version 460

layout (location = 0) in vec3 position;
layout (location = 1) in vec4 color;
layout (location = 2) in vec2 uv;

layout (location = 0) out vec4 fragColor;
layout (location = 1) out vec2 texCoord;

layout (push_constant) uniform Push {
  mat4 transform;
} push;

void main() {
  fragColor = color;
  texCoord = uv;
  gl_Position = push.transform * vec4(position, 1.0);
}