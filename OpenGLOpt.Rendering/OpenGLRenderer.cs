using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System.Drawing;
using System.Drawing.Imaging;

namespace OpenGLOpt.Rendering;

/// <summary>
/// 최신 OpenGL 4.6 기술을 사용한 고성능 렌더링 엔진
/// Triple buffering과 DirectX interop을 지원
/// </summary>
public class OpenGLRenderer : IDisposable
{
    private GameWindow? _window;
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

    // 기본 삼각형 버텍스 데이터 (최신 OpenGL 방식)
    private readonly float[] _vertices = {
        -0.5f, -0.5f, 0.0f,  1.0f, 0.0f, 0.0f,  // 왼쪽 하단 - 빨강
         0.5f, -0.5f, 0.0f,  0.0f, 1.0f, 0.0f,  // 오른쪽 하단 - 초록
         0.0f,  0.5f, 0.0f,  0.0f, 0.0f, 1.0f   // 상단 중앙 - 파랑
    };

    public int Width => _width;
    public int Height => _height;
    public IntPtr CurrentTextureHandle => new IntPtr(_colorTextures[_readBuffer]);

    public event Action<int>? TextureUpdated;

    public OpenGLRenderer(int width, int height)
    {
        _width = width;
        _height = height;
        Initialize();
    }

    private void Initialize()
    {
        // OpenTK 윈도우 설정 (오프스크린 렌더링용)
        var nativeWindowSettings = new NativeWindowSettings()
        {
            ClientSize = new Vector2i(_width, _height),
            Title = "OpenGL Renderer",
            WindowBorder = WindowBorder.Hidden,
            WindowState = WindowState.Minimized,
            Flags = ContextFlags.ForwardCompatible,
            Profile = ContextProfile.Core,
            APIVersion = new Version(4, 6)
        };

        _window = new GameWindow(GameWindowSettings.Default, nativeWindowSettings);
        _window.MakeCurrent();

        // OpenGL 버전 확인
        string version = GL.GetString(StringName.Version);
        string vendor = GL.GetString(StringName.Vendor);
        string renderer = GL.GetString(StringName.Renderer);
        
        Console.WriteLine($"OpenGL Version: {version}");
        Console.WriteLine($"Vendor: {vendor}");
        Console.WriteLine($"Renderer: {renderer}");

        // Triple buffering용 프레임버퍼와 텍스처 생성
        GL.GenFramebuffers(3, _framebuffers);
        GL.GenTextures(3, _colorTextures);

        for (int i = 0; i < 3; i++)
        {
            // 컬러 텍스처 설정
            GL.BindTexture(TextureTarget.Texture2D, _colorTextures[i]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 
                         _width, _height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, 
                         PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // 프레임버퍼 설정
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[i]);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                   TextureTarget.Texture2D, _colorTextures[i], 0);
        }

        // Depth 텍스처 생성
        GL.GenTextures(1, out _depthTexture);
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, 
                     _width, _height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.DepthComponent, 
                     PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        // 모든 프레임버퍼에 depth 텍스처 연결
        for (int i = 0; i < 3; i++)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[i]);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                   TextureTarget.Texture2D, _depthTexture, 0);
            
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"Framebuffer {i} is not complete!");
            }
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // 현대적인 OpenGL 셰이더 초기화
        InitializeShaders();
        InitializeGeometry();

        // OpenGL 상태 설정
        GL.Enable(EnableCap.DepthTest);
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
    }

    private void InitializeShaders()
    {
        // 최신 GLSL 4.60 Core Profile 버텍스 셰이더
        string vertexShaderSource = @"
#version 460 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

out vec3 vertexColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = projection * view * model * vec4(aPosition, 1.0);
    vertexColor = aColor;
}";

        // 최신 GLSL 4.60 Core Profile 프래그먼트 셰이더
        string fragmentShaderSource = @"
#version 460 core

in vec3 vertexColor;
out vec4 FragColor;

uniform float time;

void main()
{
    // 시간에 따른 색상 변화 효과
    vec3 color = vertexColor;
    color.r = vertexColor.r * (sin(time) * 0.5 + 0.5);
    color.g = vertexColor.g * (cos(time * 1.2) * 0.5 + 0.5);
    color.b = vertexColor.b * (sin(time * 0.8) * 0.5 + 0.5);
    
    FragColor = vec4(color, 1.0);
}";

        // 셰이더 컴파일
        int vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        // 셰이더 프로그램 생성 및 링크
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);

        // 링크 상태 확인
        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(_shaderProgram);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        // 사용하지 않는 셰이더 삭제
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    private int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            throw new Exception($"Shader compilation failed ({type}): {infoLog}");
        }

        return shader;
    }

    private void InitializeGeometry()
    {
        // VAO (Vertex Array Object) 생성
        GL.GenVertexArrays(1, out _vertexArrayObject);
        GL.BindVertexArray(_vertexArrayObject);

        // VBO (Vertex Buffer Object) 생성
        GL.GenBuffers(1, out _vertexBufferObject);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

        // 위치 속성 (location = 0)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // 색상 속성 (location = 1)
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public void Render(float time)
    {
        // Triple buffering: 다음 버퍼로 전환
        _currentBuffer = (_currentBuffer + 1) % 3;

        // 현재 버퍼에 렌더링
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[_currentBuffer]);
        GL.Viewport(0, 0, _width, _height);

        // 클리어
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // 셰이더 프로그램 사용
        GL.UseProgram(_shaderProgram);

        // 유니폼 설정
        Matrix4 model = Matrix4.CreateRotationY(time);
        Matrix4 view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), 
                                                                   (float)_width / _height, 0.1f, 100.0f);

        int modelLocation = GL.GetUniformLocation(_shaderProgram, "model");
        int viewLocation = GL.GetUniformLocation(_shaderProgram, "view");
        int projectionLocation = GL.GetUniformLocation(_shaderProgram, "projection");
        int timeLocation = GL.GetUniformLocation(_shaderProgram, "time");

        GL.UniformMatrix4(modelLocation, false, ref model);
        GL.UniformMatrix4(viewLocation, false, ref view);
        GL.UniformMatrix4(projectionLocation, false, ref projection);
        GL.Uniform1(timeLocation, time);

        // 삼각형 렌더링
        GL.BindVertexArray(_vertexArrayObject);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        // 렌더링 완료, 읽기 버퍼 업데이트
        _readBuffer = _currentBuffer;

        // 텍스처 업데이트 이벤트 발생
        TextureUpdated?.Invoke(_colorTextures[_readBuffer]);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public byte[] GetRenderedImageData()
    {
        GL.BindTexture(TextureTarget.Texture2D, _colorTextures[_readBuffer]);
        
        byte[] data = new byte[_width * _height * 4]; // RGBA
        GL.GetTexImage(TextureTarget.Texture2D, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, 
                      PixelType.UnsignedByte, data);
        
        return data;
    }

    public void SaveToFile(string filename)
    {
        byte[] data = GetRenderedImageData();
        
        using (var bitmap = new Bitmap(_width, _height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, _width, _height), 
                                           ImageLockMode.WriteOnly, 
                                           System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            // OpenGL은 Y축이 뒤집어져 있으므로 이를 수정
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int srcIndex = (((_height - 1) - y) * _width + x) * 4;
                    int dstIndex = (y * _width + x) * 4;
                    
                    // RGBA to BGRA 변환
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
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            GL.DeleteTextures(3, _colorTextures);
            GL.DeleteTexture(_depthTexture);
            GL.DeleteFramebuffers(3, _framebuffers);
            GL.DeleteVertexArray(_vertexArrayObject);
            GL.DeleteBuffer(_vertexBufferObject);
            GL.DeleteProgram(_shaderProgram);

            _window?.Dispose();
            _disposed = true;
        }
    }
}