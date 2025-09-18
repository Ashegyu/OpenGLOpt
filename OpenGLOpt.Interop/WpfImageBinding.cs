using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenGLOpt.Rendering;

namespace OpenGLOpt.Interop;

/// <summary>
/// WPF ImageSource와 OpenGL 렌더링 결과를 바인딩하는 클래스
/// GL.ReadPixels 없이 DirectX interop 또는 PBO를 통한 효율적인 전송
/// </summary>
public class WpfImageBinding : INotifyPropertyChanged, IDisposable
{
    private readonly OpenGLRenderer _renderer;
    private readonly DirectXInterop _interop;
    private DirectXInterop.AsyncTextureTransfer? _asyncTransfer;
    private object? _imageSource;
    private bool _disposed;
    private readonly Timer _updateTimer;
    private byte[]? _lastFrameData;

    public object? ImageSource
    {
        get => _imageSource;
        private set
        {
            if (_imageSource != value)
            {
                _imageSource = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WpfImageBinding(OpenGLRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _interop = new DirectXInterop();

        // DirectX interop이 지원되지 않으면 PBO 사용
        if (!_interop.IsInitialized || !_interop.SupportsInterop)
        {
            Console.WriteLine("Using PBO for texture transfer");
            _asyncTransfer = _interop.CreateAsyncTransfer(_renderer.Width, _renderer.Height);
        }

        // 렌더러의 텍스처 업데이트 이벤트 구독
        _renderer.TextureUpdated += OnTextureUpdated;

        // 정기적으로 이미지 업데이트 (60 FPS)
        _updateTimer = new Timer(UpdateImage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16.67));
    }

    private void OnTextureUpdated(int textureId)
    {
        // 이벤트 기반 업데이트는 별도로 처리하지 않고 타이머 기반으로 통합
    }

    private void UpdateImage(object? state)
    {
        try
        {
            if (_disposed) return;

            if (_interop.IsInitialized && _interop.SupportsInterop)
            {
                // DirectX interop을 통한 고성능 전송
                UpdateImageViaDirectXInterop();
            }
            else if (_asyncTransfer != null)
            {
                // PBO를 통한 비동기 전송
                UpdateImageViaPBO();
            }
            else
            {
                // 폴백: 직접 텍스처 읽기
                UpdateImageViaDirectRead();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating image: {ex.Message}");
        }
    }

    private void UpdateImageViaDirectXInterop()
    {
        // DirectX interop을 통한 텍스처 공유
        // 실제 구현에서는 공유된 텍스처를 WPF의 D3DImage로 직접 바인딩
        Console.WriteLine("DirectX interop update (placeholder)");
        
        // 임시로 직접 읽기 방식 사용
        UpdateImageViaDirectRead();
    }

    private void UpdateImageViaPBO()
    {
        if (_asyncTransfer == null) return;

        // PBO를 통한 비동기 텍스처 읽기
        byte[]? data = _asyncTransfer.ReadTextureAsync((int)_renderer.CurrentTextureHandle);
        
        if (data != null && data.Length > 0)
        {
            _lastFrameData = data;
            CreateImageSourceFromData(data);
        }
    }

    private void UpdateImageViaDirectRead()
    {
        // 폴백 방식: 렌더러에서 직접 이미지 데이터 가져오기
        byte[] data = _renderer.GetRenderedImageData();
        
        if (data.Length > 0)
        {
            _lastFrameData = data;
            CreateImageSourceFromData(data);
        }
    }

    private void CreateImageSourceFromData(byte[] data)
    {
        try
        {
            using (var bitmap = new Bitmap(_renderer.Width, _renderer.Height, PixelFormat.Format32bppArgb))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, _renderer.Width, _renderer.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                // OpenGL 텍스처 데이터를 비트맵으로 변환 (Y축 뒤집기)
                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
                    for (int y = 0; y < _renderer.Height; y++)
                    {
                        for (int x = 0; x < _renderer.Width; x++)
                        {
                            int srcIndex = (((_renderer.Height - 1) - y) * _renderer.Width + x) * 4;
                            int dstIndex = (y * _renderer.Width + x) * 4;

                            // RGBA to BGRA 변환
                            ptr[dstIndex] = data[srcIndex + 2];     // B
                            ptr[dstIndex + 1] = data[srcIndex + 1]; // G
                            ptr[dstIndex + 2] = data[srcIndex];     // R
                            ptr[dstIndex + 3] = data[srcIndex + 3]; // A
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);

                // 비트맵을 메모리 스트림으로 변환 (WPF에서 사용 가능한 형태)
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;

                    // 메모리 스트림을 복사해서 ImageSource로 사용
                    var imageData = stream.ToArray();
                    
                    // 실제 WPF 환경에서는 BitmapImage로 변환
                    // 여기서는 시뮬레이션용으로 데이터만 저장
                    ImageSource = CreateBitmapImagePlaceholder(imageData);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating image source: {ex.Message}");
        }
    }

    /// <summary>
    /// WPF 환경에서는 실제 BitmapImage를 생성하지만, 
    /// 여기서는 플레이스홀더로 데이터 정보만 반환
    /// </summary>
    private object CreateBitmapImagePlaceholder(byte[] imageData)
    {
        return new
        {
            Width = _renderer.Width,
            Height = _renderer.Height,
            DataSize = imageData.Length,
            Format = "PNG",
            Timestamp = DateTime.Now
        };
    }

    /// <summary>
    /// 현재 프레임을 파일로 저장
    /// </summary>
    public void SaveCurrentFrame(string filename)
    {
        if (_lastFrameData != null)
        {
            CreateImageSourceFromData(_lastFrameData);
            // 실제로는 _renderer.SaveToFile을 사용하는 것이 더 효율적
            _renderer.SaveToFile(filename);
        }
    }

    /// <summary>
    /// WPF D3DImage를 위한 설정 (실제 WPF 환경에서 사용)
    /// </summary>
    public class D3DImageBinding
    {
        private readonly DirectXInterop _interop;
        private IntPtr _sharedTexture;

        public D3DImageBinding(DirectXInterop interop)
        {
            _interop = interop;
        }

        public bool SetupSharedTexture(int glTextureId, int width, int height)
        {
            if (!_interop.IsInitialized || !_interop.SupportsInterop)
                return false;

            _sharedTexture = _interop.RegisterSharedTexture(glTextureId, width, height);
            return _sharedTexture != IntPtr.Zero;
        }

        public void UpdateD3DImage(object d3dImage)
        {
            // 실제 WPF 환경에서는 D3DImage.SetBackBuffer를 호출
            if (_sharedTexture != IntPtr.Zero)
            {
                _interop.LockSharedTexture(_sharedTexture);
                
                // D3DImage 업데이트 로직
                // d3dImage.Lock();
                // d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, sharedSurface);
                // d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
                // d3dImage.Unlock();
                
                _interop.UnlockSharedTexture(_sharedTexture);
            }
        }
    }

    /// <summary>
    /// 렌더링 통계 정보
    /// </summary>
    public class RenderingStats
    {
        public double FrameRate { get; set; }
        public double AverageFrameTime { get; set; }
        public long TotalFrames { get; set; }
        public double TextureTransferTime { get; set; }
        public string TransferMethod { get; set; } = "";
    }

    private readonly RenderingStats _stats = new();
    private DateTime _lastStatsUpdate = DateTime.Now;
    private long _frameCount = 0;

    public RenderingStats GetStats()
    {
        var now = DateTime.Now;
        var elapsed = now - _lastStatsUpdate;
        
        if (elapsed.TotalSeconds >= 1.0)
        {
            _stats.FrameRate = _frameCount / elapsed.TotalSeconds;
            _stats.AverageFrameTime = elapsed.TotalMilliseconds / _frameCount;
            _stats.TotalFrames += _frameCount;
            _stats.TransferMethod = _interop.SupportsInterop ? "DirectX Interop" : "PBO Async";
            
            _frameCount = 0;
            _lastStatsUpdate = now;
        }
        
        return _stats;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _frameCount++;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateTimer?.Dispose();
            _asyncTransfer?.Dispose();
            _interop?.Dispose();
            
            if (_renderer != null)
            {
                _renderer.TextureUpdated -= OnTextureUpdated;
            }
            
            _disposed = true;
        }
    }
}