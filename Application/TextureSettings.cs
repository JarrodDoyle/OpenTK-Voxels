using OpenTK.Graphics.OpenGL;

namespace Application;

public struct TextureSettings
{
    public int Width;
    public int Height;
    public int Depth;
    public int Dimensions;
    public TextureTarget Target;

    public TextureMinFilter MinFilter = TextureMinFilter.Linear;
    public TextureMagFilter MagFilter = TextureMagFilter.Linear;
    public PixelInternalFormat InternalPixelFormat = PixelInternalFormat.Rgba32f;
    public PixelFormat PixelFormat = PixelFormat.Rgba;
    public PixelType PixelType = PixelType.Float;

    public TextureSettings()
    {
        Width = 1;
        Height = 1;
        Depth = 0;
        Dimensions = 2;
        Target = TextureTarget.Texture2D;
    }

    public readonly int PixelCount()
    {
        return Dimensions switch
        {
            1 => Width,
            2 => Width * Height,
            3 => Width * Height * Depth,
            _ => 0
        };
    }
}