using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Application;

public class Raycaster
{
    private int _width;
    private int _height;
    private ShaderProgram _shaderProgram;
    private readonly Texture _voxels;
    private readonly Vector3i _voxelDimensions;
    private int _maxRayDepth;

    public readonly Texture Texture;
    public float _time;


    public Raycaster(int width, int height, Vector3i voxelDimensions, int maxRayDepth)
    {
        _maxRayDepth = maxRayDepth;
        _voxelDimensions = voxelDimensions;
        _time = 0f;
        _width = width;
        _height = height;
        _shaderProgram = new ShaderProgram(new Dictionary<string, ShaderType>
        {
            {"Resources/Shaders/raycast.compute.glsl", ShaderType.ComputeShader},
        });
        
        _shaderProgram.Use();
        UploadShaderUniforms();

        var textureSettings = new TextureSettings
        {
            Width = _width, Height = _height, Dimensions = 2, Target = TextureTarget.Texture2D
        };
        Texture = new Texture(textureSettings, IntPtr.Zero);

        var voxelSettings = new TextureSettings
        {
            Width = voxelDimensions.X,
            Height = voxelDimensions.Y,
            Depth = voxelDimensions.Z,
            Dimensions = 3,
            Target = TextureTarget.Texture3D,
            MinFilter = TextureMinFilter.Nearest,
            MagFilter = TextureMagFilter.Nearest,
            PixelFormat = PixelFormat.Red,
            PixelType = PixelType.UnsignedByte
        };
        _voxels = new Texture(voxelSettings, IntPtr.Zero);
        _voxels.BindSampler(1);
    }

    public void Render(float dt)
    {
        _time += dt;
        _shaderProgram.Use();
        UploadShaderUniforms();
        Texture.BindImage(0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        GL.DispatchCompute(_width / 8, _height / 8, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit);
    }

    public unsafe void UploadVoxels(byte[] voxels)
    {
        // Check that voxels array is the correct length
        // TODO: This makes big assumptions that the texture is one byte per pixel!!
        if (voxels.Length != _voxels.Settings.PixelCount())
        {
            var msg = $"Invalid number of voxels. Got {voxels.Length} expected {_voxels.Settings.PixelCount()}.";
            throw new ArgumentException(msg);
        }

        fixed (byte* voxelsPtr = voxels)
        {
            _voxels.UpdateTexture(new IntPtr(voxelsPtr));
        }
    }

    private void UploadShaderUniforms()
    {
        _shaderProgram.Upload("_voxelDims", _voxelDimensions);
        _shaderProgram.Upload("_maxRayDepth", _maxRayDepth);
        _shaderProgram.Upload("_sunlightDir", new Vector3(1.0f, 1.0f, 1.0f).Normalized());
        _shaderProgram.Upload("_time", _time);
    }
}