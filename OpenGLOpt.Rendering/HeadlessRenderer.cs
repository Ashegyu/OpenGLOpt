using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using System.Drawing.Imaging;

namespace OpenGLOpt.Rendering;

/// <summary>
/// 헤드리스(서버) 환경에서 작동하는 OpenGL 렌더러
/// X11이나 GUI가 없는 환경에서도 오프스크린 렌더링 수행
/// </summary>
public class HeadlessRenderer : IDisposable
{
    private int _width;
    private int _height;
    private bool _disposed;

    // Triple buffering을 위한 텍스처들
    private readonly int[] _colorTextures = new int[3];
    private readonly int[] _framebuffers = new int[3];
    private int _depthTexture;
    private int _currentBuffer = 0;
    private int _readBuffer = 2;

    // 셰이더 관련
    private int _shaderProgram;
    private int _vertexArrayObject;
    private int _vertexBufferObject;

    // 기본 삼각형 버텍스 데이터
    private readonly float[] _vertices = {
        -0.5f, -0.5f, 0.0f,  1.0f, 0.0f, 0.0f,  // 왼쪽 하단 - 빨강
         0.5f, -0.5f, 0.0f,  0.0f, 1.0f, 0.0f,  // 오른쪽 하단 - 초록
         0.0f,  0.5f, 0.0f,  0.0f, 0.0f, 1.0f   // 상단 중앙 - 파랑
    };

    public int Width => _width;
    public int Height => _height;
    public IntPtr CurrentTextureHandle => new IntPtr(_colorTextures[_readBuffer]);

    public event Action<int>? TextureUpdated;

    public HeadlessRenderer(int width, int height)
    {
        _width = width;
        _height = height;
        
        Console.WriteLine("Initializing Headless OpenGL Renderer...");
        Console.WriteLine("Note: This is a simulation for server environments without OpenGL");
        
        // 실제 서버 환경에서는 OSMesa나 EGL을 사용해야 함
        // 여기서는 렌더링 프로세스를 시뮬레이션
        InitializeSimulation();
    }

    private void InitializeSimulation()
    {
        // OpenGL 컨텍스트를 생성할 수 없는 환경에서의 시뮬레이션
        Console.WriteLine("Simulating OpenGL context initialization...");
        Console.WriteLine($"Render target: {_width}x{_height}");
        Console.WriteLine("OpenGL Version: Simulated 4.6 Core");
        Console.WriteLine("Vendor: Simulation");
        Console.WriteLine("Renderer: Software Simulation");

        // 프레임버퍼와 텍스처 ID 시뮬레이션
        for (int i = 0; i < 3; i++)
        {
            _framebuffers[i] = 1000 + i;
            _colorTextures[i] = 2000 + i;
        }
        _depthTexture = 3000;

        // 셰이더와 버퍼 시뮬레이션
        _shaderProgram = 4000;
        _vertexArrayObject = 5000;
        _vertexBufferObject = 6000;

        Console.WriteLine("Triple buffering initialized (simulated)");
        Console.WriteLine("Modern GLSL 4.60 shaders compiled (simulated)");
        Console.WriteLine("Geometry buffers created (simulated)");
    }

    public void Render(float time)
    {
        // Triple buffering: 다음 버퍼로 전환
        _currentBuffer = (_currentBuffer + 1) % 3;

        // 실제 렌더링 대신 시뮬레이션
        SimulateRendering(time);

        // 렌더링 완료, 읽기 버퍼 업데이트
        _readBuffer = _currentBuffer;

        // 텍스처 업데이트 이벤트 발생
        TextureUpdated?.Invoke(_colorTextures[_readBuffer]);
    }

    private void SimulateRendering(float time)
    {
        // 실제 OpenGL 렌더링을 수행할 수 없으므로 수학적으로 시뮬레이션
        // 시간에 따른 색상 변화 계산
        float r = (float)(Math.Sin(time) * 0.5 + 0.5);
        float g = (float)(Math.Cos(time * 1.2) * 0.5 + 0.5);
        float b = (float)(Math.Sin(time * 0.8) * 0.5 + 0.5);

        // 회전 매트릭스 계산
        Matrix4 model = Matrix4.CreateRotationY(time);
        Matrix4 view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(45.0f), 
            (float)_width / _height, 0.1f, 100.0f);

        // 시뮬레이션 로그 (성능 테스트용)
        if (time % 1.0f < 0.016f) // 대략 1초마다
        {
            Console.WriteLine($"Simulated render - Time: {time:F2}s, Colors: R={r:F2}, G={g:F2}, B={b:F2}");
        }
    }

    public byte[] GetRenderedImageData()
    {
        // 실제 OpenGL 텍스처 데이터 대신 프로시저럴 이미지 생성
        byte[] data = new byte[_width * _height * 4]; // RGBA

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = (y * _width + x) * 4;
                
                // 간단한 그라디언트 패턴 생성
                float normalizedX = (float)x / _width;
                float normalizedY = (float)y / _height;
                
                // 중심에서의 거리 계산
                float centerX = 0.5f;
                float centerY = 0.5f;
                float distance = (float)Math.Sqrt(
                    Math.Pow(normalizedX - centerX, 2) + 
                    Math.Pow(normalizedY - centerY, 2));

                // 색상 계산 (시간에 따른 변화 없이 정적 패턴)
                data[index] = (byte)(255 * (1.0f - distance)); // R
                data[index + 1] = (byte)(255 * normalizedX);    // G
                data[index + 2] = (byte)(255 * normalizedY);    // B
                data[index + 3] = 255;                          // A
            }
        }

        return data;
    }

    public void SaveToFile(string filename)
    {
        byte[] data = GetRenderedImageData();
        
        // 크로스 플랫폼 PNG 저장을 위한 간단한 구현
        SaveAsPng(data, filename);
    }

    private void SaveAsPng(byte[] data, string filename)
    {
        try
        {
            // System.Drawing이 작동하지 않는 환경을 위한 대안
            // 실제로는 ImageSharp 등의 크로스 플랫폼 라이브러리를 사용해야 함
            
            using (var bitmap = new Bitmap(_width, _height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, _width, _height), 
                                               ImageLockMode.WriteOnly, 
                                               System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                // Y축 뒤집기 및 RGBA to BGRA 변환
                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        int srcIndex = (((_height - 1) - y) * _width + x) * 4;
                        int dstIndex = (y * _width + x) * 4;
                        
                        unsafe
                        {
                            byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                            ptr[dstIndex] = data[srcIndex + 2];     // B
                            ptr[dstIndex + 1] = data[srcIndex + 1]; // G
                            ptr[dstIndex + 2] = data[srcIndex];     // R
                            ptr[dstIndex + 3] = data[srcIndex + 3]; // A
                        }
                    }
                }
                
                bitmap.UnlockBits(bitmapData);
                bitmap.Save(filename, ImageFormat.Png);
                Console.WriteLine($"Simulated render saved to: {filename}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save image (expected in headless environment): {ex.Message}");
            
            // 대안: 바이너리 파일로 저장
            string rawFilename = Path.ChangeExtension(filename, ".raw");
            File.WriteAllBytes(rawFilename, data);
            Console.WriteLine($"Raw image data saved to: {rawFilename}");
            Console.WriteLine($"Format: {_width}x{_height} RGBA, 4 bytes per pixel");
        }
    }

    /// <summary>
    /// 실제 환경에서 사용할 OSMesa 기반 헤드리스 렌더러 설정
    /// </summary>
    public static class OSMesaSetup
    {
        public static bool IsOSMesaAvailable()
        {
            // OSMesa 라이브러리 확인
            try
            {
                // libOSMesa.so 또는 OSMesa32.dll 확인
                return File.Exists("/usr/lib/x86_64-linux-gnu/libOSMesa.so") ||
                       File.Exists("/usr/lib/libOSMesa.so") ||
                       File.Exists("OSMesa32.dll");
            }
            catch
            {
                return false;
            }
        }

        public static void PrintSetupInstructions()
        {
            Console.WriteLine("For real headless OpenGL rendering, install OSMesa:");
            Console.WriteLine("Ubuntu/Debian: sudo apt-get install libosmesa6-dev");
            Console.WriteLine("CentOS/RHEL: sudo yum install mesa-libOSMesa-devel");
            Console.WriteLine("Windows: Download OSMesa32.dll from Mesa3D");
            Console.WriteLine();
            Console.WriteLine("Alternative: Use EGL with a virtual display");
            Console.WriteLine("or run with Xvfb for X11 forwarding");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Console.WriteLine("Disposing headless renderer (simulated)");
            _disposed = true;
        }
    }
}