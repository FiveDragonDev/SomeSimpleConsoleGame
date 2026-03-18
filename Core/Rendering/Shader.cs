using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using static OpenTK.Graphics.OpenGL4.GL;

namespace SomeSimpleConsoleGame.Core.Rendering
{
    public sealed class Shader : IDisposable
    {
        public int Handle { get; }

        public Shader(string vertexShaderSource, string fragmentShaderSource)
        {
            var vertexShader = CreateShader(ShaderType.VertexShader);
            ShaderSource(vertexShader, vertexShaderSource);
            CompileShader(vertexShader);

            GetShader(vertexShader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GetShaderInfoLog(vertexShader);
                throw new Exception($"Vertex shader compilation failed: {infoLog}");
            }

            var fragmentShader = CreateShader(ShaderType.FragmentShader);
            ShaderSource(fragmentShader, fragmentShaderSource);
            CompileShader(fragmentShader);

            GetShader(fragmentShader, ShaderParameter.CompileStatus, out success);
            if (success == 0)
            {
                string infoLog = GetShaderInfoLog(fragmentShader);
                throw new Exception($"Fragment shader compilation failed: {infoLog}");
            }

            Handle = CreateProgram();
            AttachShader(Handle, vertexShader);
            AttachShader(Handle, fragmentShader);
            LinkProgram(Handle);

            GetProgram(Handle, GetProgramParameterName.LinkStatus, out success);
            if (success == 0)
            {
                string infoLog = GetProgramInfoLog(Handle);
                throw new Exception($"Program linking failed: {infoLog}");
            }

            DetachShader(Handle, vertexShader);
            DetachShader(Handle, fragmentShader);
            DeleteShader(vertexShader);
            DeleteShader(fragmentShader);
        }

        public void Uniform(string name, in bool value) => Uniform1(GetLocation(name), value ? 1 : 0);
        public void Uniform(string name, in float value) => Uniform1(GetLocation(name), value);
        public void Uniform(string name, float x, float y) => Uniform2(GetLocation(name), new Vector2(x, y));
        public void Uniform(string name, float x, float y, float z) => Uniform3(GetLocation(name), new Vector3(x, y, z));
        public void Uniform(string name, in System.Numerics.Matrix4x4 value)
        {
            var m = new OpenTK.Mathematics.Matrix4(
                value.M11, value.M12, value.M13, value.M14,
                value.M21, value.M22, value.M23, value.M24,
                value.M31, value.M32, value.M33, value.M34,
                value.M41, value.M42, value.M43, value.M44
            );

            UniformMatrix4(GetLocation(name), transpose: true, ref m);
        }

        public void Use() => UseProgram(Handle);

        private int GetLocation(string name) => GetUniformLocation(Handle, name);

        public void Dispose()
        {
            DeleteProgram(Handle);
            GC.SuppressFinalize(this);
        }
    }
}
