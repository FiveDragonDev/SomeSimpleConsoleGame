using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using static OpenTK.Graphics.OpenGL4.GL;

namespace SomeSimpleConsoleGame
{
    public sealed class GLContext : IDisposable
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
            offset += sizeof(byte);
            CheckError();
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

        public char[] Render()
        {
            BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            BindVertexArray(_vertexArrayObject);
            BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            if (_vertices.Count > 0)
            {
                int vertexCount = _vertices.Count / 4;
                int sizeInBytes = _vertices.Count * sizeof(float);

                BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, sizeInBytes, _vertices.ToArray());
                CheckError();

                DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
                CheckError();
            }

            Finish();
            CheckError();

            var pixelData = new byte[_bufferArea];
            ReadBuffer(ReadBufferMode.ColorAttachment0);
            ReadPixels(0, 0, _bufferWidth, _bufferHeight, PixelFormat.Red, PixelType.UnsignedByte, pixelData);
            CheckError();

            ClearColor(0, 0, 0, 0);
            Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _vertices.Clear();

            const string chars = " .:+*@#";
            Span<char> data = new char[pixelData.Length];
            for (int i = 0; i < pixelData.Length; i++)
            {
                int index = (int)(pixelData[i] / 255f * (chars.Length - 1));
                index = Math.Clamp(index, 0, chars.Length - 1);
                data[i] = chars[index];
            }
            return data.ToArray();
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
