using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace OpenGLOpt.Interop;

/// <summary>
/// OpenGL과 DirectX 간의 상호 운용성을 제공하는 클래스
/// WGL/DX interop을 통해 GL.ReadPixels 없이 텍스처 공유
/// </summary>
public class DirectXInterop : IDisposable
{
    private bool _disposed;
    private IntPtr _d3dDevice;
    private IntPtr _d3dContext;
    private IntPtr _sharedHandle;
    private int _glTexture;
    private IntPtr _wglHandle;

    // WGL/DX Interop API
    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern IntPtr wglGetCurrentContext();

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern IntPtr wglGetCurrentDC();

    // D3D11 API (Windows only)
    [DllImport("d3d11.dll", SetLastError = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int DriverType,
        IntPtr Software,
        uint Flags,
        IntPtr pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        IntPtr pFeatureLevel,
        out IntPtr ppImmediateContext);

    // WGL_NV_DX_interop API (NVIDIA extension)
    [DllImport("opengl32.dll", EntryPoint = "wglDXOpenDeviceNV", SetLastError = true)]
    private static extern IntPtr wglDXOpenDeviceNV(IntPtr dxDevice);

    [DllImport("opengl32.dll", EntryPoint = "wglDXCloseDeviceNV", SetLastError = true)]
    private static extern bool wglDXCloseDeviceNV(IntPtr hDevice);

    [DllImport("opengl32.dll", EntryPoint = "wglDXRegisterObjectNV", SetLastError = true)]
    private static extern IntPtr wglDXRegisterObjectNV(
        IntPtr hDevice,
        IntPtr dxObject,
        uint name,
        uint type,
        uint access);

    [DllImport("opengl32.dll", EntryPoint = "wglDXUnregisterObjectNV", SetLastError = true)]
    private static extern bool wglDXUnregisterObjectNV(IntPtr hDevice, IntPtr hObject);

    [DllImport("opengl32.dll", EntryPoint = "wglDXLockObjectsNV", SetLastError = true)]
    private static extern bool wglDXLockObjectsNV(IntPtr hDevice, int count, IntPtr[] hObjects);

    [DllImport("opengl32.dll", EntryPoint = "wglDXUnlockObjectsNV", SetLastError = true)]
    private static extern bool wglDXUnlockObjectsNV(IntPtr hDevice, int count, IntPtr[] hObjects);

    // Constants
    private const uint WGL_ACCESS_READ_WRITE_NV = 0x0001;
    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;

    public bool IsInitialized { get; private set; }
    public bool SupportsInterop { get; private set; }

    public DirectXInterop()
    {
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // DirectX 11 디바이스 생성
            CreateD3D11Device();
            
            // WGL/DX interop 지원 확인
            CheckInteropSupport();
            
            if (SupportsInterop)
            {
                // WGL/DX interop 초기화
                InitializeInterop();
                IsInitialized = true;
                Console.WriteLine("DirectX Interop initialized successfully");
            }
            else
            {
                Console.WriteLine("WGL/DX interop not supported on this system");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DirectX Interop initialization failed: {ex.Message}");
            IsInitialized = false;
        }
    }

    private void CreateD3D11Device()
    {
        // D3D11 디바이스 생성 (Windows에서만 작동)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            uint flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
            
            int result = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                flags,
                IntPtr.Zero,
                0,
                7, // D3D11_SDK_VERSION
                out _d3dDevice,
                IntPtr.Zero,
                out _d3dContext);

            if (result < 0)
            {
                throw new Exception($"Failed to create D3D11 device. HRESULT: 0x{result:X}");
            }
        }
        else
        {
            throw new NotSupportedException("DirectX interop is only supported on Windows");
        }
    }

    private void CheckInteropSupport()
    {
        // OpenGL 확장 확인
        string extensions = GL.GetString(StringName.Extensions);
        SupportsInterop = extensions.Contains("WGL_NV_DX_interop") || 
                         extensions.Contains("WGL_NV_DX_interop2");

        if (!SupportsInterop)
        {
            Console.WriteLine("WGL_NV_DX_interop extension not found");
            
            // 대안적인 방법들 확인
            Console.WriteLine("Available alternatives:");
            Console.WriteLine("- Using GL.ReadPixels for texture transfer");
            Console.WriteLine("- Using PBO (Pixel Buffer Objects) for async transfer");
        }
    }

    private void InitializeInterop()
    {
        if (_d3dDevice != IntPtr.Zero)
        {
            _wglHandle = wglDXOpenDeviceNV(_d3dDevice);
            if (_wglHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to open WGL/DX interop device");
            }
        }
    }

    /// <summary>
    /// OpenGL 텍스처를 DirectX와 공유 가능한 형태로 등록
    /// </summary>
    public IntPtr RegisterSharedTexture(int glTextureId, int width, int height)
    {
        if (!IsInitialized || !SupportsInterop)
        {
            return IntPtr.Zero;
        }

        try
        {
            // GL 텍스처를 WGL/DX interop에 등록
            IntPtr sharedObject = wglDXRegisterObjectNV(
                _wglHandle,
                IntPtr.Zero, // DX 리소스 (null이면 GL 텍스처 사용)
                (uint)glTextureId,
                GL_TEXTURE_2D,
                WGL_ACCESS_READ_WRITE_NV);

            if (sharedObject == IntPtr.Zero)
            {
                Console.WriteLine("Failed to register shared texture");
                return IntPtr.Zero;
            }

            return sharedObject;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering shared texture: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 공유 텍스처에 대한 액세스를 잠금
    /// </summary>
    public bool LockSharedTexture(IntPtr sharedHandle)
    {
        if (!IsInitialized || sharedHandle == IntPtr.Zero)
            return false;

        IntPtr[] handles = { sharedHandle };
        return wglDXLockObjectsNV(_wglHandle, 1, handles);
    }

    /// <summary>
    /// 공유 텍스처에 대한 액세스를 해제
    /// </summary>
    public bool UnlockSharedTexture(IntPtr sharedHandle)
    {
        if (!IsInitialized || sharedHandle == IntPtr.Zero)
            return false;

        IntPtr[] handles = { sharedHandle };
        return wglDXUnlockObjectsNV(_wglHandle, 1, handles);
    }

    /// <summary>
    /// PBO를 사용한 비동기 텍스처 전송 (fallback 방법)
    /// </summary>
    public class AsyncTextureTransfer : IDisposable
    {
        private readonly int[] _pbos = new int[2];
        private int _currentPbo = 0;
        private readonly int _width;
        private readonly int _height;
        private bool _disposed;

        public AsyncTextureTransfer(int width, int height)
        {
            _width = width;
            _height = height;
            
            GL.GenBuffers(2, _pbos);
            
            // PBO 초기화
            for (int i = 0; i < 2; i++)
            {
                GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbos[i]);
                GL.BufferData(BufferTarget.PixelPackBuffer, 
                             width * height * 4, 
                             IntPtr.Zero, 
                             BufferUsageHint.StreamRead);
            }
            
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
        }

        public byte[]? ReadTextureAsync(int textureId)
        {
            if (_disposed) return null;

            // 현재 PBO에 텍스처 데이터 읽기 시작
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbos[_currentPbo]);
            
            GL.GetTexImage(TextureTarget.Texture2D, 0, 
                          OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, 
                          PixelType.UnsignedByte, IntPtr.Zero);

            // 이전 PBO에서 데이터 읽기
            int readPbo = (_currentPbo + 1) % 2;
            GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbos[readPbo]);
            
            IntPtr bufferPtr = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);
            
            byte[]? data = null;
            if (bufferPtr != IntPtr.Zero)
            {
                data = new byte[_width * _height * 4];
                Marshal.Copy(bufferPtr, data, 0, data.Length);
                GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // PBO 스왑
            _currentPbo = (_currentPbo + 1) % 2;

            return data;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                GL.DeleteBuffers(2, _pbos);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 비동기 텍스처 전송을 위한 PBO 생성
    /// </summary>
    public AsyncTextureTransfer CreateAsyncTransfer(int width, int height)
    {
        return new AsyncTextureTransfer(width, height);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_wglHandle != IntPtr.Zero)
            {
                wglDXCloseDeviceNV(_wglHandle);
                _wglHandle = IntPtr.Zero;
            }

            // D3D 리소스 해제는 COM 참조 카운팅에 의해 자동으로 처리됨
            
            _disposed = true;
        }
    }
}