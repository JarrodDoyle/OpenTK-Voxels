#version 450 core

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(binding = 0, rgba32f) restrict uniform image2D _img_result;
uniform ivec2 _resolution;

void main() {
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    if (imgCoord.x < _resolution.x && imgCoord.y < _resolution.y)
    {
        imageStore(_img_result, imgCoord, vec4(imgCoord.x / 1280.0, imgCoord.y / 720.0, 1.0, 1.0));
    }
}