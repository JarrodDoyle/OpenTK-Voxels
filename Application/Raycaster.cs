using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Application;

public class Raycaster
{
    private int _width;
    private int _height;
    private ShaderProgram _shaderProgram;

    public readonly Texture Texture;

    public Raycaster(int width, int height)
    {
        _width = width;
        _height = height;
        _shaderProgram = new ShaderProgram(new Dictionary<string, ShaderType>
        {
            {"Resources/Shaders/draw.comp.glsl", ShaderType.ComputeShader},
        });

        var textureSettings = new TextureSettings
        {
            Width = _width, Height = _height, Dimensions = 2, Target = TextureTarget.Texture2D
        };
        Texture = new Texture(textureSettings, IntPtr.Zero);
    }

    public void Render(float dt)
    {
        _shaderProgram.Use();
        Texture.BindImage(0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        GL.DispatchCompute(_width / 8, _height / 8, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit);
    }

}