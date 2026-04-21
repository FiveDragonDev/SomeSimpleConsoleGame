using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using static OpenTK.Graphics.OpenGL4.GL;

namespace SomeSimpleConsoleGame.Core.Rendering
{
    public sealed class GLContext : IRenderContext, IDisposable
    {
        private readonly int _bufferWidth, _bufferHeight, _bufferArea;

        private readonly NativeWindow _window;
        private Shader _shader;
        private bool _ownsShader = true;

        private readonly int _vertexArrayObject, _vertexBufferObject, _depthRenderBuffer;
        private int _vertexBufferCapacityBytes;

        private readonly int _framebuffer, _renderTexture;

        private float[] _vertexData;
        private int _vertexFloatCount;

        private readonly byte[] _pixelData;
        private readonly char[] _charData;

        private const string Ramp = " .^:+1f$Zg#@";
        private readonly char[] _charLookup;

        public GLContext(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

            NativeWindowSettings nativeSettings = new()
            {
                Title = "GL Context",
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
            _pixelData = new byte[_bufferArea];
            _charData = new char[_bufferArea];
            _charData.AsSpan().Fill(' ');

            _shader = CreateDefaultShader();
            _shader.Use();

            _charLookup = new char[256];
            int maxIndex = Ramp.Length - 1;
            for (int i = 0; i < 256; i++)
            {
                float v = i / 255f;
                v = MathF.Pow(v, 0.85f);
                int index = (int)(v * maxIndex);
                if (index < 0) index = 0;
                else if (index > maxIndex) index = maxIndex;
                _charLookup[i] = Ramp[index];
            }

            Viewport(0, 0, width, height);

            Enable(EnableCap.DepthTest);
            DepthFunc(DepthFunction.Less);

            Enable(EnableCap.CullFace);
            CullFace(TriangleFace.Back);
            FrontFace(FrontFaceDirection.Cw);

            Enable(EnableCap.PolygonSmooth);
            Enable(EnableCap.LineSmooth);

            _framebuffer = GenFramebuffer();
            BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            _renderTexture = GenTexture();
            BindTexture(TextureTarget.Texture2D, _renderTexture);
            TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8,
                             width, height, 0,
                             PixelFormat.Red, PixelType.UnsignedByte, IntPtr.Zero);
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

        public void MakeCurrent() => _window.MakeCurrent();

        public void SetShader(Shader shader)
        {
            ArgumentNullException.ThrowIfNull(shader);
            _window.MakeCurrent();

            if (_ownsShader)
            {
                _shader.Dispose();
                _ownsShader = false;
            }

            _shader = shader;
            _shader.Use();
        }

        public void DrawTriangles(ReadOnlySpan<(float X, float Y, float Z, float Intensity)> vertices)
        {
            if (vertices.Length == 0) return;

            int additionalFloats = vertices.Length * 4;
            EnsureVertexCapacity(additionalFloats);

            for (int i = 0; i < vertices.Length; i++)
            {
                var (X, Y, Z, Intensity) = vertices[i];
                _vertexData[_vertexFloatCount++] = X;
                _vertexData[_vertexFloatCount++] = Y;
                _vertexData[_vertexFloatCount++] = Z;
                _vertexData[_vertexFloatCount++] = Intensity;
            }
        }
        private void DrawPrimitiveVertex(float x, float y, float z, float intensity)
        {
#if DEBUG
            if (!float.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x), "Vertex components must be finite.");
            if (!float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y), "Vertex components must be finite.");
            if (!float.IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(z), "Vertex components must be finite.");
            if (!float.IsFinite(intensity)) throw new ArgumentOutOfRangeException(nameof(intensity), "Vertex components must be finite.");
            if (intensity < 0 || intensity > 1) throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be in the range [0, 1].");
#endif

            EnsureVertexCapacity(additionalFloats: 4);
            _vertexData[_vertexFloatCount++] = x;
            _vertexData[_vertexFloatCount++] = y;
            _vertexData[_vertexFloatCount++] = z;
            _vertexData[_vertexFloatCount++] = intensity;
        }

        public void Render(ICharRenderTarget target)
        {
            _window.MakeCurrent();
            _shader.Use();

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
            ReadPixels(0, 0, _bufferWidth, _bufferHeight, PixelFormat.Red, PixelType.UnsignedByte, _pixelData);
            CheckError();

            ClearColor(0, 0, 0, 0);
            Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _vertexFloatCount = 0;

            for (int y = 0; y < _bufferHeight; y++)
            {
                int srcRow = y;
                int dstRow = _bufferHeight - 1 - y;

                int rowOffset = srcRow * _bufferWidth;
                int dstOffset = dstRow * _bufferWidth;

                for (int x = 0; x < _bufferWidth; x++)
                {
                    _charData[dstOffset + x] = _charLookup[_pixelData[rowOffset + x]];
                }
            }

            target.UpdateBackBuffer(_charData);
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
            _window.MakeCurrent();

            DeleteVertexArray(_vertexArrayObject);
            DeleteBuffer(_vertexBufferObject);
            DeleteFramebuffer(_framebuffer);
            DeleteTexture(_renderTexture);
            DeleteRenderbuffer(_depthRenderBuffer);
            if (_ownsShader) _shader.Dispose();
            _window.Dispose();
        }

        private static Shader CreateDefaultShader() =>
            new(
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
 }");
    }
}
