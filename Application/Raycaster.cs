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
            {"Resources/Shaders/raycast.compute.glsl", ShaderType.ComputeShader},
        });
        Texture = new Texture(_width, _height, TextureMinFilter.Linear, TextureMagFilter.Linear);
    }

    public void Render()
    {
        _shaderProgram.Use();
        Texture.BindImage(0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        _shaderProgram.Upload("_resolution", new Vector2i(_width, _height));
        GL.DispatchCompute(_width / 8, _height / 8, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit);
    }
}