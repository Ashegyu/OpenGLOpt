# OpenGLOpt - OpenTK + OpenGL + DirectX Optimization

OpenGLOpt is a C#/.NET graphics optimization project using OpenTK for OpenGL and DirectX functionality. This repository focuses on performance optimization techniques for real-time graphics applications.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Initial Setup and Dependencies
- **Bootstrap the repository**:
  - `dotnet restore` -- takes 1-2 seconds for existing projects
  - For new projects: `dotnet new sln -n OpenGLOpt` -- takes < 1 second
  - Create projects: `dotnet new console -n [ProjectName]` -- takes 1-2 seconds each
  - Add to solution: `dotnet sln add [ProjectName]`

- **Install essential packages**:
  - OpenTK graphics library: `dotnet add package OpenTK` -- takes 5-6 seconds. NEVER CANCEL.
  - Performance benchmarking: `dotnet add package BenchmarkDotNet` -- takes 6-8 seconds. NEVER CANCEL.
  - Testing framework is usually already included in test projects

### Build and Compilation
- **Clean build process**:
  - `dotnet clean` -- takes < 1 second
  - `dotnet build` -- takes 2-10 seconds for Debug, 1-3 seconds for Release. NEVER CANCEL.
  - `dotnet build --configuration Release` -- for optimized builds
  - Full solution build typically takes 2-10 seconds depending on project complexity

### Testing
- **Run unit tests**: 
  - `dotnet test` -- takes 5-7 seconds. NEVER CANCEL. Set timeout to 30+ seconds for safety.
  - Tests include xUnit framework for C# projects
  - Always run tests after graphics-related changes to ensure rendering pipeline integrity

### Code Quality and Formatting
- **Setup formatting tools**:
  - `dotnet new tool-manifest` -- creates tool manifest if not present
  - `dotnet tool install dotnet-format` -- installs code formatter
  - `dotnet format --verify-no-changes` -- takes 10-12 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
  - `dotnet format` -- automatically fixes formatting issues

### Running Applications
- **Graphics applications**:
  - `dotnet run` -- takes 1-2 seconds for console apps
  - Graphics applications will create OpenGL/OpenTK windows (cannot interact with UI in headless environments)
  - Always test OpenGL context creation and basic rendering after changes

## Project Structure Recommendations

### Typical Directory Layout
```
OpenGLOpt/
├── OpenGLOpt.sln
├── OpenGLOpt.Core/           # Main graphics optimization library
├── OpenGLOpt.Benchmarks/    # Performance benchmarking projects
├── OpenGLOpt.Tests/          # Unit and integration tests
├── OpenGLOpt.Samples/        # Example applications and demos
└── docs/                     # Documentation and research notes
```

### Essential Files to Monitor
- `*.csproj` files - project configuration and package references
- `Program.cs` files - application entry points
- Any files with OpenTK, OpenGL, or DirectX rendering code

## Validation Scenarios

### CRITICAL: Always Test Graphics Functionality
After making any changes to graphics-related code:

1. **Build validation**: `dotnet build` must complete without warnings
2. **Test suite**: `dotnet test` must pass all tests
3. **Graphics context test**: Run sample applications to verify:
   - OpenGL context creation succeeds
   - Basic rendering pipeline works
   - No graphics driver compatibility issues
4. **Performance validation**: Run benchmarks after optimization changes using BenchmarkDotNet

### Performance Testing Workflow
- Use `OpenGLOpt.Benchmarks` project for performance measurements
- `dotnet run --configuration Release` in benchmarks project for accurate timing
- Always compare before/after performance when making optimization changes

## Common Graphics Development Tasks

### Adding New Graphics Features
1. Implement in `OpenGLOpt.Core` project first
2. Add corresponding tests in `OpenGLOpt.Tests`
3. Create performance benchmarks in `OpenGLOpt.Benchmarks`
4. Add usage examples in `OpenGLOpt.Samples`

### Debugging Graphics Issues
- Check OpenGL version compatibility: use `GL.GetString(StringName.Version)` in code
- Verify graphics drivers: use `GL.GetString(StringName.Renderer)` 
- Monitor for OpenGL errors in rendering loops
- Test on different graphics hardware configurations when possible

### OpenTK-Specific Considerations
- OpenTK applications require OpenGL context for graphics operations
- Test window creation, rendering loops, and input handling
- Validate cross-platform compatibility (Windows, Linux, macOS)
- Monitor GPU memory usage in optimization scenarios

## Timeout and Timing Expectations

**NEVER CANCEL** any of these operations before the specified timeouts:

- `dotnet restore`: Set timeout to 60+ seconds (usually completes in 1-2 seconds)
- `dotnet build`: Set timeout to 60+ seconds (usually completes in 2-10 seconds)  
- `dotnet test`: Set timeout to 120+ seconds (usually completes in 5-7 seconds)
- `dotnet format --verify-no-changes`: Set timeout to 60+ seconds (usually completes in 10-12 seconds)
- `dotnet add package [PackageName]`: Set timeout to 120+ seconds (usually completes in 5-8 seconds)

**Graphics applications may take longer to initialize due to graphics driver loading and OpenGL context creation.**

## Dependencies and Requirements

### .NET Requirements
- **.NET 8.0** is confirmed working in this environment
- Uses modern C# language features for performance optimization

### Key Package Ecosystem
- **OpenTK 4.9.4**: Primary OpenGL/graphics library
- **BenchmarkDotNet**: Performance measurement and optimization
- **xUnit**: Testing framework for unit and integration tests
- **System.Runtime.CompilerServices.Unsafe**: For low-level optimizations

### Platform Considerations  
- Cross-platform: Windows, Linux, macOS
- OpenGL drivers required for graphics functionality
- Some graphics features may be platform-specific (DirectX on Windows)

## CI/CD and Quality Checks

### Pre-commit Validation
Always run these commands before committing changes:
1. `dotnet format --verify-no-changes` -- ensures code formatting standards
2. `dotnet build --configuration Release` -- verifies release build works
3. `dotnet test` -- ensures all tests pass

### Build Pipeline Expectations
- All builds should complete without warnings
- Tests must pass on multiple platforms
- Performance benchmarks should not regress significantly
- Graphics applications should initialize successfully on target platforms

## Troubleshooting Common Issues

### Graphics Driver Issues
- **Problem**: OpenGL context creation fails
- **Solution**: Check graphics driver compatibility, try different OpenGL versions

### Package Restore Failures  
- **Problem**: NuGet package downloads fail
- **Solution**: Clear NuGet cache with `dotnet nuget locals all --clear`, then retry restore

### Performance Regressions
- **Problem**: Optimization changes reduce performance
- **Solution**: Use BenchmarkDotNet to measure before/after performance, profile with graphics debugging tools

### Build Failures
- **Problem**: Compilation errors in graphics code
- **Solution**: Check OpenTK version compatibility, verify OpenGL function usage, ensure proper using statements

Remember: Graphics optimization is iterative. Always measure performance before and after changes, and validate functionality across different graphics hardware configurations.