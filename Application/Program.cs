namespace Application;

internal static class Program
{
    private static void Main()
    {
        using var app = new AppWindow(1280, 720, "JVoxel");
        app.Run();
    }
}