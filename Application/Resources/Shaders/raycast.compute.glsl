#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout (binding = 0, rgba32f) restrict uniform image2D _img_result;
layout (binding = 1) uniform sampler3D _voxels;

uniform float _time;

vec2 rotate(vec2 v, float a) {
    float sinA = sin(a);
    float cosA = cos(a);
    return vec2(v.x * cosA - v.y * sinA, v.y * cosA + v.x * sinA);
}

bool voxelHit(ivec3 p) {
    p += ivec3(8);
    bool hit = false;
    if (p.x >= 0 && p.x < 16 && p.y >= 0 && p.y < 16 && p.z >= 0 && p.z < 16) {
        vec3 pos = p / vec3(16.0);
        vec4 col = texture(_voxels, pos);
        if (col.r != 0) hit = true;
    }
    return hit;
}

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 imgSize = imageSize(_img_result);

    // This discards the extra pixels in cases where the image size isn't perfectly divisible by the kernel.xy
    if (imgCoord.x >= imgSize.x || imgCoord.y >= imgSize.y) return;
    
    vec3 rayPos = vec3(0.0, 0.0, -20.0);
    rayPos.xz = rotate(rayPos.xz, _time);
    
    vec3 screenPos = vec3(2.0 * imgCoord.xy / imgSize.xy - 1.0, 0.0);
    vec3 cameraDir = vec3(0.0, 0.0, 1.0);
    vec3 cameraPlane = vec3(1.0, 1.0 * imgSize.y / imgSize.x, 0.0);
    vec3 rayDir = cameraDir + screenPos * cameraPlane;
    rayDir.xz = rotate(rayDir.xz, _time);

    bvec3 mask;
    ivec3 mapPos = ivec3(floor(rayPos));
    vec3 deltaDist = 1.0 / abs(rayDir);
    ivec3 rayStep = ivec3(sign(rayDir));
    vec3 sideDist = (sign(rayDir) * (vec3(mapPos) - rayPos) + (sign(rayDir) * 0.5) + 0.5) * deltaDist;

    bool hit = false;
    const int maxRayDepth = 128;
    for (int i = 0; i < maxRayDepth; i++)
    {
        if (voxelHit(mapPos)) {
            hit = true;
            break;
        }

        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(vec3(mask)) * rayStep;
    }

    if (hit) {
        vec3 sideColor = (vec3(1.0) * (mask.x ? vec3(0.5) : mask.y ? vec3(1.0) : mask.z ? vec3(0.75) : vec3(0.0)));
        vec4 voxelColor = texture(_voxels, vec3((mapPos + ivec3(8)) / vec3(16.0)));
        imageStore(_img_result, imgCoord, vec4(sideColor, 1.0) * voxelColor);
    } else {
        imageStore(_img_result, imgCoord, vec4(vec3(0.0), 1.0));
    }
}