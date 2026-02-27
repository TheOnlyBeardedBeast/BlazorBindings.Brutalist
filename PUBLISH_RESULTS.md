# Brutalis.Sample Published Build Comparison

## AOT Build (net9.0-osx-arm64)

- **Published Size**: 43 MB
- **Executable Size**: 5.9 MB
- **Key Components**:
  - BrutalisSample (AOT native executable): 5.9 MB
  - libSkiaSharp.dylib: 15 MB
  - libyoga.dylib: 178 KB
  - libglfw.3.dylib: 257 KB
  - Debug symbols (.dSYM): included
  - PDB files included
- **Build Time**: 28.7 seconds
- **Status**: Published successfully but app exits early on startup (possible AOT incompatibilities)

## Standard JIT Build (net9.0)

- **Published Size**: 54 MB
- **Executable Size**: 122 KB (managed executable, runs on .NET runtime)
- **Key Components**:
  - BrutalisSample.exe: 122 KB (managed entry point)
  - Managed assemblies (DLLs): ~390 KB combined
  - OpenTK.Graphics.dll: 4.9 MB (largest managed assembly)
  - SkiaSharp.dll: 478 KB
  - Yoga-CS.dll: 20 KB
  - YogaSharp.dll: 26 KB
  - runtimes/ folder with native bindings
- **Build Time**: 3.3 seconds
- **Status**: Published successfully

## Key Differences

| Metric         | AOT                   | JIT                        |
| -------------- | --------------------- | -------------------------- |
| Total Size     | 43 MB                 | 54 MB                      |
| Executable     | 5.9 MB (native)       | 122 KB (managed)           |
| Build Time     | 28.7s                 | 3.3s                       |
| Startup        | Fast (native code)    | Moderate (JIT compilation) |
| Runtime Deps   | .dylib files only     | .NET runtime required      |
| Memory Profile | TBD (app exits early) | TBD (needs GUI)            |

## Runtime Resource Usage (JIT Build)

- **Memory**: 250 MB
- **CPU**: 38%

The app runs well with moderate resource consumption. This is typical for a Blazor app with Skia rendering.

## Observations

1. **AOT vs JIT Trade-offs**:
   - AOT: Larger executable (5.9 MB), faster build compilation, no JIT needed, but potential compatibility issues
   - JIT: Smaller executable (122 KB), requires .NET runtime, slower startup due to JIT

2. **Native Dependencies**:
   - Both builds include large native libraries (SkiaSharp, Yoga, OpenTK)
   - These account for most of the published size (~20 MB combined)

3. **AOT Challenges**:
   - The app exits early - likely due to reflection/dynamic code issues
   - TrimWarnings from OpenTK.Windowing.Desktop and Microsoft.AspNetCore.Components
   - Would need additional RD.xml configuration for AOT compatibility

4. **Build Location**:
   - AOT: `/bin/Release/net9.0/osx-arm64/publish/`
   - JIT: `/bin/Release/net9.0/publish/`

## Recommendations

- Use **JIT build** for development/testing due to quick compilation
- For production, **AOT needs additional trimming/AOT configuration** to fix the early exit
- Consider adding `<InvariantGlobalization>true</InvariantGlobalization>` or other AOT compatibility settings
- The app needs GUI environment to test properly (requires windowed display)
