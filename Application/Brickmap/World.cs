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
    private int _maxLoadQueueCount;

    private readonly uint[] _indices;
    private readonly List<Brick> _brickPool;
    private BufferObject _indicesBuffer;
    private BufferObject _brickPoolBuffer;
    private BufferObject _brickLoadQueueBuffer;
    private BufferObject _worldConfig;

    private readonly FastNoise _generator;
    private int _seed;
    private float _frequency;

    public World(Vector3i gridDimensions, Vector3i mapDimensions, uint brickPoolSize, int maxLoadQueueCount,
        FastNoise generator, int seed, float frequency)
    {
        _generator = generator;
        _seed = seed;
        _frequency = frequency;

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

        // Upload indices marked as unloaded
        var gpuIndices = new uint[numIndices];
        for (var i = 0; i < gpuIndices.Length; i++)
            gpuIndices[i] = 0x10000000u;
        _indicesBuffer.UploadData(0, sizeof(uint) * numIndices, gpuIndices);

        // Allocate the Load Queue SSBO
        _maxLoadQueueCount = maxLoadQueueCount;
        _brickLoadQueueBuffer = new BufferObject(new BufferObjectSettings
        {
            Size = 4 * sizeof(uint) + _maxLoadQueueCount * Vector4i.SizeInBytes,
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.ShaderStorageBuffer,
            Index = 3,
            Offset = 0,
        });
        _brickLoadQueueBuffer.UploadData(0, sizeof(uint), 0u);
        _brickLoadQueueBuffer.UploadData(sizeof(uint), sizeof(uint), _maxLoadQueueCount);
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

        if (LoadedBricks > _brickPoolSize)
        {
            Console.WriteLine("Uh oh too many bricks loaded!");
            Console.WriteLine(
                $"_indices.length: {_indices.Length}, LoadedBricks: {LoadedBricks}, _brickPoolSize: {_brickPoolSize}");
        }
    }

    public void ProcessLoadQueue()
    {
        // Get loadQueueCount
        _brickLoadQueueBuffer.DownloadData(0, sizeof(uint), out uint loadCount);
        if (loadCount == 0) return;

        // Get loadQueue (up to length of loadQueueCount)
        loadCount = Math.Min(loadCount, (uint) _maxLoadQueueCount);
        var loadPositions = new Vector4i[loadCount];
        _brickLoadQueueBuffer.DownloadData(4 * sizeof(uint), (int) (loadCount * Vector4i.SizeInBytes), loadPositions);

        // Reset loadCount
        _brickLoadQueueBuffer.UploadData(0, sizeof(uint), 0u);

        // Generate the requested bricks
        foreach (var pos in loadPositions)
            GenerateMap(pos.X, pos.Y, pos.Z, _generator, _seed, _frequency);

        // Upload the requested bricks and update their index
        var bricksArr = _brickPool.ToArray();
        foreach (var pos in loadPositions)
        {
            // Get the CPU brick index + brick
            var indicesIndex = pos.X + pos.Y * GridDimensions.X + pos.Z * GridDimensions.X * GridDimensions.Y;
            var rawIndex = _indices[indicesIndex];
            if (rawIndex == 0)
            {
                _indicesBuffer.UploadData(indicesIndex * sizeof(uint), sizeof(uint), 0);
                continue;
            }

            var index = rawIndex & 0x0FFFFFFFu;
            var brick = bricksArr[index];

            // Update the GPU brick index
            var gpuIndex = LoadedBricks | (4u << 28);
            _indicesBuffer.UploadData(indicesIndex * sizeof(uint), sizeof(uint), gpuIndex);

            // Upload the brick data to GPU
            // TODO: probably shouldn't calculate bricksize here, have it as a class property
            var brickSize = brick.Data.Length * sizeof(uint);
            _brickPoolBuffer.UploadData((int) (LoadedBricks * brickSize), brickSize, brick.Data);
            LoadedBricks++;
        }
    }
}