using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenGLOpt.Interop;
using OpenGLOpt.Buffers;

namespace OpenGLOpt.Rendering
{
    /// <summary>
    /// WPF UserControl that integrates OpenTK with D3D11Image for high-performance rendering
    /// </summary>
    public partial class OpenTKControl : UserControl, IDisposable
    {
        private GameWindow _gameWindow;
        private D3D11ImageSource _d3dImage;
        private FallbackImageSource _fallbackImage;
        private bool _useWglInterop = false;
        private Thread _renderThread;
        private bool _isRendering = false;
        private readonly object _renderLock = new object();

        // Viewport management
        private int _viewportWidth = 800;
        private int _viewportHeight = 600;
        private bool _viewportChanged = false;

        // Performance tracking
        private Stopwatch _fpsStopwatch = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0;

        // Buffer objects
        private SSBOManager _ssboManager;
        private TBOManager _tboManager;
        private ParticleRenderer _particleRenderer;

        // Properties
        public double CurrentFps => _currentFps;
        public int ParticleCount { get; set; } = 10000;

        public OpenTKControl()
        {
            InitializeComponent();
            
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeOpenGL();
            InitializeD3DInterop();
            InitializeBuffers();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
            Dispose();
        }

        private void InitializeOpenGL()
        {
            Console.WriteLine("Starting OpenGL initialization...");
            
            // Check if GLFW is available
            try
            {
                Console.WriteLine("Checking GLFW availability...");
                var version = OpenTK.Windowing.GraphicsLibraryFramework.GLFW.GetVersionString();
                Console.WriteLine($"GLFW Version: {version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GLFW not available: {ex.Message}");
                throw;
            }
            
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new OpenTK.Mathematics.Vector2i(400, 300), // Reasonable size
                Title = "OpenGL Context",
                WindowState = OpenTK.Windowing.Common.WindowState.Normal,
                StartFocused = false,
            };

            var gameWindowSettings = new GameWindowSettings()
            {
                UpdateFrequency = 60,
            };

            try
            {
                Console.WriteLine("Creating GameWindow...");
                _gameWindow = new GameWindow(gameWindowSettings, nativeWindowSettings);
                Console.WriteLine("GameWindow created successfully");
                
                // Don't make context current here - let render thread handle it
                // This avoids context ownership conflicts
                
                Console.WriteLine("OpenGL initialization completed - context will be activated by render thread");
            }
            catch (OpenTK.Windowing.GraphicsLibraryFramework.GLFWException glfwEx)
            {
                Console.WriteLine($"GLFW Exception: {glfwEx.Message}");
                Console.WriteLine($"GLFW Error Code: {glfwEx.ErrorCode}");
                Console.WriteLine($"Stack Trace: {glfwEx.StackTrace}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General exception creating GameWindow: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
            
            if (_useWglInterop)
            {
                Console.WriteLine("Using WGL DX Interop for high-performance rendering");
                WglDxInterop.Initialize();
            }
            else
            {
                Console.WriteLine("WGL DX Interop not supported, using fallback method (GL.ReadPixels)");
            }
        }

        private void InitializeD3DInterop()
        {
            int width = (int)ActualWidth > 0 ? (int)ActualWidth : 800;
            int height = (int)ActualHeight > 0 ? (int)ActualHeight : 600;

            Image image;
            
            if (_useWglInterop)
            {
                _d3dImage = new D3D11ImageSource(width, height);
                image = new Image()
                {
                    Source = _d3dImage,
                    Stretch = System.Windows.Media.Stretch.Fill
                };
            }
            else
            {
                _fallbackImage = new FallbackImageSource(width, height);
                image = new Image()
                {
                    Source = _fallbackImage.ImageSource,
                    Stretch = System.Windows.Media.Stretch.Fill
                };
            }
            
            Content = image;
        }

        private void InitializeBuffers()
        {
            _ssboManager = new SSBOManager();
            _tboManager = new TBOManager();
            _particleRenderer = new ParticleRenderer(_ssboManager, _tboManager);

            // Initialize with default particle count
            _particleRenderer.InitializeParticles(ParticleCount);
        }

        public void StartRendering()
        {
            if (_isRendering) return;

            _isRendering = true;
            _fpsStopwatch.Start();

            _renderThread = new Thread(RenderLoop)
            {
                Name = "OpenGL Render Thread",
                IsBackground = true
            };
            _renderThread.Start();
        }

        public void StopRendering()
        {
            _isRendering = false;
            _renderThread?.Join(1000); // Wait up to 1 second
            _fpsStopwatch.Stop();
        }

        private void RenderLoop()
        {
            try
            {
                Console.WriteLine("Render thread starting...");
                
                // Make context current for this thread with retry logic
                int retryCount = 0;
                const int maxRetries = 5;
                
                while (retryCount < maxRetries)
                {
                    try
                    {
                        _gameWindow.MakeCurrent();
                        Console.WriteLine("Render thread acquired OpenGL context");
                        
                        // Initialize OpenGL settings in render thread
                        GL.Enable(EnableCap.DepthTest);
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                        GL.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);
                        
                        // Check for WGL DX interop support
                        _useWglInterop = WglDxInterop.IsSupported();
                        Console.WriteLine($"OpenGL settings initialized. WGL Interop: {_useWglInterop}");
                        
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to make context current (attempt {retryCount + 1}): {ex.Message}");
                        retryCount++;
                        
                        if (retryCount >= maxRetries)
                        {
                            Console.WriteLine("Failed to acquire OpenGL context after multiple attempts");
                            return;
                        }
                        
                        Thread.Sleep(100); // Wait before retry
                    }
                }

                while (_isRendering)
                {
                    lock (_renderLock)
                    {
                        try
                        {
                            // Update viewport if changed
                            if (_viewportChanged)
                            {
                                GL.Viewport(0, 0, _viewportWidth, _viewportHeight);
                                _viewportChanged = false;
                            }

                            // Update particle count if changed
                            _particleRenderer.UpdateParticleCount(ParticleCount);

                            // Clear
                            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                            // Render particles using SSBO/TBO
                            _particleRenderer.Render();

                            // Swap buffers and update D3D11Image
                            _gameWindow.SwapBuffers();
                            
                            // Update image source on UI thread
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (_useWglInterop)
                                {
                                    _d3dImage?.UpdateFromOpenGL();
                                }
                                else
                                {
                                    _fallbackImage?.UpdateFromOpenGL();
                                }
                            });

                            // Update FPS
                            UpdateFpsCounter();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Render error: {ex.Message}");
                        }
                    }

                    Thread.Sleep(16); // ~60 FPS
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Render loop error: {ex.Message}");
            }
            finally
            {
                // Context will be released when thread ends
                Console.WriteLine("Render loop ended");
            }
        }

        private void UpdateFpsCounter()
        {
            _frameCount++;
            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _frameCount * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            if (sizeInfo.NewSize.Width > 0 && sizeInfo.NewSize.Height > 0)
            {
                if (_useWglInterop && _d3dImage != null)
                {
                    _d3dImage.Resize((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
                }
                else if (!_useWglInterop && _fallbackImage != null)
                {
                    _fallbackImage.Resize((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
                }
                
                // Store viewport size for render thread to use
                _viewportWidth = (int)sizeInfo.NewSize.Width;
                _viewportHeight = (int)sizeInfo.NewSize.Height;
                _viewportChanged = true;
            }
        }

        /// <summary>
        /// Check if OpenGL context is ready for GL calls
        /// </summary>
        private bool IsOpenGLReady()
        {
            return _gameWindow != null;
        }

        public void Dispose()
        {
            StopRendering();
            
            _particleRenderer?.Dispose();
            _ssboManager?.Dispose();
            _tboManager?.Dispose();
            _d3dImage?.Dispose();
            _fallbackImage?.Dispose();
            _gameWindow?.Dispose();
        }
    }
}