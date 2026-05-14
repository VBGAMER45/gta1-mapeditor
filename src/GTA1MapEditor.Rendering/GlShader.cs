using OpenTK.Graphics.OpenGL4;

namespace GTA1MapEditor.Rendering;

/// <summary>Minimal compile/link/bind wrapper around a GL program.</summary>
public sealed class GlShader : IDisposable
{
    public int Program { get; }

    public GlShader(string vertexSource, string fragmentSource)
    {
        int vs = CompileShader(ShaderType.VertexShader, vertexSource);
        int fs = CompileShader(ShaderType.FragmentShader, fragmentSource);
        Program = GL.CreateProgram();
        GL.AttachShader(Program, vs);
        GL.AttachShader(Program, fs);
        GL.LinkProgram(Program);
        GL.GetProgram(Program, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(Program);
            GL.DeleteProgram(Program);
            throw new InvalidOperationException("Shader link failed: " + log);
        }
        GL.DetachShader(Program, vs);
        GL.DetachShader(Program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
    }

    public void Use() => GL.UseProgram(Program);

    public int GetUniform(string name) => GL.GetUniformLocation(Program, name);

    public void Dispose() => GL.DeleteProgram(Program);

    private static int CompileShader(ShaderType type, string source)
    {
        int id = GL.CreateShader(type);
        GL.ShaderSource(id, source);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = GL.GetShaderInfoLog(id);
            GL.DeleteShader(id);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }
        return id;
    }
}
