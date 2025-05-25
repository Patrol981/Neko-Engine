#version 460

layout(location = 0) in vec3 position;
layout(location = 1) in vec3 color;
layout(location = 2) in vec3 normal;
layout(location = 3) in vec2 uv;
layout(location = 4) in ivec4 jointIndices;
layout(location = 5) in vec4 jointWeights;

layout(location = 0) out vec3 fragColor;
layout(location = 1) out vec3 fragPositionWorld;
layout(location = 2) out vec3 fragNormalWorld;
layout(location = 3) out vec2 texCoord;
layout(location = 4) flat out int filterFlag;
layout(location = 5) out float entityToFragDistance;
layout(location = 6) out float fogVisiblity;
layout(location = 7) out vec2 screenTexCoord;

#include material

#include directional_light
#include point_light
#include fog
#include object_data

#define MAX_NUM_JOINTS 128
int MAX_JOINT_INFLUENCE = 4;

// set 0 = tex
// set 1 = tex

layout(set = 2, binding = 0) #include global_ubo
layout(std140, set = 3, binding = 0) readonly buffer ObjectBuffer {
    ObjectData objectData[];
} objectBuffer;
layout(set = 4, binding = 0) #include skinned_model_ubo
// set 5 = point lights

layout(std140, set = 6, binding = 0) readonly buffer JointBuffer {
    mat4 jointMatrices[];
} jointBuffer;


vec3 applyBoneTransform(vec4 p) {
    vec4 result = vec4(0.0);
    for (int i = 0; i < 4; ++i) {
        mat4 boneTransform = jointBuffer.jointMatrices[jointIndices[i]];
        result += jointWeights[i] * (boneTransform * vec4(position, 1.0));
        result /= dot(jointWeights, vec4(1.0));
    }
    return result.xyz;
}

void main() {
    int offset = int(objectBuffer.objectData[gl_BaseInstance].jointsBufferOffset.x);
    mat4 skinMat =
        jointWeights.x * jointBuffer.jointMatrices[jointIndices.x + offset] +
            jointWeights.y * jointBuffer.jointMatrices[jointIndices.y + offset] +
            jointWeights.z * jointBuffer.jointMatrices[jointIndices.z + offset] +
            jointWeights.w * jointBuffer.jointMatrices[jointIndices.w + offset];

    vec4 positionWorld =
        objectBuffer.objectData[gl_BaseInstance].transformMatrix *
            objectBuffer.objectData[gl_BaseInstance].nodeMatrix *
            skinMat *
            vec4(position, 1.0);

    vec3 worldPos = positionWorld.xyz / positionWorld.w;
    vec4 clip = ubo.projection * ubo.view * vec4(worldPos, 1.0);
    gl_Position = clip;

    fragNormalWorld = normalize(mat3(objectBuffer.objectData[gl_BaseInstance].normalMatrix) * normal);

    fragPositionWorld = positionWorld.xyz;
    fragColor = color;
    texCoord = uv;
    filterFlag = objectBuffer.objectData[gl_BaseInstance].filterFlag;

    vec3 ndc = clip.xyz / clip.w;
    screenTexCoord = ndc.xy * 0.5 + 0.5;
    // screenTexCoord.y = 1.0 - screenTexCoord.y;

    entityToFragDistance = distance(fragPositionWorld.xz, ubo.cameraPosition.xz);
    float normalizedDistance = entityToFragDistance / ubo.fog.x;
    fogVisiblity = exp(-pow(normalizedDistance, 2.0));
    fogVisiblity = clamp(fogVisiblity, 0.0, 1.0);

    // if(ubo.hasImportantEntity == 1) {
    //   // entityToFragDistance = distance(fragPositionWorld.xz, ubo.importantEntityPosition.xz);
    //   entityToFragDistance = distance(fragPositionWorld.xz, ubo.cameraPosition.xz);

    //   float normalizedDistance = entityToFragDistance / ubo.fog.x;
    //   fogVisiblity = exp(-pow(normalizedDistance, 2.0));
    //   fogVisiblity = clamp(fogVisiblity, 0.0, 1.0);
    // } else {
    //   entityToFragDistance = -1;
    // }

    if(ubo.useFog == 0) {
      fogVisiblity = 1.0;
    }
}
