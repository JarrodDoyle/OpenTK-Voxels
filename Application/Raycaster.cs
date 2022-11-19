using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Application;

public class Raycaster
{
    private int _width;
    private int _height;
    private ShaderProgram _shaderProgram;
    private readonly Vector3i _voxelDimensions;
    private int _maxRayDepth;
    private BufferObject _worldConfig;
    private BufferObject _world;

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
            {"Resources/Shaders/draw.comp.glsl", ShaderType.ComputeShader},
        });

        _shaderProgram.Use();
        UploadShaderUniforms();

        var textureSettings = new TextureSettings
        {
            Width = _width, Height = _height, Dimensions = 2, Target = TextureTarget.Texture2D
        };
        Texture = new Texture(textureSettings, IntPtr.Zero);

        _worldConfig = new BufferObject(new BufferObjectSettings
        {
            Size = Vector3i.SizeInBytes + sizeof(int),
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 0,
            Offset = 0,
        });
        _worldConfig.UploadData(0, Vector3i.SizeInBytes, _voxelDimensions);
        _worldConfig.UploadData(Vector3i.SizeInBytes, sizeof(int), _maxRayDepth);

        _world = new BufferObject(new BufferObjectSettings
        {
            Size = sizeof(byte) * 512 * _voxelDimensions.X * _voxelDimensions.Y * _voxelDimensions.Z,
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 1,
            Offset = 0,
        });
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

    public void UploadVoxelChunk(Vector3i chunkPos, byte[] voxels)
    {
        // TODO: Check that the data is valid (position and length)
        var chunkIndex = chunkPos.X + chunkPos.Y * _voxelDimensions.X +
                         chunkPos.Z * _voxelDimensions.X * _voxelDimensions.Y;

        const int voxelDataSize = 512 * sizeof(byte);
        const int chunkSize = voxelDataSize + sizeof(uint);

        var numFilled = voxels.Count(voxel => voxel != 0);
        _world.UploadData(chunkIndex * chunkSize, sizeof(uint), numFilled);
        _world.UploadData(chunkIndex * chunkSize + sizeof(uint), voxelDataSize, voxels);
    }

    private void UploadShaderUniforms()
    {
        _shaderProgram.Upload("_voxelDims", _voxelDimensions);
        _shaderProgram.Upload("_maxRayDepth", _maxRayDepth);
        _shaderProgram.Upload("_sunlightDir", new Vector3(1.0f, 1.0f, 1.0f).Normalized());
        _shaderProgram.Upload("_time", _time);
    }
}