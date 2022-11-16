using System.Security.Cryptography;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Application;

public class ShaderProgram : IDisposable
{
    private int _id = -1;
    private readonly Dictionary<string, ShaderType> _shaderDetails;
    private readonly Dictionary<string, byte[]> _shaderHashes;

    public ShaderProgram(Dictionary<string, ShaderType> shaderDetails)
    {
        _shaderDetails = shaderDetails;
        _shaderHashes = new Dictionary<string, byte[]>();
        Reload();
    }

    public void Use()
    {
        if (AnyFilesUpdated())
        {
            Reload();
        }

        GL.UseProgram(_id);
    }

    public void Upload(string name, Vector2i vec2)
    {
        GL.ProgramUniform2(_id, GL.GetUniformLocation(_id, name), vec2);
    }

    public void Upload(string name, Vector3i value)
    {
        GL.ProgramUniform3(_id, GL.GetUniformLocation(_id, name), value);
    }

    public void Upload(string name, Vector3 value)
    {
        GL.ProgramUniform3(_id, GL.GetUniformLocation(_id, name), value);
    }

    public void Upload(string name, float value)
    {
        GL.ProgramUniform1(_id, GL.GetUniformLocation(_id, name), value);
    }

    public void Upload(string name, int value)
    {
        GL.ProgramUniform1(_id, GL.GetUniformLocation(_id, name), value);
    }

    public void Reload()
    {
        if (_id != -1)
            GL.DeleteProgram(_id);

        // Load all of the shaders
        var shaderIdsList = new List<int>();
        foreach (var (sourcePath, shaderType) in _shaderDetails)
        {
            var shaderId = GL.CreateShader(shaderType);
            GL.ShaderSource(shaderId, File.ReadAllText(sourcePath));
            GL.CompileShader(shaderId);

            // Check for shader compilation errors
            var compileLog = GL.GetShaderInfoLog(shaderId);
            if (compileLog != string.Empty)
                Console.WriteLine(compileLog);

            shaderIdsList.Add(shaderId);
        }

        var shaderIds = shaderIdsList.ToArray();

        // Setup the new program
        _id = GL.CreateProgram();
        foreach (var shaderId in shaderIds)
            GL.AttachShader(_id, shaderId);
        GL.LinkProgram(_id);

        // Check for program compilation errors
        GL.GetProgram(_id, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            var infoLog = GL.GetProgramInfoLog(_id);
            Console.WriteLine(infoLog);
        }

        // Get rid of the shaders themselves, we don't need them anymore
        foreach (var shaderId in shaderIds)
        {
            GL.DetachShader(_id, shaderId);
            GL.DeleteShader(shaderId);
        }

        // If everything was successful, we should compute the new hashes
        _shaderHashes.Clear();
        var md5 = MD5.Create();
        foreach (var (sourcePath, _) in _shaderDetails)
        {
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var hash = md5.ComputeHash(sourceBytes);
            _shaderHashes.Add(sourcePath, hash);
        }

        Console.WriteLine($"Reloaded shader program. New ID {_id}.");
    }

    private bool AnyFilesUpdated()
    {
        var updated = false;
        var md5 = MD5.Create();
        foreach (var (sourcePath, _) in _shaderDetails)
        {
            var sourceBytes = File.ReadAllBytes(sourcePath);
            if (!_shaderHashes[sourcePath].SequenceEqual(md5.ComputeHash(sourceBytes)))
            {
                updated = true;
                break;
            }
        }

        return updated;
    }

    public void Dispose()
    {
        GL.DeleteProgram(_id);
    }
}