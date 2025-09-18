using OpenGLOpt.Rendering;
using OpenGLOpt.Interop;

namespace OpenGLOpt.WPF;

/// <summary>
/// OpenTK + DirectX + WPF 통합 데모 애플리케이션
/// 최신 OpenGL 4.6 기술과 삼중 버퍼링을 활용한 고성능 렌더링
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== OpenGL Optimization Demo ===");
        Console.WriteLine("OpenTK + DirectX Interop + Triple Buffering");
        Console.WriteLine();

        try
        {
            // 환경 감지 및 적절한 렌더러 선택
            bool useHeadless = ShouldUseHeadless();
            
            if (useHeadless)
            {
                Console.WriteLine("Running in headless mode (server environment)");
                HeadlessRenderer.OSMesaSetup.PrintSetupInstructions();
                Console.WriteLine();
                RunHeadlessDemo();
            }
            else
            {
                Console.WriteLine("Running with full OpenGL context");
                RunDemo();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static bool ShouldUseHeadless()
    {
        // DISPLAY 환경변수가 없거나 CI 환경인지 확인
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        string? ci = Environment.GetEnvironmentVariable("CI");
        
        return string.IsNullOrEmpty(display) || !string.IsNullOrEmpty(ci);
    }

    static void RunHeadlessDemo()
    {
        const int width = 800;
        const int height = 600;
        const int frameCount = 60; // 1초 @ 60fps (서버 환경에서는 짧게)

        Console.WriteLine("Initializing Headless Renderer...");
        using var renderer = new HeadlessRenderer(width, height);

        Console.WriteLine("Starting simulation...");
        Console.WriteLine($"Target: {frameCount} frames");
        Console.WriteLine();

        var startTime = DateTime.Now;
        
        for (int frame = 0; frame < frameCount; frame++)
        {
            float time = (float)(DateTime.Now - startTime).TotalSeconds;
            
            // 헤드리스 렌더링 (시뮬레이션)
            renderer.Render(time);

            // 진행 상황 표시
            if (frame % 20 == 0) // 매 20프레임마다
            {
                Console.WriteLine($"Frame {frame}/{frameCount} - Time: {time:F2}s");
                
                // 스크린샷 저장
                string filename = $"headless_frame_{frame:D3}.png";
                renderer.SaveToFile(filename);
            }

            // 시뮬레이션 딜레이
            Thread.Sleep(16); // ~60 FPS
        }

        var endTime = DateTime.Now;
        var totalTime = endTime - startTime;
        
        Console.WriteLine();
        Console.WriteLine("=== Headless Simulation Complete ===");
        Console.WriteLine($"Total Time: {totalTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Average FPS: {frameCount / totalTime.TotalSeconds:F1}");
        
        // 최종 이미지 저장
        renderer.SaveToFile("headless_final.png");
        Console.WriteLine("Final output saved");
    }

    static void RunDemo()
    {
        const int width = 800;
        const int height = 600;
        const int frameCount = 300; // 5초 @ 60fps

        Console.WriteLine("Initializing OpenGL Renderer...");
        using var renderer = new OpenGLRenderer(width, height);

        Console.WriteLine("Initializing DirectX Interop...");
        using var wpfBinding = new WpfImageBinding(renderer);

        Console.WriteLine("Starting rendering loop...");
        Console.WriteLine($"Target: {frameCount} frames");
        Console.WriteLine();

        var startTime = DateTime.Now;
        
        for (int frame = 0; frame < frameCount; frame++)
        {
            float time = (float)(DateTime.Now - startTime).TotalSeconds;
            
            // OpenGL 렌더링 (삼중 버퍼링 포함)
            renderer.Render(time);

            // 진행 상황 표시
            if (frame % 60 == 0) // 매 초마다
            {
                var stats = wpfBinding.GetStats();
                Console.WriteLine($"Frame {frame}/{frameCount} - " +
                                $"FPS: {stats.FrameRate:F1} - " +
                                $"Transfer: {stats.TransferMethod}");
                
                // 스크린샷 저장 (매 60프레임마다)
                string filename = $"frame_{frame:D4}.png";
                renderer.SaveToFile(filename);
                Console.WriteLine($"  Saved: {filename}");
            }

            // 프레임 레이트 제한 (60 FPS)
            Thread.Sleep(16); // ~60 FPS
        }

        var endTime = DateTime.Now;
        var totalTime = endTime - startTime;
        
        Console.WriteLine();
        Console.WriteLine("=== Rendering Complete ===");
        Console.WriteLine($"Total Time: {totalTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Average FPS: {frameCount / totalTime.TotalSeconds:F1}");
        
        var finalStats = wpfBinding.GetStats();
        Console.WriteLine($"Final Stats:");
        Console.WriteLine($"  Total Frames: {finalStats.TotalFrames}");
        Console.WriteLine($"  Transfer Method: {finalStats.TransferMethod}");
        Console.WriteLine($"  Avg Frame Time: {finalStats.AverageFrameTime:F2}ms");

        // 최종 이미지 저장
        renderer.SaveToFile("final_output.png");
        Console.WriteLine("Final output saved as: final_output.png");
    }
}
