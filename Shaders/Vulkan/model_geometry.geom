#version 460

// Set this to 1 if your projection produces Vulkan-style depth (z in [0,w])
// Set to 0 if your projection is legacy OpenGL-style (z in [-w, w])
#define CLIP_Z_ZERO_TO_ONE 1

layout(triangles) in;
layout(triangle_strip, max_vertices = 3) out;

// ===== VS -> GS inputs (arrays) =====
layout(location = 0) in vec3  in_fragColor[];
layout(location = 1) in vec3  in_fragPositionWorld[];
layout(location = 2) in vec3  in_fragNormalWorld[];
layout(location = 3) in vec2  in_texCoord[];
layout(location = 4) flat in int   in_filterFlag[];
layout(location = 5) in float in_entityToFragDistance[];
layout(location = 6) in float in_fogVisiblity[];
layout(location = 7) in vec2  in_screenTexCoord[];
layout(location = 8) flat in int   in_id[];

// ===== GS -> FS outputs =====
layout(location = 0) out vec3  fragColor;
layout(location = 1) out vec3  fragPositionWorld;
layout(location = 2) out vec3  fragNormalWorld;
layout(location = 3) out vec2  texCoord;
layout(location = 4) flat out int   filterFlag;
layout(location = 5) out float entityToFragDistance;
layout(location = 6) out float fogVisiblity;
layout(location = 7) out vec2  screenTexCoord;
layout(location = 8) flat out int   id;

// Return true if triangle ABC is wholly outside ANY plane.
// Adds epsilon, scaled by |w|, to avoid over-culling near the planes.
bool outside_all(vec4 a, vec4 b, vec4 c) {
    float maxAbsW = max(max(abs(a.w), abs(b.w)), abs(c.w));
    float eps = 1e-5 * max(1.0, maxAbsW);

    // If any vertex has w <= 0, let the fixed clipper handle it (don’t GS-cull)
    if (a.w <= 0.0 || b.w <= 0.0 || c.w <= 0.0)
        return false;

    // X/Y planes
    bool left   = (a.x < -a.w - eps) && (b.x < -b.w - eps) && (c.x < -c.w - eps);
    bool right  = (a.x >  a.w + eps) && (b.x >  b.w + eps) && (c.x >  c.w + eps);
    bool bottom = (a.y < -a.w - eps) && (b.y < -b.w - eps) && (c.y < -c.w - eps);
    bool top    = (a.y >  a.w + eps) && (b.y >  b.w + eps) && (c.y >  c.w + eps);

#if CLIP_Z_ZERO_TO_ONE
    // Vulkan depth convention
    bool nearP = (a.z < 0.0  - eps) && (b.z < 0.0  - eps) && (c.z < 0.0  - eps);
    bool farP  = (a.z >  a.w + eps) && (b.z >  b.w + eps) && (c.z >  c.w + eps); // <-- fixed c.w
#else
    // OpenGL depth convention
    bool nearP = (a.z < -a.w - eps) && (b.z < -b.w - eps) && (c.z < -c.w - eps);
    bool farP  = (a.z >  a.w + eps) && (b.z >  b.w + eps) && (c.z >  c.w + eps); // <-- fixed c.w
#endif

    return left || right || bottom || top || nearP || farP;
}

void main() {
    vec4 A = gl_in[0].gl_Position;
    vec4 B = gl_in[1].gl_Position;
    vec4 C = gl_in[2].gl_Position;

    if (outside_all(A, B, C)) {
        return; // culled
    }

    // Pass-through
    for (int i = 0; i < 3; ++i) {
        gl_Position          = gl_in[i].gl_Position;
        fragColor            = in_fragColor[i];
        fragPositionWorld    = in_fragPositionWorld[i];
        fragNormalWorld      = in_fragNormalWorld[i];
        texCoord             = in_texCoord[i];
        filterFlag           = in_filterFlag[i];
        entityToFragDistance = in_entityToFragDistance[i];
        fogVisiblity         = in_fogVisiblity[i];
        screenTexCoord       = in_screenTexCoord[i];
        id                   = in_id[i];
        EmitVertex();
    }
    EndPrimitive();
}