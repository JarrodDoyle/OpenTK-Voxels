#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout (binding = 0, rgba32f) restrict uniform image2D _img_result;
layout (binding = 1) uniform sampler3D _voxels;

layout (binding = 0) uniform Camera {
    mat4 projection;
    mat4 view;
    vec3 pos;
} _camera;

uniform vec3 _sunlightDir;
uniform ivec3 _voxelDims;
uniform int _maxRayDepth;
uniform float _time;

struct HitInfo {
    bool hit;
    vec3 pos;
    vec4 color;
    bvec3 mask;
    float d;
};

vec4 voxelColor;

bool rayIntersectAabb(vec3 rayPos, vec3 rayDirInv, out float tmin) {
    vec3 t1 = (vec3(0.0) - rayPos) * rayDirInv;
    vec3 t2 = (vec3(_voxelDims) - rayPos) * rayDirInv;
    vec3 t_min = min(t1, t2);
    vec3 t_max = max(t1, t2);
    tmin = max(max(t_min.x, 0.f), max(t_min.y, t_min.z));
    float tmax = min(t_max.x, min(t_max.y, t_max.z));
    return tmax > tmin;
}

bool pointInsideAabb(ivec3 p) {
    return p.x >= 0 && p.x < _voxelDims.x && p.y >= 0 && p.y < _voxelDims.y && p.z >= 0 && p.z < _voxelDims.z;
}

bool voxelHit(ivec3 p) {
    voxelColor = texelFetch(_voxels, p, 0);
    return voxelColor.r != 0;
}

vec3 voxelNormal(vec3 p) {
    vec3 normal;

    // Sample a cube around the voxel at point p
    // Samples that contain a voxel contribute to a weighted average neighbour offset
    // Essentially the inverse of a normal!
    int samplesize = 5;
    vec3 sampleCenter = vec3(samplesize / 2);
    for (int x = 0; x < samplesize; x++) {
        for (int y = 0; y < samplesize; y++) {
            for (int z = 0; z < samplesize; z++) {
                vec3 offset = vec3(x, y, z) - sampleCenter;
                if (texelFetch(_voxels, ivec3(p + offset), 0).r != 0) {
                    normal += offset;
                }
            }
        }
    }

    return -normalize(normal);
}

vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

bool castRay(vec3 rayPos, vec3 rayDir, out HitInfo hitInfo) {
    hitInfo.hit = false;
    bvec3 mask;

    float tmin;
    if (!rayIntersectAabb(rayPos, 1.0 / rayDir, tmin)) {
        return false;
    }

    if (tmin > 0) {
        rayPos += rayDir * (tmin - 0.0001);
    }
    tmin = max(0.0, tmin);

    ivec3 mapPos = ivec3(floor(rayPos));
    vec3 deltaDist = 1.0 / abs(rayDir);
    ivec3 rayStep = ivec3(sign(rayDir));
    vec3 sideDist = (rayStep * (vec3(mapPos) - rayPos) + (rayStep * 0.5) + 0.5) * deltaDist;
    for (int i = 0; i < _maxRayDepth; i++)
    {
        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(mask) * rayStep;
        if (!pointInsideAabb(mapPos)) {
            break;
        }
        if (voxelHit(mapPos)) {
            hitInfo = HitInfo(true, mapPos + vec3(0.5), voxelColor, mask, tmin + length(vec3(mask) * (sideDist - deltaDist)));
            break;
        }
    }

    return hitInfo.hit;
}

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 imgSize = imageSize(_img_result);

    // This discards the extra pixels in cases where the image size isn't perfectly divisible by the kernel.xy
    if (imgCoord.x >= imgSize.x || imgCoord.y >= imgSize.y) return;

    // Construct ray
    vec2 screenPos = vec2(2.0 * imgCoord.xy / imgSize.xy - 1.0);
    vec4 rayEye = _camera.projection * vec4(screenPos, -1.0, 0.0);
    rayEye.zw = vec2(-1.0, 0.0);
    vec3 rayDir = normalize((_camera.view * rayEye).xyz);
    vec3 rayPos = _camera.pos + _voxelDims / 2;

    // Cast that ray!
    vec4 finalColor;
    HitInfo hitInfo;
    if (castRay(rayPos, rayDir, hitInfo)) {
        finalColor = vec4(hsv2rgb(hitInfo.color.raa), 1.0);

        float fogAmount = clamp(1.0 - exp(-hitInfo.d * 0.0025), 0.0, 1.0);
        float diffuse = clamp(dot(voxelNormal(hitInfo.pos), normalize(_sunlightDir)), 0.0, 1.0);
        float shadow = castRay(hitInfo.pos, _sunlightDir, hitInfo) ? 0.25 : 1.0;

        finalColor.xyz *= max(shadow * diffuse, 0.25);
        finalColor.xyz = mix(finalColor.xyz, vec3(0.5, 0.6, 0.7), fogAmount);
    }

    imageStore(_img_result, imgCoord, finalColor);
}