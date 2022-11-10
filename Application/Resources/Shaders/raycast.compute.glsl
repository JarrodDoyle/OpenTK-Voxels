#version 450 core

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout (binding = 0, rgba32f) restrict uniform image2D _img_result;
layout (binding = 1) uniform sampler3D _voxels;
uniform ivec2 _resolution;

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    
    if (imgCoord.x < _resolution.x && imgCoord.y < _resolution.y)
    {
        int idx = (imgCoord.x * imgCoord.y) % (16 * 16 * 16);
        float x = (idx % 16) / 16.0;
        float y = ((idx / 16) % 16) / 16.0;
        float z = (idx / (16 * 16)) / 16.0;
        imageStore(_img_result, imgCoord, texture(_voxels, vec3(x, y, z)));
    }
}