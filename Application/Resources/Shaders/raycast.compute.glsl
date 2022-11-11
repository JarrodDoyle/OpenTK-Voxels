#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout (binding = 0, rgba32f) restrict uniform image2D _img_result;
layout (binding = 1) uniform sampler3D _voxels;

uniform ivec3 _voxelDims;
uniform float _time;

vec4 voxelColor;

vec2 rotate(vec2 v, float a) {
    float sinA = sin(a);
    float cosA = cos(a);
    return vec2(v.x * cosA - v.y * sinA, v.y * cosA + v.x * sinA);
}

bool voxelHit(ivec3 p) {
    p += _voxelDims / 2;
    voxelColor = texelFetch(_voxels, p, 0);
    return voxelColor.r != 0;
}

vec4 castRay(vec3 rayPos, vec3 rayDir) {
    bvec3 mask;
    ivec3 mapPos = ivec3(floor(rayPos));
    vec3 deltaDist = 1.0 / abs(rayDir);
    ivec3 rayStep = ivec3(sign(rayDir));
    vec3 sideDist = (rayStep * (vec3(mapPos) - rayPos) + (rayStep * 0.5) + 0.5) * deltaDist;

    const int maxRayDepth = 2 * _voxelDims.z;
    for (int i = 0; i < maxRayDepth; i++)
    {
        if (voxelHit(mapPos))break;
        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(mask) * rayStep;
    }

    vec3 sideColor = mask.x ? vec3(0.5) : mask.y ? vec3(1.0) : mask.z ? vec3(0.75) : vec3(0.0);
    return vec4(sideColor, 1.0) * voxelColor;
}

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 imgSize = imageSize(_img_result);

    // This discards the extra pixels in cases where the image size isn't perfectly divisible by the kernel.xy
    if (imgCoord.x >= imgSize.x || imgCoord.y >= imgSize.y) return;

    // Construct ray
    vec3 rayPos = vec3(0.0, 0.0, -float(_voxelDims.z + 16));
    rayPos.xz = rotate(rayPos.xz, _time);

    vec2 screenPos = vec2(2.0 * imgCoord.xy / imgSize.xy - 1.0);
    vec2 cameraPlane = vec2(1.0, 1.0 * imgSize.y / imgSize.x);
    vec3 rayDir = vec3(screenPos * cameraPlane, 1.0);
    rayDir.xz = rotate(rayDir.xz, _time);

    // Cast that ray!
    imageStore(_img_result, imgCoord, castRay(rayPos, rayDir));
}