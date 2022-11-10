using System.Drawing;
using OpenTK.Graphics.OpenGL;

namespace Application;

public class Texture
{
    private int _width;
    private int _height;
    private int _id;

    public Texture(int width, int height, TextureMinFilter minFilter, TextureMagFilter magFilter)
    {
        _width = width;
        _height = height;

        const TextureTarget target = TextureTarget.Texture2D;

        GL.CreateTextures(target, 1, out _id);
        GL.TextureParameter(_id, TextureParameterName.TextureMinFilter, (int) minFilter);
        GL.TextureParameter(_id, TextureParameterName.TextureMagFilter, (int) magFilter);
        GL.BindTexture(target, _id);
        GL.TexImage2D(target, 0, PixelInternalFormat.Rgba32f, _width, _height, 0, PixelFormat.Rgba,
            PixelType.Float, IntPtr.Zero);
    }
    
    public void BindImage(int unit, int level, bool layered, int layer, TextureAccess textureAccess, SizedInternalFormat sizedInternalFormat)
    {
        GL.BindImageTexture(unit, _id, level, layered, layer, textureAccess, sizedInternalFormat);
    }

    public void BindSampler(int unit)
    {
        GL.BindTextureUnit(unit, _id);
    }

    public unsafe void SpewData(int x, int y)
    {
        long offset = 4 * (x + y * _width);
        var data = new byte[4 * _width * _height];
        fixed (byte* ptr = &data[0])
        {
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptr);
            Console.WriteLine(Color.FromArgb(data[offset + 3], data[offset + 0], data[offset + 1], data[offset + 2]));
        }
    }
}