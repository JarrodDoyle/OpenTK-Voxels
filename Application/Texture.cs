using OpenTK.Graphics.OpenGL;

namespace Application;

public class Texture
{
    // TODO: Make this dispose
    public readonly TextureSettings Settings;
    private int _id;

    public Texture(TextureSettings textureSettings, IntPtr data)
    {
        Settings = textureSettings;

        GL.CreateTextures(Settings.Target, 1, out _id);
        GL.TextureParameter(_id, TextureParameterName.TextureMinFilter, (int) Settings.MinFilter);
        GL.TextureParameter(_id, TextureParameterName.TextureMagFilter, (int) Settings.MagFilter);
        UpdateTexture(data);
    }

    public void BindImage(int unit, int level, bool layered, int layer, TextureAccess textureAccess,
        SizedInternalFormat sizedInternalFormat)
    {
        GL.BindImageTexture(unit, _id, level, layered, layer, textureAccess, sizedInternalFormat);
    }

    public void BindSampler(int unit)
    {
        GL.BindTextureUnit(unit, _id);
    }

    public void UpdateTexture(IntPtr data)
    {
        GL.BindTexture(Settings.Target, _id);
        switch (Settings.Dimensions)
        {
            case 1:
                GL.TexImage1D(Settings.Target, 0, Settings.InternalPixelFormat, Settings.Width, 0,
                    Settings.PixelFormat, Settings.PixelType, data);
                break;
            case 2:
                GL.TexImage2D(Settings.Target, 0, Settings.InternalPixelFormat, Settings.Width, Settings.Height, 0,
                    Settings.PixelFormat, Settings.PixelType, data);
                break;
            case 3:
                GL.TexImage3D(Settings.Target, 0, Settings.InternalPixelFormat, Settings.Width, Settings.Height,
                    Settings.Depth, 0, Settings.PixelFormat, Settings.PixelType, data);
                break;
        }
    }
}