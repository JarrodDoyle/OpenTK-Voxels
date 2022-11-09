using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Application;

public class AppWindow : GameWindow
{
    private HashSet<string> _driverExtensions;
    
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
        CheckRequiredExtension(new Version(4,2), "GL_ARB_texture_storage");

        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
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
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        
        GL.Clear(ClearBufferMask.ColorBufferBit);

        SwapBuffers();
    }

    private void CheckRequiredExtension(Version minVersion, string extensionName)
    {
        if (!(APIVersion >= minVersion || _driverExtensions.Contains(extensionName)))
            throw new NotSupportedException($"Extension {extensionName} is not available."); 
    }
}