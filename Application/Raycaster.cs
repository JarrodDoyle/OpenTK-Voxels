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

        var settings = new TextureSettings
        {
            Width = _width, Height = _height, Dimensions = 2, Target = TextureTarget.Texture2D
        };
        Texture = new Texture(settings, IntPtr.Zero);
    }

    public void Render()
    {
        _shaderProgram.Use();
        Texture.BindImage(0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        _shaderProgram.Upload("_resolution", new Vector2i(_width, _height));
        GL.DispatchCompute(_width / 8, _height / 8, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit);
    }

    public unsafe void UploadVoxels(byte[] voxels)
    {
        // TODO: Make this versatile for more than 16x16x16
        fixed (byte* voxelsPtr = voxels)
        {
            var settings = new TextureSettings
            {
                Width = 16, Height = 16, Depth = 16, Dimensions = 3, Target = TextureTarget.Texture3D,
                PixelFormat = PixelFormat.Red, PixelType = PixelType.Byte
            };
            var texture = new Texture(settings, new IntPtr(voxelsPtr));
            texture.BindSampler(1);
        }
    }
}