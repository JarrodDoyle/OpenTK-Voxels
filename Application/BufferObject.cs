using OpenTK.Graphics.OpenGL;

namespace Application;

public struct BufferObjectSettings
{
    public int Size;
    public IntPtr Data;
    public BufferStorageFlags StorageFlags;
    public BufferRangeTarget RangeTarget;
    public int Index;
    public int Offset;
}

public class BufferObject :IDisposable
{
    private int _id = -1;
    public readonly BufferObjectSettings Settings;

    public BufferObject(BufferObjectSettings settings)
    {
        Settings = settings;
        
        GL.CreateBuffers(1, out _id);
        GL.NamedBufferStorage(_id, Settings.Size, Settings.Data, Settings.StorageFlags);
        GL.BindBufferRange(Settings.RangeTarget, Settings.Index, _id, (IntPtr)Settings.Offset, Settings.Size);
    }
    
    public void UploadData<T>(int offset, int size, T data) where T : struct
    {
        GL.NamedBufferSubData(_id, (IntPtr)offset, size, ref data);
    }
    
    public void UploadData<T>(int offset, int size, T[] data) where T : struct
    {
        GL.NamedBufferSubData(_id, (IntPtr)offset, size, data);
    }
    
    public void DownloadData<T>(int offset, int size, out T data) where T : struct
    {
        data = new T();
        GL.GetNamedBufferSubData(_id, (IntPtr)offset, size, ref data);
    }
    
    public void DownloadData<T>(int offset, int size, T[] data) where T : struct
    {
        GL.GetNamedBufferSubData(_id, (IntPtr)offset, size, data);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_id);
    }
}