using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics.OpenGL4;

namespace OpenGLOpt.Interop
{
    /// <summary>
    /// Fallback implementation when WGL_NV_DX_interop is not supported
    /// Uses GL.ReadPixels to copy data from OpenGL to WPF (slower but compatible)
    /// </summary>
    public class FallbackImageSource : IDisposable
    {
        private WriteableBitmap _bitmap;
        private byte[] _pixelBuffer;
        private bool _disposed = false;

        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public ImageSource ImageSource => _bitmap;

        public FallbackImageSource(int width, int height)
        {
            Width = width;
            Height = height;
            InitializeBitmap();
        }

        private void InitializeBitmap()
        {
            // Create WriteableBitmap for WPF
            _bitmap = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgra32, null);
            _pixelBuffer = new byte[Width * Height * 4]; // BGRA
            
            Console.WriteLine($"Fallback bitmap created: {Width}x{Height}");
        }

        public void UpdateFromOpenGL()
        {
            if (_disposed || _bitmap == null) return;

            try
            {
                // Read pixels from OpenGL framebuffer
                GL.ReadPixels(0, 0, Width, Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, _pixelBuffer);
                
                // Flip image vertically (OpenGL vs WPF coordinate systems)
                FlipImageVertically(_pixelBuffer, Width, Height);
                
                // Update WriteableBitmap
                _bitmap.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _bitmap.Lock();
                        
                        // Copy pixel data
                        unsafe
                        {
                            var backBuffer = _bitmap.BackBuffer;
                            var stride = _bitmap.BackBufferStride;
                            
                            fixed (byte* srcPtr = _pixelBuffer)
                            {
                                byte* src = srcPtr;
                                byte* dst = (byte*)backBuffer;
                                
                                for (int y = 0; y < Height; y++)
                                {
                                    for (int x = 0; x < Width * 4; x++)
                                    {
                                        dst[y * stride + x] = src[y * Width * 4 + x];
                                    }
                                }
                            }
                        }
                        
                        _bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
                    }
                    finally
                    {
                        _bitmap.Unlock();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating fallback image: {ex.Message}");
            }
        }

        private void FlipImageVertically(byte[] pixels, int width, int height)
        {
            int stride = width * 4;
            byte[] temp = new byte[stride];
            
            for (int y = 0; y < height / 2; y++)
            {
                int topRowOffset = y * stride;
                int bottomRowOffset = (height - 1 - y) * stride;
                
                // Copy top row to temp
                Array.Copy(pixels, topRowOffset, temp, 0, stride);
                
                // Copy bottom row to top
                Array.Copy(pixels, bottomRowOffset, pixels, topRowOffset, stride);
                
                // Copy temp to bottom
                Array.Copy(temp, 0, pixels, bottomRowOffset, stride);
            }
        }

        public void Resize(int newWidth, int newHeight)
        {
            if (Width == newWidth && Height == newHeight) return;

            Width = newWidth;
            Height = newHeight;
            
            _bitmap?.Dispatcher.Invoke(() =>
            {
                InitializeBitmap();
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _bitmap = null;
            _pixelBuffer = null;
        }
    }
}