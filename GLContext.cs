using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using static OpenTK.Graphics.OpenGL4.GL;

namespace SomeSimpleConsoleGame
{
    public sealed class GLContext : IRenderContext, IDisposable
    {
        private readonly int _bufferWidth, _bufferHeight, _bufferArea;

        private readonly NativeWindow _window;
        private readonly Shader _shader;

        private readonly int _vertexArrayObject, _vertexBufferObject, _elementBufferObject, _depthRenderBuffer;
        private int _vertexBufferCapacityBytes;

        private readonly int _framebuffer, _renderTexture;

        private float[] _vertexData;
        private int _vertexFloatCount;
        private readonly float[] _pixelData;
        private readonly char[] _charData;

        private const string Ramp = " .:^+$*#";

        public GLContext(int width, int height, Shader? shader = null)
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

            _vertexData = new float[4096];
            _pixelData = new float[_bufferArea];
            _charData = new char[_bufferArea];

            _shader = shader ?? new Shader(
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
    FragColor = vColor * (vCoord.z + 0.5);
 }");
            _shader.Use();

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

            _vertexBufferCapacityBytes = _vertexData.Length * sizeof(float);
            BufferData(BufferTarget.ArrayBuffer, _vertexBufferCapacityBytes, IntPtr.Zero, BufferUsageHint.StreamDraw);

            int stride = 4 * sizeof(float);
            int offset = 0;

            // POSITION
            VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, offset);
            EnableVertexAttribArray(0);
            offset += 3 * sizeof(float);

            // INTENSITY
            VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, offset);
            EnableVertexAttribArray(1);
            offset += sizeof(float);
            CheckError();
        }

        public void DrawMesh(Mesh mesh)
        {
            var primitive = mesh.GetTriangleVertices();
            for (int i = 0; i < primitive.Length; i++)
            {
                var vertex = primitive[i];
                DrawPrimitiveVertex(vertex.X, vertex.Y, vertex.Z, 1f);
            }
        }
        public void DrawPrimitive(ReadOnlySpan<(float, float, float, float)> vertices)
        {
            if (vertices.Length == 0) return;
            foreach (var (x, y, z, intensity) in vertices)
            {
                DrawPrimitiveVertex(x, y, z, intensity);
            }
        }

        private void DrawPrimitiveVertex(float x, float y, float z, float intensity)
        {
#if DEBUG
            if (x < -1 || x > 1 || y < -1 || y > 1 || z < -1 || z > 1 || intensity < 0 || intensity > 1)
                throw new ArgumentOutOfRangeException(nameof(intensity), "Vertex components must be in the range [-1, 1] for position and [0, 1] for intensity.");
#endif

            EnsureVertexCapacity(additionalFloats: 4);
            _vertexData[_vertexFloatCount++] = x;
            _vertexData[_vertexFloatCount++] = y;
            _vertexData[_vertexFloatCount++] = z;
            _vertexData[_vertexFloatCount++] = intensity;
        }

        public (int, char[]) Render()
        {
            BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            BindVertexArray(_vertexArrayObject);
            BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            if (_vertexFloatCount > 0)
            {
                int vertexCount = _vertexFloatCount / 4;
                int sizeInBytes = _vertexFloatCount * sizeof(float);

                if (sizeInBytes > _vertexBufferCapacityBytes)
                {
                    while (_vertexBufferCapacityBytes < sizeInBytes) _vertexBufferCapacityBytes *= 2;
                    BufferData(BufferTarget.ArrayBuffer, _vertexBufferCapacityBytes, IntPtr.Zero, BufferUsageHint.StreamDraw);
                }

                BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeInBytes, _vertexData);
                DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            }

            Flush();
            CheckError();

            ReadBuffer(ReadBufferMode.ColorAttachment0);
            ReadPixels(0, 0, _bufferWidth, _bufferHeight, PixelFormat.Red, PixelType.Float, _pixelData);
            CheckError();

            ClearColor(0, 0, 0, 0);
            Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _vertexFloatCount = 0;

            int maxIndex = Ramp.Length - 1;
            for (int i = 0; i < _pixelData.Length; i++)
            {
                float v = _pixelData[i];
                if (v < 0) v = 0;
                else if (v > 1) v = 1;

                int index = (int)(v * maxIndex);
                _charData[i] = Ramp[index];
            }
            return (0, _charData);
        }

        private void EnsureVertexCapacity(int additionalFloats)
        {
            int required = _vertexFloatCount + additionalFloats;
            if (required <= _vertexData.Length) return;

            int newSize = _vertexData.Length;
            while (newSize < required) newSize *= 2;

            Array.Resize(ref _vertexData, newSize);
        }

        [Conditional("DEBUG")]
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
            _shader.Dispose();
            _window.Dispose();
        }
    }
}
