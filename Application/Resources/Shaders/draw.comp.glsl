#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

struct Chunk {
    uint voxels[512];
};

layout (binding = 0, rgba32f) restrict uniform image2D _DrawTexture;

layout (std430, binding = 0) buffer WorldConfig {
    ivec3 worldSize;
    int maxRayDepth;
};

layout (std430, binding = 1) buffer World{
    Chunk chunks[];
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
vec4 voxelColor;

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

bool PointInsideAabb(ivec3 p) {
    return p.x >= 0 && p.x < worldSize.x * 8 && p.y >= 0 && p.y < worldSize.y * 8 && p.z >= 0 && p.z < worldSize.z * 8;
}

bool VoxelHit(ivec3 p) {
    ivec3 chunkPos = p / 8;
    uint chunkIndex = chunkPos.x + chunkPos.y * worldSize.x + chunkPos.z * worldSize.x * worldSize.y;
    Chunk chunk = chunks[chunkIndex];
    
    ivec3 localPos = p % 8;
    uint localIndex = localPos.x + localPos.y * 8 + localPos.z * 64;
    uint voxelHue = clamp(int(chunk.voxels[localIndex]), 0, 255);
    
    voxelColor = vec4(hsv2rgb(vec3(voxelHue / 255.0, 1.0, 1.0)), 1);
    return voxelHue != 0;
}

bool TraverseWorld(vec3 rayPos, vec3 rayDir, out HitInfo hitInfo) {
    hitInfo.hit = false;
    bvec3 mask;

    float tmin;
    if (!RayIntersectAabb(rayPos, 1.0 / rayDir, tmin)) {
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
    for (int i = 0; i < maxRayDepth; i++)
    {
        mask = lessThanEqual(sideDist.xyz, min(sideDist.yzx, sideDist.zxy));
        sideDist += vec3(mask) * deltaDist;
        mapPos += ivec3(mask) * rayStep;
        if (!PointInsideAabb(mapPos)) {
            break;
        }
        if (VoxelHit(mapPos)) {
            hitInfo = HitInfo(true, mapPos + vec3(0.5), voxelColor, mask, tmin + length(vec3(mask) * (sideDist - deltaDist)));
            break;
        }
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
        finalColor = hitInfo.color;
    }
    
    imageStore(_DrawTexture, imgCoord, finalColor);
}