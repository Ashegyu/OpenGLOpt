using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace OpenGLOpt.Interop
{
    /// <summary>
    /// D3D11Image implementation for OpenGL-DirectX interop
    /// Uses WGL_NV_DX_interop2 for efficient texture sharing
    /// </summary>
    public class D3D11ImageSource : D3DImage, IDisposable
    {
        private Device _device;
        private Texture2D _renderTexture;
        private Texture2D _sharedTexture;
        private IntPtr _sharedHandle;
        private bool _disposed = false;

        public new int Width { get; private set; }
        public new int Height { get; private set; }
        
        public Device D3D11Device => _device;
        public Texture2D SharedTexture => _sharedTexture;
        public IntPtr SharedHandle => _sharedHandle;

        public D3D11ImageSource(int width, int height)
        {
            Width = width;
            Height = height;
            InitializeD3D11();
            CreateSharedTexture();
        }

        private void InitializeD3D11()
        {
            try
            {
                // Create D3D11 device with debug layer if available
                var creationFlags = DeviceCreationFlags.BgraSupport;
#if DEBUG
                creationFlags |= DeviceCreationFlags.Debug;
#endif
                
                _device = new Device(SharpDX.Direct3D.DriverType.Hardware, creationFlags);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create D3D11 device", ex);
            }
        }

        private void CreateSharedTexture()
        {
            var textureDesc = new Texture2DDescription
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.Shared | ResourceOptionFlags.SharedKeyedmutex
            };

            _sharedTexture = new Texture2D(_device, textureDesc);
            
            // Get shared handle for OpenGL interop
            using (var resource = _sharedTexture.QueryInterface<SharpDX.DXGI.Resource>())
            {
                _sharedHandle = resource.SharedHandle;
            }

            // Create render texture (non-shared for triple buffering)
            textureDesc.OptionFlags = ResourceOptionFlags.None;
            _renderTexture = new Texture2D(_device, textureDesc);

            // Set as back buffer
            Lock();
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, _sharedTexture.NativePointer);
            Unlock();
        }

        public void UpdateFromOpenGL()
        {
            if (_disposed) return;

            Lock();
            try
            {
                // Copy from render texture to shared texture
                _device.ImmediateContext.CopyResource(_renderTexture, _sharedTexture);
                
                // Signal WPF that content has changed
                AddDirtyRect(new Int32Rect(0, 0, Width, Height));
            }
            finally
            {
                Unlock();
            }
        }

        public void Resize(int newWidth, int newHeight)
        {
            if (Width == newWidth && Height == newHeight) return;

            Width = newWidth;
            Height = newHeight;

            // Dispose old textures
            _renderTexture?.Dispose();
            _sharedTexture?.Dispose();

            // Create new textures
            CreateSharedTexture();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            Lock();
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
            Unlock();

            _renderTexture?.Dispose();
            _sharedTexture?.Dispose();
            _device?.Dispose();
        }
    }
}