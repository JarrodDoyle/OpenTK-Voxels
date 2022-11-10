#version 450 core
layout(location = 0) out vec4 _frag_color;

layout(binding = 0) uniform sampler2D _texture;

in vec2 _tex_coord;

void main()
{
    _frag_color = texture(_texture, _tex_coord);
}