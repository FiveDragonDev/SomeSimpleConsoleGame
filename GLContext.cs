using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using static OpenTK.Graphics.OpenGL4.GL;

namespace SomeSimpleConsoleGame
{
    public sealed class GLContext : IRenderContext, IDisposable
    {
        private readonly int _bufferWidth, _bufferHeight, _bufferArea;

        private readonly NativeWindow _window;

        private readonly int _vertexArrayObject, _vertexBufferObject, _elementBufferObject, _depthRenderBuffer;

        private readonly int _framebuffer, _renderTexture;

        private readonly List<float> _vertices = [];

        public GLContext(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

            NativeWindowSettings nativeSettings = new()
            {
                ClientSize = new(1, 1),
                WindowBorder = WindowBorder.Hidden,
                WindowState = WindowState.Minimized,
                StartVisible = false,
                APIVersion = new(4, 4),
                Flags = ContextFlags.Offscreen,
                StartFocused = false,
            };

            _window = new(nativeSettings);
            _window.MakeCurrent();

            _bufferWidth = width;
            _bufferHeight = height;
            _bufferArea = width * height;

            new Shader(
@"
#version 440 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in float aColor;
out vec3 vCoord;
out float vColor;
void main() {
    gl_Position = vec4(aPosition, 1.0);
    vColor = aColor;
    vCoord = aPosition.xyz;
}",
@"
#version 440 core
in vec3 vCoord;
in float vColor;
out float FragColor;
void main() {
    FragColor = vColor;
}").Use();

            Viewport(0, 0, width, height);

            Enable(EnableCap.DepthTest);
            DepthFunc(DepthFunction.Less);

            Enable(EnableCap.CullFace);
            CullFace(TriangleFace.Back);
            FrontFace(FrontFaceDirection.Cw);

            _framebuffer = GenFramebuffer();
            BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            _renderTexture = GenTexture();
            BindTexture(TextureTarget.Texture2D, _renderTexture);
            TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f,
                             width, height, 0,
                             PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                       FramebufferAttachment.ColorAttachment0,
                                       TextureTarget.Texture2D, _renderTexture, 0);

            DrawBuffer(DrawBufferMode.ColorAttachment0);

            _depthRenderBuffer = GenRenderbuffer();
            BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRenderBuffer);
            RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                                     OpenTK.Graphics.OpenGL4.RenderbufferStorage.DepthComponent24,
                                     width, height);
            FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                                         FramebufferAttachment.DepthAttachment,
                                         RenderbufferTarget.Renderbuffer, _depthRenderBuffer);

            if (CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new Exception("Framebuffer is not complete!");

            BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            _vertexArrayObject = GenVertexArray();
            BindVertexArray(_vertexArrayObject);

            _vertexBufferObject = GenBuffer();
            BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            _elementBufferObject = GenBuffer();
            BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);

            BufferData(BufferTarget.ArrayBuffer, 4 * sizeof(float) * 10 /** Enum.GetValues<PrimitiveType>().Length*/, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            int stride = 4 * sizeof(float);
            int offset = 0;

            // POSITION
            VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, offset);
            EnableVertexAttribArray(0);
            offset += 3 * sizeof(float);

            // INTENSITY
            VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, offset);
            EnableVertexAttribArray(1);
            offset += sizeof(byte); // later if we want to add more attributes, we can use the remaining byte for a flag or something
            CheckError();
        }

        public void DrawMesh(Mesh mesh)
        {
            var primitive = mesh.GetTriangleVertices();
            var convertedPrimitives = new (float, float, float, float)[primitive.Length];
            for (int i = 0; i < primitive.Length; i++)
            {
                var vertex = primitive[i];
                convertedPrimitives[i] = (vertex.X, vertex.Y, vertex.Z, 1);
            }
            DrawPrimitive(convertedPrimitives);
        }
        public void DrawPrimitive(ReadOnlySpan<(float, float, float, float)> vertices)
        {
            if (vertices.Length == 0) return;
            foreach (var (x, y, z, intensity) in vertices)
            {
                if (x < -1 || x > 1 || y < -1 || y > 1 || z < -1 || z > 1 || intensity < 0 || intensity > 1)
                    throw new ArgumentOutOfRangeException(nameof(vertices), "Vertex components must be in the range [-1, 1] for position and [0, 1] for intensity.");

                _vertices.Add(x);
                _vertices.Add(y);
                _vertices.Add(z);
                _vertices.Add(intensity);
            }
        }

        public (int, char[]) Render()
        {
            BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            BindVertexArray(_vertexArrayObject);
            BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            if (_vertices.Count > 0)
            {
                int vertexCount = _vertices.Count / 4;
                int sizeInBytes = _vertices.Count * sizeof(float);

                // BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeInBytes, _vertices.ToArray());
                BufferData(BufferTarget.ArrayBuffer, sizeInBytes, _vertices.ToArray(), BufferUsageHint.DynamicDraw);
                CheckError();
                DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            }

            Finish();
            CheckError();

            var pixelData = new float[_bufferArea];
            ReadBuffer(ReadBufferMode.ColorAttachment0);
            ReadPixels(0, 0, _bufferWidth, _bufferHeight, PixelFormat.Red, PixelType.Float, pixelData);
            CheckError();

            ClearColor(0, 0, 0, 0);
            Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _vertices.Clear();

            const string chars = " .:^+$*#";
            Span<char> data = new char[pixelData.Length];
            for (int i = 0; i < pixelData.Length; i++)
            {
                int index = (int)(pixelData[i] * (chars.Length - 1));
                index = Math.Clamp(index, 0, chars.Length - 1);
                data[i] = chars[index];
            }
            return (0, data.ToArray());
        }

        private static void CheckError()
        {
            var error = GetError();
            if (error != ErrorCode.NoError)
                throw new Exception($"OpenGL error: {error}");
        }

        public void Dispose()
        {
            DeleteVertexArray(_vertexArrayObject);
            DeleteBuffer(_vertexBufferObject);
            DeleteBuffer(_elementBufferObject);
            DeleteFramebuffer(_framebuffer);
            DeleteTexture(_renderTexture);
            DeleteRenderbuffer(_depthRenderBuffer);
            _window.Dispose();
        }
    }
}
