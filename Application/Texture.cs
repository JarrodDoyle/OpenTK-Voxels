using OpenTK.Graphics.OpenGL;

namespace Application;

public class Texture
{
    // TODO: Make this dispose
    private int _id;
    private readonly TextureSettings _settings;

    public Texture(TextureSettings textureSettings, IntPtr data)
    {
        _settings = textureSettings;

        GL.CreateTextures(_settings.Target, 1, out _id);
        GL.TextureParameter(_id, TextureParameterName.TextureMinFilter, (int) _settings.MinFilter);
        GL.TextureParameter(_id, TextureParameterName.TextureMagFilter, (int) _settings.MagFilter);
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
        GL.BindTexture(_settings.Target, _id);
        switch (_settings.Dimensions)
        {
            case 1:
                GL.TexImage1D(_settings.Target, 0, _settings.InternalPixelFormat, _settings.Width, 0,
                    _settings.PixelFormat, _settings.PixelType, data);
                break;
            case 2:
                GL.TexImage2D(_settings.Target, 0, _settings.InternalPixelFormat, _settings.Width, _settings.Height, 0,
                    _settings.PixelFormat, _settings.PixelType, data);
                break;
            case 3:
                GL.TexImage3D(_settings.Target, 0, _settings.InternalPixelFormat, _settings.Width, _settings.Height,
                    _settings.Depth, 0, _settings.PixelFormat, _settings.PixelType, data);
                break;
        }
    }
}