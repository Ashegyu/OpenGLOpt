using System;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace OpenGLOpt.Rendering
{
    /// <summary>
    /// Triple buffering system for smooth rendering without blocking
    /// Uses OpenGL framebuffers and D3D11 texture synchronization
    /// </summary>
    public class TripleBufferSystem : IDisposable
    {
        private const int BufferCount = 3;
        
        private int[] _framebuffers = new int[BufferCount];
        private int[] _colorTextures = new int[BufferCount];
        private int[] _depthTextures = new int[BufferCount];
        
        private Texture2D[] _d3dTextures = new Texture2D[BufferCount];
        private IntPtr[] _sharedHandles = new IntPtr[BufferCount];
        private IntPtr[] _glTextures = new IntPtr[BufferCount]; // WGL interop handles
        
        private Device _d3dDevice;
        private IntPtr _wglDevice;
        
        private int _currentRenderBuffer = 0;
        private int _currentDisplayBuffer = 1;
        private int _currentBackBuffer = 2;
        
        private readonly object _bufferLock = new object();
        private bool _disposed = false;

        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public int CurrentFramebuffer => _framebuffers[_currentRenderBuffer];
        public Texture2D CurrentDisplayTexture => _d3dTextures[_currentDisplayBuffer];

        public TripleBufferSystem(Device d3dDevice, int width, int height)
        {
            _d3dDevice = d3dDevice;
            Width = width;
            Height = height;
            
            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            // Initialize WGL DX interop
            Interop.WglDxInterop.Initialize();
            _wglDevice = Interop.WglDxInterop.DXOpenDevice(_d3dDevice.NativePointer);
            
            if (_wglDevice == IntPtr.Zero)
                throw new InvalidOperationException("Failed to open WGL DX interop device");

            for (int i = 0; i < BufferCount; i++)
            {
                CreateBuffer(i);
            }

            Console.WriteLine($"Triple buffer system initialized: {BufferCount} buffers, {Width}x{Height}");
        }

        private void CreateBuffer(int index)
        {
            // Create D3D11 shared texture
            var textureDesc = new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.Shared | ResourceOptionFlags.SharedKeyedmutex
            };

            _d3dTextures[index] = new Texture2D(_d3dDevice, textureDesc);
            
            // Get shared handle
            using (var resource = _d3dTextures[index].QueryInterface<SharpDX.DXGI.Resource>())
            {
                _sharedHandles[index] = resource.SharedHandle;
            }

            // Create OpenGL texture and framebuffer
            _colorTextures[index] = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTextures[index]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Width, Height, 0, 
                         PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Register with WGL interop
            _glTextures[index] = Interop.WglDxInterop.DXRegisterObject(
                _wglDevice, 
                _d3dTextures[index].NativePointer, 
                (uint)_colorTextures[index], 
                TextureTarget.Texture2D, 
                Interop.WglDxInterop.WGL_ACCESS_READ_WRITE_NV);

            if (_glTextures[index] == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to register D3D11 texture {index} with OpenGL");

            // Create depth texture
            _depthTextures[index] = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _depthTextures[index]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, Width, Height, 0, 
                         PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Create framebuffer
            _framebuffers[index] = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[index]);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, 
                                   TextureTarget.Texture2D, _colorTextures[index], 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, 
                                   TextureTarget.Texture2D, _depthTextures[index], 0);

            // Check framebuffer completeness
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException($"Framebuffer {index} is not complete");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            Console.WriteLine($"Buffer {index} created: FBO={_framebuffers[index]}, ColorTex={_colorTextures[index]}");
        }

        public void BeginRender()
        {
            lock (_bufferLock)
            {
                // Lock the current render buffer for OpenGL access
                IntPtr[] lockHandles = { _glTextures[_currentRenderBuffer] };
                if (!Interop.WglDxInterop.DXLockObjects(_wglDevice, lockHandles))
                    throw new InvalidOperationException("Failed to lock render buffer");

                // Bind render framebuffer
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[_currentRenderBuffer]);
                GL.Viewport(0, 0, Width, Height);
            }
        }

        public void EndRender()
        {
            lock (_bufferLock)
            {
                // Flush OpenGL commands
                GL.Flush();
                GL.Finish();

                // Unlock the render buffer
                IntPtr[] unlockHandles = { _glTextures[_currentRenderBuffer] };
                if (!Interop.WglDxInterop.DXUnlockObjects(_wglDevice, unlockHandles))
                    throw new InvalidOperationException("Failed to unlock render buffer");

                // Unbind framebuffer
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }

        public void SwapBuffers()
        {
            lock (_bufferLock)
            {
                // Rotate buffer indices
                int newDisplay = _currentRenderBuffer;
                int newRender = _currentBackBuffer;
                int newBack = _currentDisplayBuffer;

                _currentDisplayBuffer = newDisplay;
                _currentRenderBuffer = newRender;
                _currentBackBuffer = newBack;
            }
        }

        public void Resize(int newWidth, int newHeight)
        {
            if (Width == newWidth && Height == newHeight) return;

            Width = newWidth;
            Height = newHeight;

            // Recreate all buffers
            DisposeBuffers();
            InitializeBuffers();
        }

        private void DisposeBuffers()
        {
            for (int i = 0; i < BufferCount; i++)
            {
                if (_glTextures[i] != IntPtr.Zero)
                {
                    Interop.WglDxInterop.DXUnregisterObject(_wglDevice, _glTextures[i]);
                    _glTextures[i] = IntPtr.Zero;
                }

                if (_framebuffers[i] != 0)
                {
                    GL.DeleteFramebuffer(_framebuffers[i]);
                    _framebuffers[i] = 0;
                }

                if (_colorTextures[i] != 0)
                {
                    GL.DeleteTexture(_colorTextures[i]);
                    _colorTextures[i] = 0;
                }

                if (_depthTextures[i] != 0)
                {
                    GL.DeleteTexture(_depthTextures[i]);
                    _depthTextures[i] = 0;
                }

                _d3dTextures[i]?.Dispose();
                _d3dTextures[i] = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            DisposeBuffers();

            if (_wglDevice != IntPtr.Zero)
            {
                Interop.WglDxInterop.DXCloseDevice(_wglDevice);
                _wglDevice = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}