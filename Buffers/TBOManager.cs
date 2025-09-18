using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace OpenGLOpt.Buffers
{
    /// <summary>
    /// Manages Texture Buffer Objects for 1D texture data accessible in shaders
    /// TBO provides random access to large arrays of data in shaders
    /// </summary>
    public class TBOManager : IDisposable
    {
        private int _tbo;           // Texture Buffer Object
        private int _textureId;     // Associated texture
        private int _bufferSize;
        private bool _disposed = false;

        public int TextureId => _textureId;
        public int BufferId => _tbo;

        public TBOManager()
        {
            InitializeTBO();
        }

        private void InitializeTBO()
        {
            // Generate texture buffer object
            _tbo = GL.GenBuffer();
            _textureId = GL.GenTexture();

            // Initialize with gradient/lookup table data
            InitializeGradientData();

            Console.WriteLine($"TBO initialized: Buffer ID={_tbo}, Texture ID={_textureId}");
        }

        /// <summary>
        /// Initialize TBO with color gradient data for particle rendering
        /// </summary>
        private void InitializeGradientData()
        {
            // Create a color gradient lookup table (256 entries)
            const int gradientSize = 256;
            Vector4[] gradientData = new Vector4[gradientSize];

            // Create fire-like gradient: black -> red -> orange -> yellow -> white
            for (int i = 0; i < gradientSize; i++)
            {
                float t = i / (float)(gradientSize - 1);
                
                if (t < 0.25f) // Black to Red
                {
                    float localT = t / 0.25f;
                    gradientData[i] = new Vector4(localT, 0, 0, 1);
                }
                else if (t < 0.5f) // Red to Orange  
                {
                    float localT = (t - 0.25f) / 0.25f;
                    gradientData[i] = new Vector4(1, localT * 0.5f, 0, 1);
                }
                else if (t < 0.75f) // Orange to Yellow
                {
                    float localT = (t - 0.5f) / 0.25f;
                    gradientData[i] = new Vector4(1, 0.5f + localT * 0.5f, 0, 1);
                }
                else // Yellow to White
                {
                    float localT = (t - 0.75f) / 0.25f;
                    gradientData[i] = new Vector4(1, 1, localT, 1);
                }
            }

            UpdateTBOData(gradientData);
        }

        /// <summary>
        /// Update TBO with new data
        /// </summary>
        public void UpdateTBOData(Vector4[] data)
        {
            _bufferSize = data.Length * sizeof(float) * 4; // Vector4 = 4 floats

            // Bind and upload data to buffer
            GL.BindBuffer(BufferTarget.TextureBuffer, _tbo);
            GL.BufferData(BufferTarget.TextureBuffer, _bufferSize, data, BufferUsageHint.StaticDraw);

            // Bind texture and associate with buffer
            GL.BindTexture(TextureTarget.TextureBuffer, _textureId);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, _tbo);

            // Unbind
            GL.BindTexture(TextureTarget.TextureBuffer, 0);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

            Console.WriteLine($"TBO updated: {data.Length} Vector4 entries, {_bufferSize} bytes");
        }

        /// <summary>
        /// Create animated color palette for dynamic effects
        /// </summary>
        public void UpdateAnimatedGradient(float time)
        {
            const int gradientSize = 256;
            Vector4[] gradientData = new Vector4[gradientSize];

            for (int i = 0; i < gradientSize; i++)
            {
                float t = i / (float)(gradientSize - 1);
                
                // Animated rainbow gradient
                float hue = (t + time * 0.1f) % 1.0f;
                Vector3 rgb = HsvToRgb(hue, 0.8f, 1.0f);
                
                gradientData[i] = new Vector4(rgb.X, rgb.Y, rgb.Z, 1.0f);
            }

            UpdateTBOData(gradientData);
        }

        /// <summary>
        /// Initialize procedural noise data for texture sampling
        /// </summary>
        public void InitializeNoiseData(int size = 512)
        {
            Vector4[] noiseData = new Vector4[size];
            Random random = new Random(42); // Seeded for reproducible noise

            for (int i = 0; i < size; i++)
            {
                // Generate Perlin-like noise values
                float noise1 = (float)(random.NextDouble() * 2.0 - 1.0);
                float noise2 = (float)(random.NextDouble() * 2.0 - 1.0);
                float noise3 = (float)(random.NextDouble() * 2.0 - 1.0);
                float noise4 = (float)(random.NextDouble() * 2.0 - 1.0);

                noiseData[i] = new Vector4(noise1, noise2, noise3, noise4);
            }

            UpdateTBOData(noiseData);
            Console.WriteLine($"Noise TBO initialized with {size} samples");
        }

        /// <summary>
        /// Bind TBO texture to specified texture unit
        /// </summary>
        public void BindToTextureUnit(int textureUnit = 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + textureUnit);
            GL.BindTexture(TextureTarget.TextureBuffer, _textureId);
        }

        /// <summary>
        /// Convert HSV to RGB color space
        /// </summary>
        private Vector3 HsvToRgb(float h, float s, float v)
        {
            int i = (int)(h * 6.0f);
            float f = h * 6.0f - i;
            float p = v * (1.0f - s);
            float q = v * (1.0f - f * s);
            float t = v * (1.0f - (1.0f - f) * s);

            switch (i % 6)
            {
                case 0: return new Vector3(v, t, p);
                case 1: return new Vector3(q, v, p);
                case 2: return new Vector3(p, v, t);
                case 3: return new Vector3(p, q, v);
                case 4: return new Vector3(t, p, v);
                case 5: return new Vector3(v, p, q);
                default: return new Vector3(0, 0, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_tbo != 0)
            {
                GL.DeleteBuffer(_tbo);
                _tbo = 0;
            }

            if (_textureId != 0)
            {
                GL.DeleteTexture(_textureId);
                _textureId = 0;
            }

            _disposed = true;
        }
    }
}