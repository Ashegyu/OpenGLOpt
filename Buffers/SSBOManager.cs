using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace OpenGLOpt.Buffers
{
    /// <summary>
    /// Particle data structure for SSBO
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ParticleData
    {
        public Vector3 Position;     // 12 bytes
        public float Life;           // 4 bytes
        public Vector3 Velocity;     // 12 bytes  
        public float Size;           // 4 bytes
        public Vector4 Color;        // 16 bytes
        // Total: 48 bytes (aligned to 16 bytes as required by std140)
    }

    /// <summary>
    /// Manages Shader Storage Buffer Objects for large-scale particle data
    /// </summary>
    public class SSBOManager : IDisposable
    {
        private int _ssbo;
        private int _maxParticles;
        private ParticleData[] _particles;
        private bool _disposed = false;

        public int BufferId => _ssbo;
        public int MaxParticles => _maxParticles;

        public SSBOManager()
        {
            InitializeSSBO();
        }

        private void InitializeSSBO()
        {
            // Generate SSBO
            _ssbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);

            // Set initial size for 100,000 particles
            _maxParticles = 100000;
            int bufferSize = _maxParticles * Marshal.SizeOf<ParticleData>();
            
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            
            // Bind to binding point 0
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _ssbo);
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            Console.WriteLine($"SSBO initialized: Buffer ID={_ssbo}, Max Particles={_maxParticles}, Size={bufferSize} bytes");
        }

        public void InitializeParticles(int particleCount)
        {
            if (particleCount > _maxParticles)
            {
                ResizeBuffer(particleCount);
            }

            _particles = new ParticleData[particleCount];
            var random = new Random();

            // Initialize particle data
            for (int i = 0; i < particleCount; i++)
            {
                _particles[i] = new ParticleData
                {
                    Position = new Vector3(
                        (float)(random.NextDouble() * 20 - 10), // -10 to 10
                        (float)(random.NextDouble() * 20 - 10),
                        (float)(random.NextDouble() * 20 - 10)
                    ),
                    Velocity = new Vector3(
                        (float)(random.NextDouble() * 2 - 1), // -1 to 1
                        (float)(random.NextDouble() * 2 - 1),
                        (float)(random.NextDouble() * 2 - 1)
                    ),
                    Color = new Vector4(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(), 
                        (float)random.NextDouble(),
                        1.0f
                    ),
                    Life = (float)(random.NextDouble() * 5 + 1), // 1 to 6 seconds
                    Size = (float)(random.NextDouble() * 0.5 + 0.1) // 0.1 to 0.6
                };
            }

            UpdateBuffer();
        }

        public void UpdateParticles(float deltaTime)
        {
            if (_particles == null) return;

            var random = new Random();

            // Update particles on CPU (could be moved to compute shader for better performance)
            for (int i = 0; i < _particles.Length; i++)
            {
                ref var particle = ref _particles[i];
                
                // Update position
                particle.Position += particle.Velocity * deltaTime;
                
                // Update life
                particle.Life -= deltaTime;
                
                // Respawn if dead
                if (particle.Life <= 0)
                {
                    particle.Position = new Vector3(
                        (float)(random.NextDouble() * 20 - 10),
                        (float)(random.NextDouble() * 20 - 10), 
                        (float)(random.NextDouble() * 20 - 10)
                    );
                    particle.Life = (float)(random.NextDouble() * 5 + 1);
                    particle.Color = new Vector4(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        (float)random.NextDouble(), 
                        1.0f
                    );
                }

                // Fade alpha based on life
                particle.Color.W = Math.Max(0, particle.Life / 6.0f);
            }

            UpdateBuffer();
        }

        private void UpdateBuffer()
        {
            if (_particles == null) return;

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            
            // Map buffer for writing
            IntPtr ptr = GL.MapBuffer(BufferTarget.ShaderStorageBuffer, BufferAccess.WriteOnly);
            if (ptr != IntPtr.Zero)
            {
                unsafe
                {
                    ParticleData* particlePtr = (ParticleData*)ptr;
                    for (int i = 0; i < _particles.Length; i++)
                    {
                        particlePtr[i] = _particles[i];
                    }
                }
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            }
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        private void ResizeBuffer(int newSize)
        {
            _maxParticles = Math.Max(newSize, _maxParticles * 2);
            int bufferSize = _maxParticles * Marshal.SizeOf<ParticleData>();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            Console.WriteLine($"SSBO resized: Max Particles={_maxParticles}, Size={bufferSize} bytes");
        }

        public void BindToShader(int bindingPoint = 0)
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingPoint, _ssbo);
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_ssbo != 0)
            {
                GL.DeleteBuffer(_ssbo);
                _ssbo = 0;
            }

            _disposed = true;
        }
    }
}