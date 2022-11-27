#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

struct Chunk {
    uint voxels[512 / 32]; // Each uint in this array is 32 packed single-bit voxels
};

layout (binding = 0, rgba32f) restrict uniform image2D _DrawTexture;

layout (std430, binding = 0) buffer WorldConfig {
    ivec3 worldSize;
    int maxRayDepth;
};

layout (std430, binding = 1) buffer World {
    Chunk chunks[];
};

layout (std430, binding = 2) buffer Indices {
    uint chunkIndices[];
};

layout (std430, binding = 3) buffer LoadQueue{
    uint loadQueueCount;
    uint loadQueueMaxCount;
    uint p1;
    uint p2;
    ivec4 loadQueue[];
};

layout (binding = 0) uniform Camera {
    mat4 iProj;
    mat4 iView;
    vec3 pos;
} _Camera;

struct HitInfo {
    bool hit;
    vec3 pos;
    vec4 color;
    bvec3 mask;
    float d;
};

int currentRayDepth = 0;

vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void BuildRayFromCamera(vec2 ndc, out vec3 rayPos, out vec3 rayDir) {
    vec4 rayEye = _Camera.iProj * vec4(ndc, -1.0, 0.0);
    rayEye.zw = vec2(-1.0, 0.0);
    rayDir = vec3(normalize((_Camera.iView * rayEye).xyz));
    rayPos = _Camera.pos;
}

bool RayIntersectAabb(vec3 rayPos, vec3 rayDirInv, out float tmin) {
    vec3 t1 = (vec3(0.0) - rayPos) * rayDirInv;
    vec3 t2 = (vec3(worldSize) * 8.0 - rayPos) * rayDirInv;
    vec3 t_min = min(t1, t2);
    vec3 t_max = max(t1, t2);
    tmin = max(max(t_min.x, 0.f), max(t_min.y, t_min.z));
    float tmax = min(t_max.x, min(t_max.y, t_max.z));
    return tmax > tmin;
}

bool PointInsideAabb(ivec3 p, ivec3 min, ivec3 max) {
    return p.x >= min.x && p.x < max.x && p.y >= min.y && p.y < max.y && p.z >= min.z && p.z < max.z;
}

bool VoxelHit(uint chunkIndex, uint localIndex) {
    return (chunks[chunkIndex].voxels[localIndex / 32] >> (localIndex % 32) & 1u) != 0;
}

bool TraverseChunk(uint chunkIndex, vec3 rayPos, vec3 rayDir, out HitInfo hitInfo) {
    // TODO: Doesn't update ray depth!
    bvec3 mask = bvec3(false);
    ivec3 mapPos = ivec3(rayPos);
    vec3 deltaDist = 1.0 / abs(rayDir);
    ivec3 rayStep = ivec3(sign(rayDir));
    vec3 sideDist = (rayStep * (vec3(mapPos) - rayPos) + (rayStep * 0.5) + 0.5) * deltaDist;

    mapPos = mapPos % 8;
    const uint maxStepsInChunk = 8 * 3;
    for (int i = 0; i < maxStepsInChunk; i++)
    {
        if (!PointInsideAabb(mapPos, ivec3(0), ivec3(8))) {
            break;
        }

        uint localIndex = mapPos.x + mapPos.y * 8 + mapPos.z * 64;
        if (VoxelHit(chunkIndex, localIndex)) {
            hitInfo = HitInfo(true, mapPos + vec3(0.5), vec4(1.0), mask, length(vec3(mask) * (sideDist - deltaDist)));
            break;
        }

        if (currentRayDepth >= maxRayDepth) {
            break;
        }

        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(mask) * rayStep;
        currentRayDepth++;
    }

    return hitInfo.hit;
}

bool TraverseWorld(vec3 rayPos, vec3 rayDir, out HitInfo hitInfo) {
    hitInfo.hit = false;
    bvec3 mask = bvec3(false);

    float tmin;
    if (!RayIntersectAabb(rayPos, 1.0 / rayDir, tmin)) {
        return false;
    }

    vec3 normal;
    if (tmin > 0) {
        rayPos += rayDir * tmin;

        // Push the ray a little bit into the bounds of the world
        vec3 worldCenter = (worldSize * 8) / 2.0;
        vec3 dist = rayPos - worldCenter;
        bvec3 strongestAxis = greaterThanEqual(dist.xyz, max(dist.yzx, dist.zxy));
        normal = vec3(strongestAxis) * ivec3(sign(dist));
        rayPos -= normal * 0.0001;
    }
    tmin = max(0.0, tmin);

    // Convert rayPos into chunk coordinates
    rayPos /= 8.0;

    ivec3 mapPos = ivec3(rayPos);
    vec3 deltaDist = 1.0 / abs(rayDir);
    ivec3 rayStep = ivec3(sign(rayDir));
    vec3 sideDist = (rayStep * (vec3(mapPos) - rayPos) + (rayStep * 0.5) + 0.5) * deltaDist;

    uint maxChunkSteps = worldSize.x + worldSize.y + worldSize.z;
    for (int i = 0; i < maxChunkSteps; i++)
    {
        if (!PointInsideAabb(mapPos, ivec3(0), worldSize)) {
            break;
        }

        // What chunk are we in right now
        uint indicesIndex = mapPos.x + mapPos.y * worldSize.x + mapPos.z * worldSize.x * worldSize.y;
        uint rawChunkIndex = chunkIndices[indicesIndex];
        uint loadState = (rawChunkIndex >> 28u) & 0xFu;
        if (loadState == 1u) {
            // Only mark the chunk for loading if the load queue has space
            if (loadQueueCount < loadQueueMaxCount) {
                uint old = atomicOr(chunkIndices[indicesIndex], (2u << 28u));
                if (((old >> 29u) & 0x1u) == 0) {
                    const uint loadIndex = atomicAdd(loadQueueCount, 1u);
                    if (loadIndex < loadQueueMaxCount) {
                        loadQueue[loadIndex] = ivec4(mapPos, 1);
                    } else {
                        atomicXor(chunkIndices[indicesIndex], (2u << 28u));
                    }
                }
            }

            float d = length(vec3(mask) * (sideDist - deltaDist));
            uint col = rawChunkIndex & 0x00FFFFFFu;
            vec4 color = vec4(vec3(col >> 16u, (col >> 8u) & 0xFFu, col & 0xFFu), 1.0);
            color.rgb /= 255;
            hitInfo = HitInfo(true, mapPos * 8.0, color, mask, tmin + d * 8);
            break;
        }
        else if (loadState == 4u) {
            // What's our world position?
            float chunkDistance = length(vec3(mask) * (sideDist - deltaDist));
            chunkDistance += 0.0001;// TODO: Find a better way to fix the fpp artifacts!
            vec3 chunkFrac = (rayPos + rayDir * chunkDistance) - vec3(mapPos);

            // Traverse the chunk!
            if (TraverseChunk(rawChunkIndex & 0x0FFFFFFFu, chunkFrac * 8, rayDir, hitInfo)) {
                float d = length(vec3(mask) * (sideDist - deltaDist));
                hitInfo.d += tmin + d * 8;
                hitInfo.pos += mapPos * 8.0;
                break;
            }
        }

        if (currentRayDepth >= maxRayDepth) {
            break;
        }

        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(mask) * rayStep;
    }

    return hitInfo.hit;
}

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 imgSize = imageSize(_DrawTexture);

    // This discards the extra pixels in cases where the image size isn't perfectly divisible by the kernel.xy
    if (imgCoord.x >= imgSize.x || imgCoord.y >= imgSize.y) return;

    vec3 rayPos, rayDir;
    BuildRayFromCamera(vec2(2.0 * imgCoord.xy / imgSize.xy - 1.0), rayPos, rayDir);

    vec4 finalColor;
    HitInfo hitInfo;
    if (TraverseWorld(rayPos, rayDir, hitInfo)) {
        finalColor.rgb = hitInfo.color.rgb * (hitInfo.mask.x ? 0.5 : hitInfo.mask.y ? 1.0 : hitInfo.mask.z ? 0.75 : 0.0);
    }

    imageStore(_DrawTexture, imgCoord, finalColor);
}
