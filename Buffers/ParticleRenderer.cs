using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenGLOpt.Buffers;

namespace OpenGLOpt.Buffers
{
    /// <summary>
    /// Particle rendering system using SSBO and TBO
    /// </summary>
    public class ParticleRenderer : IDisposable
    {
        private SSBOManager _ssboManager;
        private TBOManager _tboManager;
        private TBOManager _noiseTexture;
        
        private int _shaderProgram;
        private int _vao;
        private int _vbo;
        private int _particleCount;
        
        private float _time = 0f;
        private bool _disposed = false;

        // Uniform locations
        private int _mvpLocation;
        private int _timeLocation;
        private int _gradientTextureLocation;
        private int _noiseTextureLocation;

        public ParticleRenderer(SSBOManager ssboManager, TBOManager tboManager)
        {
            _ssboManager = ssboManager;
            _tboManager = tboManager;
            
            InitializeRenderer();
        }

        private void InitializeRenderer()
        {
            // Create shader program
            CreateShaderProgram();
            
            // Create noise texture TBO
            _noiseTexture = new TBOManager();
            _noiseTexture.InitializeNoiseData(512);
            
            // Create VAO for instanced rendering
            CreateVertexArray();
            
            // Get uniform locations
            _mvpLocation = GL.GetUniformLocation(_shaderProgram, "mvpMatrix");
            _timeLocation = GL.GetUniformLocation(_shaderProgram, "time");
            _gradientTextureLocation = GL.GetUniformLocation(_shaderProgram, "gradientTexture");
            _noiseTextureLocation = GL.GetUniformLocation(_shaderProgram, "noiseTexture");

            Console.WriteLine("ParticleRenderer initialized successfully");
        }

        private void CreateShaderProgram()
        {
            // Load and compile shaders
            string vertexSource = LoadShaderSource("Shaders/particle.vert");
            string fragmentSource = LoadShaderSource("Shaders/particle.frag");
            
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);
            
            // Create and link program
            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);
            
            // Check linking status
            GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_shaderProgram);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }
            
            // Clean up
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            
            Console.WriteLine($"Shader program created: ID={_shaderProgram}");
        }

        private string LoadShaderSource(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load shader source from {filePath}: {ex.Message}");
            }
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            
            // Check compilation status
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                GL.DeleteShader(shader);
                throw new Exception($"{type} compilation failed: {infoLog}");
            }
            
            return shader;
        }

        private void CreateVertexArray()
        {
            // Generate VAO and VBO for quad vertices
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            
            GL.BindVertexArray(_vao);
            
            // We'll use gl_VertexID to generate quad vertices in the shader
            // So we just need a dummy VBO with instance IDs
            int[] instanceIds = new int[100000]; // Max instances
            for (int i = 0; i < instanceIds.Length; i++)
            {
                instanceIds[i] = i;
            }
            
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, instanceIds.Length * sizeof(int), instanceIds, BufferUsageHint.StaticDraw);
            
            // Set up vertex attribute (instance ID)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribIPointer(0, 1, VertexAttribIntegerType.Int, sizeof(int), 0);
            GL.VertexAttribDivisor(0, 1); // Instance attribute
            
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public void InitializeParticles(int particleCount)
        {
            _particleCount = particleCount;
            _ssboManager.InitializeParticles(particleCount);
        }

        public void UpdateParticleCount(int newCount)
        {
            if (newCount != _particleCount)
            {
                _particleCount = newCount;
                _ssboManager.InitializeParticles(newCount);
            }
        }

        public void Render()
        {
            if (_particleCount <= 0) return;
            
            // Update time
            _time += 0.016f; // ~60 FPS
            
            // Update particles
            _ssboManager.UpdateParticles(0.016f);
            
            // Update animated gradient
            _tboManager.UpdateAnimatedGradient(_time);
            
            // Use shader program
            GL.UseProgram(_shaderProgram);
            
            // Set uniforms
            Matrix4 mvp = CreateMVPMatrix();
            GL.UniformMatrix4(_mvpLocation, false, ref mvp);
            GL.Uniform1(_timeLocation, _time);
            
            // Bind textures
            GL.Uniform1(_gradientTextureLocation, 0);
            GL.Uniform1(_noiseTextureLocation, 1);
            
            _tboManager.BindToTextureUnit(0);
            _noiseTexture.BindToTextureUnit(1);
            
            // Bind SSBO
            _ssboManager.BindToShader(0);
            
            // Render particles
            GL.BindVertexArray(_vao);
            GL.DrawArraysInstanced(PrimitiveType.TriangleStrip, 0, 4, _particleCount);
            GL.BindVertexArray(0);
            
            GL.UseProgram(0);
        }

        private Matrix4 CreateMVPMatrix()
        {
            // Create a simple camera matrix
            Vector3 eye = new Vector3(0, 0, 25);
            Vector3 target = Vector3.Zero;
            Vector3 up = Vector3.UnitY;
            
            Matrix4 view = Matrix4.LookAt(eye, target, up);
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45f), 800f / 600f, 0.1f, 100f);
            
            return view * projection;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _noiseTexture?.Dispose();
            
            if (_shaderProgram != 0)
            {
                GL.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
            }
            
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }
            
            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }
            
            _disposed = true;
        }
    }
}