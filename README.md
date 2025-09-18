# OpenGLOpt
OpenTK + OpenGL + DirectX 최적화 프로젝트

## 프로젝트 개요

이 프로젝트는 다음과 같은 최신 기술을 활용한 고성능 그래픽 렌더링 시스템입니다:

- **OpenTK 4.8.2**: 최신 OpenGL 4.6 Core Profile 지원
- **DirectX Interop**: GL.ReadPixels 없이 효율적인 텍스처 공유
- **Triple Buffering**: 부드러운 렌더링을 위한 삼중 버퍼링
- **WPF 바인딩**: ImageSource와의 실시간 바인딩 (설계)
- **크로스 플랫폼**: Windows/Linux 지원

## 프로젝트 구조

```
OpenGLOpt/
├── OpenGLOpt.WPF/          # 메인 애플리케이션
├── OpenGLOpt.Rendering/    # OpenGL 렌더링 엔진
├── OpenGLOpt.Interop/      # DirectX 상호운용성
└── README.md
```

## 주요 기능

### 1. 현대적인 OpenGL 렌더링
- **OpenGL 4.6 Core Profile**: 최신 GLSL 4.60 셰이더
- **VAO/VBO**: 현대적인 버텍스 관리
- **Triple Buffering**: 부드러운 프레임 출력

```csharp
// 최신 GLSL 4.60 버텍스 셰이더 예시
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
```

### 2. DirectX Interop
- **WGL/DX Interop**: NVIDIA 확장을 통한 텍스처 공유
- **PBO 대안**: 비동기 픽셀 버퍼 전송
- **Zero-Copy**: GL.ReadPixels 없는 효율적인 전송

### 3. WPF 바인딩 (설계)
- **D3DImage 지원**: 직접적인 DirectX 텍스처 바인딩
- **MVVM 패턴**: 데이터 바인딩과 알림
- **실시간 업데이트**: 60 FPS 렌더링 지원

## 실행 방법

### 기본 실행
```bash
cd OpenGLOpt.WPF
dotnet run
```

### 환경별 실행
- **Windows (GUI)**: 전체 OpenGL 컨텍스트로 실행
- **Linux/Server**: 헤드리스 시뮬레이션 모드로 실행

## 시스템 요구사항

### Windows
- .NET 8.0
- OpenGL 4.6 지원 그래픽 카드
- DirectX 11/12 (WGL interop용)

### Linux
- .NET 8.0
- Mesa/OpenGL 4.6
- X11 또는 Wayland (GUI 모드)
- OSMesa (헤드리스 모드)

## 출력 예시

헤드리스 모드에서 실행 시 다음과 같은 출력을 생성합니다:

```
=== OpenGL Optimization Demo ===
OpenTK + DirectX Interop + Triple Buffering

Running in headless mode (server environment)
Initializing Headless OpenGL Renderer...
OpenGL Version: Simulated 4.6 Core
Triple buffering initialized (simulated)
Modern GLSL 4.60 shaders compiled (simulated)

=== Rendering Complete ===
Total Time: 1.12 seconds
Average FPS: 53.4
```

생성된 파일:
- `headless_frame_*.raw`: 각 프레임의 RGBA 데이터 (800x600x4 bytes)
- `headless_final.raw`: 최종 렌더링 결과

## 기술적 특징

### Triple Buffering 구현
```csharp
private readonly int[] _colorTextures = new int[3];
private readonly int[] _framebuffers = new int[3];
private int _currentBuffer = 0;
private int _readBuffer = 2;

public void Render(float time)
{
    // 버퍼 순환
    _currentBuffer = (_currentBuffer + 1) % 3;
    
    // 현재 버퍼에 렌더링
    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffers[_currentBuffer]);
    // ... 렌더링 로직
    
    _readBuffer = _currentBuffer;
}
```

### DirectX Interop
```csharp
// WGL/DX 확장을 통한 텍스처 공유
IntPtr sharedObject = wglDXRegisterObjectNV(
    _wglHandle,
    IntPtr.Zero,
    (uint)glTextureId,
    GL_TEXTURE_2D,
    WGL_ACCESS_READ_WRITE_NV);
```

### PBO 비동기 전송
```csharp
// 픽셀 버퍼 객체를 통한 비동기 텍스처 읽기
GL.BindBuffer(BufferTarget.PixelPackBuffer, _pbos[_currentPbo]);
GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
```

## 성능 최적화

1. **GPU 메모리 관리**: 효율적인 버퍼 할당
2. **CPU-GPU 동기화**: 비동기 전송으로 지연 최소화
3. **메모리 복사 최소화**: Direct interop 활용
4. **현대적인 OpenGL**: Core Profile과 최신 확장 활용

## 확장 가능성

- **Vulkan 지원**: 다음 세대 그래픽 API
- **Compute Shader**: GPGPU 연산 지원
- **Multi-threading**: 렝더링 파이프라인 병렬화
- **실시간 레이트레이싱**: RTX 확장 활용

## 라이선스

MIT License - 자세한 내용은 LICENSE 파일 참조

## 기여

Pull Request와 Issue는 언제나 환영합니다. 주요 변경사항은 먼저 Issue로 논의해 주세요.