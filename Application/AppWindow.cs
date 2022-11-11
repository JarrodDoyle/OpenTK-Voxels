using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Application;

public class AppWindow : GameWindow
{
    private HashSet<string> _driverExtensions;
    private Raycaster _raycaster;
    private ShaderProgram _shader;
    private BufferObject _cameraUbo;
    private Camera _camera;

    private int _vao;

    public AppWindow(int width, int height, string title) :
        base(GameWindowSettings.Default, new NativeWindowSettings {Size = (width, height), Title = title})
    {
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        // Basic system info
        Console.WriteLine($"OpenGL: {GL.GetString(StringName.Version)}");
        Console.WriteLine($"GLSL: {GL.GetString(StringName.ShadingLanguageVersion)}");
        Console.WriteLine($"GPU: {GL.GetString(StringName.Renderer)}");

        // What extension features do we have?
        var extensionCount = GL.GetInteger(GetPName.NumExtensions);
        _driverExtensions = new HashSet<string>();
        for (var i = 0; i < extensionCount; i++)
            _driverExtensions.Add(GL.GetString(StringNameIndexed.Extensions, i));

        // If a required extension isn't found we throw an error
        CheckRequiredExtension(new Version(4, 5), "GL_ARB_direct_state_access");
        CheckRequiredExtension(new Version(4, 4), "GL_ARB_buffer_storage");
        CheckRequiredExtension(new Version(4, 3), "GL_ARB_compute_shader");
        CheckRequiredExtension(new Version(4, 2), "GL_ARB_texture_storage");

        // Set the clear colour to something cool
        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

        // Load our stuff!
        var voxelDims = Vector3i.One * 512;
        _raycaster = new Raycaster(ClientSize.X, ClientSize.Y, voxelDims, 512);

        // Generate voxels!
        var seed = (int) DateTime.Now.ToBinary();
        var generator = new FastNoise("FractalFBm");
        generator.Set("Source", new FastNoise("Simplex"));
        generator.Set("Gain", 0.5f);
        generator.Set("Octaves", 5);
        generator.Set("Lacunarity", 2f);

        var numVoxels = voxelDims.X * voxelDims.Y * voxelDims.Z;
        var noiseData = new float[numVoxels];
        generator.GenUniformGrid3D(noiseData, 0, 0, 0, voxelDims.X, voxelDims.Y, voxelDims.Z, 0.005f, seed);
        
        var bytes = new byte[numVoxels];
        for (var i = 0; i < numVoxels; i++)
        {
            bytes[i] = (byte) (noiseData[i] > 0 ? 0 : 255);
        }

        _raycaster.UploadVoxels(bytes);

        _shader = new ShaderProgram(new Dictionary<string, ShaderType>
        {
            {"Resources/Shaders/screenQuad.vertex.glsl", ShaderType.VertexShader},
            {"Resources/Shaders/screenQuad.fragment.glsl", ShaderType.FragmentShader},
        });

        _cameraUbo = new BufferObject(new BufferObjectSettings
        {
            Size = Vector4.SizeInBytes * 9,
            Data = IntPtr.Zero,
            StorageFlags = BufferStorageFlags.DynamicStorageBit,
            RangeTarget = BufferRangeTarget.UniformBuffer,
            Index = 0,
            Offset = 0,
        });

        _camera = new Camera(Vector3.One * 0.5f, ClientSize.X / (float) ClientSize.Y);
        _camera.MoveSpeed = 10f;
        _camera.MouseSensitivity = 0.25f;
    }

    protected override void OnResize(ResizeEventArgs args)
    {
        base.OnResize(args);

        GL.Viewport(0, 0, args.Width, args.Height);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        var input = KeyboardState;
        if (input.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        if (input.IsKeyPressed(Keys.V))
        {
            VSync = (VSync == VSyncMode.On) ? VSyncMode.Off : VSyncMode.On;
        }

        if (MouseState.IsButtonPressed(MouseButton.Left))
            CursorState = CursorState.Grabbed;
        else if (MouseState.IsButtonPressed(MouseButton.Right))
            CursorState = CursorState.Normal;

        if (CursorState == CursorState.Grabbed)
            _camera.ProcessInputs((float) args.Time, input, MouseState);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        var dt = (float) RenderTime;
        Title = $"JVoxel - FPS: {(int) (1 / dt)}";

        if (_camera.ProcessUpdate())
        {
            _cameraUbo.UploadData(0, Vector4.SizeInBytes * 4, _camera.GetProjectionMatrix().Inverted());
            _cameraUbo.UploadData(Vector4.SizeInBytes * 4, Vector4.SizeInBytes * 4, _camera.GetViewMatrix().Inverted());
            _cameraUbo.UploadData(Vector4.SizeInBytes * 8, Vector4.SizeInBytes, _camera.Position);
        }

        _raycaster.Render(dt);

        _shader.Use();
        _raycaster.Texture.BindSampler(0);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

        SwapBuffers();
    }

    private void CheckRequiredExtension(Version minVersion, string extensionName)
    {
        if (!(APIVersion >= minVersion || _driverExtensions.Contains(extensionName)))
            throw new NotSupportedException($"Extension {extensionName} is not available.");
    }
}