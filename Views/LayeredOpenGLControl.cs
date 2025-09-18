using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Wpf;
using OpenGLOpt.ViewModels;
using OpenGLOpt.Buffers;
using OpenGLOpt.Rendering;

namespace OpenGLOpt.Views
{
    /// <summary>
    /// Multi-layer OpenGL WPF Control using GLWpfControl with MVVM pattern
    /// </summary>
    public partial class LayeredOpenGLControl : UserControl, IDisposable
    {
        private GLWpfControl _glControl;
        private Canvas _canvas;
        private Dictionary<int, Image> _layers;
        private Dictionary<int, WriteableBitmap> _layerImages;
        private readonly int _layerCount = 2;

        // Rendering components
        private SSBOManager _ssboManager;
        private TBOManager _tboManager;
        private ParticleRenderer _particleRenderer;
        private bool _isInitialized = false;

        // ViewModel (will be set from MainWindow)
        public ParticleRenderViewModel ViewModel => DataContext as ParticleRenderViewModel;

        public LayeredOpenGLControl()
        {
            InitializeComponent();
            InitializeOpenGLControl();
        }

        private void InitializeOpenGLControl()
        {
            // Create GLWpfControl
            _glControl = new GLWpfControl();
            
            GLWpfControlSettings mainSettings = new GLWpfControlSettings
            {
                MajorVersion = 4,
                MinorVersion = 6,
                Profile = OpenTK.Windowing.Common.ContextProfile.Core,
                RenderContinuously = true,
            };

            _glControl.Start(mainSettings);

            // Setup event handlers
            _glControl.Ready += OnGLControlReady;
            _glControl.Render += OnGLControlRender;
            _glControl.SizeChanged += OnGLControlSizeChanged;

            // Create layered canvas structure
            _canvas = new Canvas();
            _layers = new Dictionary<int, Image>();
            _layerImages = new Dictionary<int, WriteableBitmap>();

            for (int i = 0; i < _layerCount; i++)
            {
                WriteableBitmap bmp = null;
                var img = new Image()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Stretch = Stretch.None
                };
                _layers.Add(i, img);
                _layerImages.Add(i, bmp);
                _canvas.Children.Add(img);
            }

            // Apply Y-axis flip for OpenGL coordinate system
            _layers[0].RenderTransform = new ScaleTransform(1.0, -1.0);
            _layers[0].RenderTransformOrigin = new Point(0.0, 0.5);
            _layers[1].RenderTransform = new ScaleTransform(1.0, -1.0);
            _layers[1].RenderTransformOrigin = new Point(0.0, 0.5);

            // Add both GLWpfControl and layered canvas
            var mainGrid = new Grid();
            mainGrid.Children.Add(_glControl);
            mainGrid.Children.Add(_canvas);
            
            Content = mainGrid;
        }

        private void OnGLControlReady()
        {
            try
            {
                Console.WriteLine("GLWpfControl is ready");
                
                // Initialize OpenGL settings
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);

                // Initialize rendering components
                _ssboManager = new SSBOManager();
                _tboManager = new TBOManager();
                _particleRenderer = new ParticleRenderer(_ssboManager, _tboManager);
                
                // Initialize particles
                _particleRenderer.InitializeParticles(ViewModel?.ParticleCount ?? 10000);
                
                _isInitialized = true;
                if (ViewModel != null)
                {
                    ViewModel.StatusMessage = "OpenGL initialized successfully";
                }
                Console.WriteLine("Rendering components initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenGL initialization error: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.StatusMessage = $"Initialization error: {ex.Message}";
                }
            }
        }

        private void OnGLControlRender(TimeSpan deltaTime)
        {
            if (!_isInitialized)
            {
              OnGLControlReady();
            }
            if (!_isInitialized || !ViewModel.IsRenderingEnabled) return;

            try
            {
                // Clear the screen
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // Update particle count if changed
                _particleRenderer.UpdateParticleCount(ViewModel?.ParticleCount ?? 10000);

                // Render particles using SSBO/TBO
                _particleRenderer.Render();

                // Update FPS in ViewModel
                if (ViewModel != null)
                {
                    var fps = 1.0 / deltaTime.TotalSeconds;
                    ViewModel.UpdateFps(fps);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Render error: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.StatusMessage = $"Render error: {ex.Message}";
                }
            }
        }

        private void OnGLControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized) return;

            // Update viewport
            GL.Viewport(0, 0, (int)e.NewSize.Width, (int)e.NewSize.Height);
            
            // Update layer images if needed
            UpdateLayerSizes((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        private void UpdateLayerSizes(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            for (int i = 0; i < _layerCount; i++)
            {
                if (_layerImages[i] == null || 
                    _layerImages[i].PixelWidth != width || 
                    _layerImages[i].PixelHeight != height)
                {
                    _layerImages[i] = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                    _layers[i].Source = _layerImages[i];
                }
            }
        }

        public void StartRendering()
        {
            if (ViewModel != null)
            {
                ViewModel.IsRenderingEnabled = true;
            }
        }

        public void StopRendering()
        {
            if (ViewModel != null)
            {
                ViewModel.IsRenderingEnabled = false;
            }
        }

        public void Dispose()
        {
            _particleRenderer?.Dispose();
            _ssboManager?.Dispose();
            _tboManager?.Dispose();
            _glControl?.Dispose();
        }
    }
}