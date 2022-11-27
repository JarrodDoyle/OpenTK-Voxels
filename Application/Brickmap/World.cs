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

    private uint _brickmapSize;
    private uint _brickPoolSize;
    private uint _brickPoolIndex;

    private readonly uint[] _indices;
    private readonly List<Brick> _brickPool;
    private BufferObject _indicesBuffer;
    private BufferObject _brickPoolBuffer;
    private BufferObject _worldConfig;

    public World(Vector3i gridDimensions, Vector3i mapDimensions, uint brickPoolSize)
    {
        GridDimensions = gridDimensions;
        MapDimensions = mapDimensions;

        // Allocate the World Config SSBO
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

        // Allocate the Brickmap Pool SSBO
        _brickPool = new List<Brick>();
        _brickmapSize = (uint) (MapDimensions.X * MapDimensions.Y * MapDimensions.Z / 8);
        _brickPoolSize = brickPoolSize;
        _brickPoolIndex = 0;
        _brickPoolBuffer = new BufferObject(new BufferObjectSettings
        {
            Size = (int) (_brickmapSize * _brickPoolSize),
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 1,
            Offset = 0,
        });

        // Allocate the Brickmap Index SSBO
        var numIndices = gridDimensions.X * gridDimensions.Y * gridDimensions.Z;
        _indices = new uint[numIndices];
        _indicesBuffer = new BufferObject(new BufferObjectSettings
        {
            Size = sizeof(uint) * numIndices,
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 2,
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

        // Upload all the non-empty bricks
        var bricksArr = _brickPool.ToArray();
        var offset = 0;
        foreach (var brick in bricksArr)
        {
            var brickSize = brick.Data.Length * sizeof(uint);
            _brickPoolBuffer.UploadData(offset, brickSize, brick.Data);
            offset += brickSize;
            LoadedBricks++;
        }

        _indicesBuffer.UploadData(0, _indices.Length * sizeof(uint), _indices);

        if (LoadedBricks > _brickPoolSize)
        {
            Console.WriteLine("Uh oh too many bricks loaded!");
            Console.WriteLine(
                $"_indices.length: {_indices.Length}, LoadedBricks: {LoadedBricks}, _brickPoolSize: {_brickPoolSize}");
        }
    }

    private void GenerateMap(int gridX, int gridY, int gridZ, FastNoise generator, int seed, float frequency)
    {
        // TODO: Multithread this!
        // Generate noise values
        var numVoxels = MapDimensions.X * MapDimensions.Y * MapDimensions.Z;
        var noiseData = new float[numVoxels];
        generator.GenUniformGrid3D(noiseData, gridX * MapDimensions.X, gridY * MapDimensions.Y, gridZ * MapDimensions.Z,
            MapDimensions.X, MapDimensions.Y, MapDimensions.Z, frequency, seed);

        // Build the brick
        const int voxelsPerUint = sizeof(uint) * 8;
        var empty = true;
        var brick = new Brick {Data = new uint[numVoxels / voxelsPerUint]};
        for (var brickX = 0; brickX < MapDimensions.X; brickX++)
        for (var brickY = 0; brickY < MapDimensions.Y; brickY++)
        for (var brickZ = 0; brickZ < MapDimensions.Z; brickZ++)
        {
            var brickIndex = brickX + brickY * MapDimensions.X + brickZ * MapDimensions.X * MapDimensions.Y;
            if (noiseData[brickIndex] > 0) continue;

            FilledVoxels++;
            empty = false;
            brick.Data[brickIndex / voxelsPerUint] |= (uint) (1 << (brickIndex % voxelsPerUint));
        }

        if (!empty)
        {
            var gridIndex = gridX + gridY * GridDimensions.X + gridZ * GridDimensions.X * GridDimensions.Y;
            _indices[gridIndex] = (uint) _brickPool.Count | (1u << 28);
            _brickPool.Add(brick);
        }
    }
}