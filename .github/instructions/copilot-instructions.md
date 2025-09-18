# Copilot Instructions for OpenGLOpt

이 프로젝트는 OpenTK, OpenGL, DirectX를 활용한 그래픽 최적화에 중점을 둡니다. 현재 코드베이스가 매우 간소하므로, 프로젝트가 성장할 때마다 이 문서를 업데이트하세요.

## 아키텍처 및 주요 컴포넌트
- **WPF + OpenTK 통합**: WPF 애플리케이션에서 OpenTK를 사용한 OpenGL 렌더링
- **D3D11Image 기반 Interop**: D3D11Image 클래스를 통한 Direct3D 11 텍스처를 ImageSource로 노출
- **OpenGL ↔ D3D11 공유**: WGL_NV_DX_interop2 확장을 사용한 텍스처 공유
- **삼중 버퍼링**: 부드러운 렌더링을 위한 트리플 버퍼링 구현
- **VRAM 최적화**: D3D11 기반으로 메모리 누수 최소화
- 주요 파일 및 디렉터리는 프로젝트가 확장되면 추가될 예정입니다.

## 개발자 워크플로우
- 빌드, 테스트, 실행 방법은 아직 정의되어 있지 않습니다. 빌드 스크립트나 CI/CD가 도입되면 반드시 여기에 추가하세요.
- .NET 및 그래픽 디버깅에는 Visual Studio, RenderDoc 등 플랫폼별 도구를 사용하세요.

## 프로젝트 관례
- 코드 구조와 네이밍에 C# 및 .NET의 베스트 프랙티스를 따르세요.
- 그래픽 API(OpenGL, DirectX)와 최적화 대상별로 코드를 구성하세요.
- 커스텀 패턴이나 추상화가 생기면 반드시 이 문서에 기록하세요.

## 통합 포인트
- **OpenTK**: WPF 컨트롤 내에서 OpenGL 컨텍스트 생성 및 관리
- **D3D11Image**: 커뮤니티 D3D11Image 클래스 (SharpDX/HelixToolkit.WPF.SharpDX 참고)
- **OpenGL-D3D11 Interop**: WGL_NV_DX_interop2 확장을 통한 텍스처 공유
- **WPF 바인딩**: D3D11 ID3D11Texture2D를 WPF Image.Source에 직접 바인딩
- 외부 의존성: OpenTK 4.x, SharpDX, System.Windows.Interop, WGL_NV_DX_interop2
- 네이티브 라이브러리나 플랫폼별 설정이 필요하다면 반드시 이 문서에 명시하세요.

## 예시
- 주요 파일 및 디렉터리가 생성되면 여기에 경로를 기록하세요(예: `src/Rendering/`, `src/WPF/`, `src/Interop/`).
- D3D11Image 클래스 구현, WGL_NV_DX_interop2 사용법, OpenGL-D3D11 텍스처 공유 패턴 문서화
- OpenTK 컨트롤 초기화, ID3D11Texture2D 생성/관리, WPF 바인딩 패턴 기록
- 렌더링 루프, 리소스 관리, VRAM 최적화 패턴을 문서화하세요.

## 목표
- WPF 프로젝트에서 OpenGL 렌더링 결과를 ImageSource에 바인딩
- GL.ReadPixels 사용 없이 바인딩 (DirectX interop 사용)
- 삼중 버퍼링 구현
- 최신 OpenGL 기술 활용
---

**프로젝트가 성장할 때마다 이 파일을 반드시 최신화하세요.**
AI 에이전트는 워크플로우나 패턴이 불명확할 경우 사용자에게 명확한 예시나 설명을 요청하세요.