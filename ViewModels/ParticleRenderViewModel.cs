using System;
using System.Windows.Input;
using OpenGLOpt.Buffers;
using OpenGLOpt.Rendering;

namespace OpenGLOpt.ViewModels
{
    public class ParticleRenderViewModel : ViewModelBase
    {
        private int _particleCount = 10000;
        private double _currentFps = 0;
        private bool _useWglInterop = false;
        private bool _isRenderingEnabled = true;
        private string _statusMessage = "Initializing...";

        // Rendering components
        private SSBOManager _ssboManager;
        private TBOManager _tboManager;
        private ParticleRenderer _particleRenderer;

        public int ParticleCount
        {
            get => _particleCount;
            set
            {
                if (SetProperty(ref _particleCount, value))
                {
                    UpdateParticleCount?.Invoke(value);
                }
            }
        }

        public double CurrentFps
        {
            get => _currentFps;
            set => SetProperty(ref _currentFps, value);
        }

        public bool UseWglInterop
        {
            get => _useWglInterop;
            set => SetProperty(ref _useWglInterop, value);
        }

        public bool IsRenderingEnabled
        {
            get => _isRenderingEnabled;
            set => SetProperty(ref _isRenderingEnabled, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Events
        public event Action<int> UpdateParticleCount;
        public event Action<double> FpsUpdated;

        // Commands
        public ICommand ResetParticlesCommand { get; }
        public ICommand ToggleRenderingCommand { get; }

        public ParticleRenderViewModel()
        {
            ResetParticlesCommand = new RelayCommand(ResetParticles);
            ToggleRenderingCommand = new RelayCommand(ToggleRendering);
        }

        public void InitializeRendering()
        {
            try
            {
                _ssboManager = new SSBOManager();
                _tboManager = new TBOManager();
                _particleRenderer = new ParticleRenderer(_ssboManager, _tboManager);
                _particleRenderer.InitializeParticles(_particleCount);
                
                StatusMessage = "Rendering initialized successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Initialization error: {ex.Message}";
            }
        }

        public void Render()
        {
            try
            {
                _particleRenderer?.UpdateParticleCount(_particleCount);
                _particleRenderer?.Render();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Render error: {ex.Message}";
            }
        }

        private void ResetParticles()
        {
            try
            {
                _particleRenderer?.InitializeParticles(_particleCount);
                StatusMessage = "Particles reset successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Reset error: {ex.Message}";
            }
        }

        private void ToggleRendering()
        {
            IsRenderingEnabled = !IsRenderingEnabled;
            StatusMessage = IsRenderingEnabled ? "Rendering started" : "Rendering stopped";
        }

        public void UpdateFps(double fps)
        {
            CurrentFps = fps;
            FpsUpdated?.Invoke(fps);
        }

        public void Dispose()
        {
            _particleRenderer?.Dispose();
            _ssboManager?.Dispose();
            _tboManager?.Dispose();
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }
}