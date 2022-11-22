using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Application.Brickmap;

struct Brick
{
    public uint[] Data;
}

public class World
{
    public ulong FilledVoxels { get; private set; }
    public uint LoadedBricks { get; private set; }

    public Vector3i GridDimensions { get; }
    public Vector3i MapDimensions { get; }

    private readonly Brick[] _bricks;
    private BufferObject _worldConfig;
    private BufferObject _worldVoxels;

    public World(Vector3i gridDimensions, Vector3i mapDimensions)
    {
        GridDimensions = gridDimensions;
        MapDimensions = mapDimensions;
        _bricks = new Brick[gridDimensions.X * gridDimensions.Y * gridDimensions.Z];

        _worldConfig = new BufferObject(new BufferObjectSettings
        {
            Size = Vector3i.SizeInBytes + sizeof(int),
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 0,
            Offset = 0,
        });
        _worldConfig.UploadData(0, Vector3i.SizeInBytes, gridDimensions);
        _worldConfig.UploadData(Vector3i.SizeInBytes, sizeof(int), 512);

        _worldVoxels = new BufferObject(new BufferObjectSettings
        {
            Size = (512 / 8) * gridDimensions.X * gridDimensions.Y * gridDimensions.Z,
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 1,
            Offset = 0,
        });
    }

    public void Generate(int seed, float frequency, string generatorType, float gain, int octaves, float lacunarity)
    {
        // Create the generator
        var generator = new FastNoise("FractalFBm");
        generator.Set("Source", new FastNoise(generatorType));
        generator.Set("Gain", gain);
        generator.Set("Octaves", octaves);
        generator.Set("Lacunarity", lacunarity);

        // Generate each brickmap
        for (var x = 0; x < GridDimensions.X; x++)
        for (var y = 0; y < GridDimensions.Y; y++)
        for (var z = 0; z < GridDimensions.Z; z++)
            GenerateMap(x, y, z, generator, seed, frequency);

        // Upload the brickmaps
        const int voxelDataSize = 512 / 8;
        const int chunkSize = voxelDataSize + sizeof(uint);
        var bricksArr = _bricks.ToArray();
        for (var i = 0; i < bricksArr.Length; i++)
        {
            var brick = bricksArr[i];
            // TODO: This isn't actually correct but it works for now (Only checks chunks of 4 bytes)
            var numFilled = brick.Data.Count(voxel => voxel != 0);
            _worldVoxels.UploadData(i * chunkSize, sizeof(uint), numFilled);
            _worldVoxels.UploadData(i * chunkSize + sizeof(uint), voxelDataSize, brick.Data);
        }
    }

    private void GenerateMap(int gridX, int gridY, int gridZ, FastNoise generator, int seed, float frequency)
    {
        // Generate noise values
        var numVoxels = MapDimensions.X * MapDimensions.Y * MapDimensions.Z;
        var noiseData = new float[numVoxels];
        generator.GenUniformGrid3D(noiseData, gridX * MapDimensions.X, gridY * MapDimensions.Y, gridZ * MapDimensions.Z,
            MapDimensions.X, MapDimensions.Y, MapDimensions.Z, frequency, seed);

        // Build the brick
        const int voxelsPerUint = sizeof(uint) * 8;
        var brick = new Brick {Data = new uint[numVoxels / voxelsPerUint]};
        for (var brickX = 0; brickX < MapDimensions.X; brickX++)
        for (var brickY = 0; brickY < MapDimensions.Y; brickY++)
        for (var brickZ = 0; brickZ < MapDimensions.Z; brickZ++)
        {
            var brickIndex = brickX + brickY * MapDimensions.X + brickZ * MapDimensions.X * MapDimensions.Y;
            if (noiseData[brickIndex] > 0) continue;

            FilledVoxels++;
            brick.Data[brickIndex / voxelsPerUint] |= (uint) (1 << (brickIndex % voxelsPerUint));
        }

        var gridIndex = gridX + gridY * GridDimensions.X + gridZ * GridDimensions.X * GridDimensions.Y;
        _bricks[gridIndex] = brick;
    }
}