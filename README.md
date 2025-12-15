# NetShaper

**High-performance, zero-allocation network packet interception and shaping library for .NET 8**

NetShaper is a production-grade packet processing engine built on WinDivert. It is designed for enterprise firewalls, high-throughput traffic analysis, and network security applications where performance and stability are critical.

## ✨ Features

- **🚀 Zero-Allocation Hot Path**: No GC pressure during the packet processing loop.
- **🔒 Lock-Free Architecture**: Thread-safe by design without using mutexes or locks.
- **⚡ High Throughput**: Optimized for millions of packets per second.
- **🛡️ Battle-Tested**: Validated with 10,000+ stability cycles and 64-worker chaos testing.
- **🎯 Clean Architecture**: Follows SOLID principles and is ready for dependency injection.
- **📊 Production Ready**: Tier 1 robustness comparable to foundational libraries like Kestrel or NGINX.
- **✍️ Self-Documenting & Normative**: Includes a custom Roslyn analyzer (`NetShaper.Normative`) to enforce architectural rules at compile time.

## 🏗️ Project Structure

| Project | Purpose |
|---|---|
| `NetShaper.Abstractions` | Defines the core interfaces and data contracts for the engine. |
| `NetShaper.Engine` | The zero-allocation, high-performance packet processing core. **Strict rules apply here.** |
| `NetShaper.Native` | P/Invoke interop layer for the underlying WinDivert driver, using `LibraryImport`. |
| `NetShaper.Infrastructure` | Provides supporting services like logging and diagnostics. |
| `NetShaper.Composition` | Handles dependency injection setup and service composition. |
| `NetShaper.Domain` | Contains business logic, domain models, and use cases. |
| `NetShaper.Normative` | **Roslyn Analyzer** that enforces architectural rules (e.g., no allocations in Engine). |
| `NetShaper.UI` | An example WPF application demonstrating engine usage. |
| `NetShaper.StressTest` | A console app for running stability and chaos tests. |
| `NetShaper.Benchmarks` | Contains performance benchmarks for the engine. |

## 🚦 Build & Run

This project includes a custom Roslyn analyzer that enforces strict architectural rules. Due to aggressive caching in .NET, it's **highly recommended** to perform a full clean build if you encounter strange compilation errors.

```bash
# 1. Clean the solution (removes bin/obj folders)
# Use Git Bash or WSL on Windows
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +

# 2. Build the solution in Release mode
dotnet build -c Release

# 3. Run the UI (requires administrator privileges)
dotnet run --project NetShaper.UI

# 4. Run Stress Tests
# Run stability tests (10k cycles)
dotnet run --project NetShaper.StressTest -- 1
# Run chaos tests (64 workers)
dotnet run --project NetShaper.StressTest -- 3
```

## ⚠️ Troubleshooting: Analyzer Caching Issues

You may encounter a situation where the build fails with an error like:
> `CSC : error AD0001: Analyzer 'NetShaper.Analyzers.NetShaperNormativeAnalyzer' threw an exception ... Reported diagnostic has an ID 'R7.01', which is not a valid identifier.`

This error occurs when the .NET build system uses a stale, cached version of the `NetShaper.Normative.dll` analyzer, even after you've made corrections to the code.

To resolve this, you must perform a full, aggressive clean:

```bash
# 1. Shutdown the .NET build server to release any in-memory cache
dotnet build-server shutdown

# 2. Manually delete all bin and obj folders from the entire solution
# (Run this from the root directory)
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +

# 3. Clear all local NuGet package caches
dotnet nuget locals all --clear

# 4. Rebuild the project
dotnet build -c Release
```

This ensures that all cached artifacts are removed and the compiler uses the latest version of the analyzer.

## 🎓 Use Cases

- Enterprise firewalls and traffic filtering systems.
- Network security monitoring (NSM) and analysis tools.
- Traffic shaping and Quality of Service (QoS) enforcement.
- Deep Packet Inspection (DPI) engines.
- Network debugging and diagnostic utilities.

## 📝 License

[Add your license here]

## 🤝 Contributing

Contributions are welcome! Please ensure:
- All tests pass (stability + chaos).
- Code adheres to the existing architectural patterns.
- **No allocations** are introduced into the `NetShaper.Engine` hot path. The analyzer will enforce this.
- Public APIs are documented with XML comments.

---

**Built with ❤️ using .NET 8 and modern C# practices**
