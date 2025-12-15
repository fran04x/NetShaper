// NetShaper.StressTest/TestServiceFactory.cs
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Composition;

namespace NetShaper.StressTest
{
    /// <summary>
    /// Factory for creating test services using dependency injection.
    /// Provides a centralized way to create properly configured instances for testing.
    /// </summary>
    internal static class TestServiceFactory
    {
        /// <summary>
        /// Creates a new IEngine instance with all required dependencies.
        /// </summary>
        /// <returns>A configured engine instance.</returns>
        public static IEngine CreateEngine()
        {
            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();
            
            return provider.GetRequiredService<IEngine>();
        }

        /// <summary>
        /// Creates a new IPacketCapture instance.
        /// </summary>
        /// <returns>A configured packet capture adapter.</returns>
        public static IPacketCapture CreatePacketCapture()
        {
            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();
            
            return provider.GetRequiredService<IPacketCapture>();
        }

        /// <summary>
        /// Creates a new IPacketLogger instance.
        /// </summary>
        /// <returns>A configured packet logger.</returns>
        public static IPacketLogger CreatePacketLogger()
        {
            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();
            
            return provider.GetRequiredService<IPacketLogger>();
        }

        /// <summary>
        /// Creates a new IEngine instance with custom logger and capture adapter.
        /// </summary>
        /// <param name="logger">The packet logger to use.</param>
        /// <param name="capture">The packet capture adapter to use.</param>
        /// <returns>A configured engine instance.</returns>
        public static IEngine CreateEngine(IPacketLogger logger, IPacketCapture capture)
        {
            // For custom instances, we need to create the engine directly
            // since we're providing specific instances rather than using DI
            return new Engine.Engine(logger, capture);
        }

        /// <summary>
        /// Creates a new EnginePool instance with N threads.
        /// </summary>
        /// <param name="threadCount">Number of concurrent processing threads (1-16).</param>
        /// <returns>A configured engine pool instance.</returns>
        public static Engine.EnginePool CreateEnginePool(int threadCount = 4)
        {
            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();
            
            var logger = provider.GetRequiredService<IPacketLogger>();
            
            // Factory to create NEW packet capture instances for each thread
            // This is critical for WinDivert load balancing (requires unique handles)
            Func<IPacketCapture> captureFactory = () => 
            {
                // Create a fresh service scope or just new instance?
                // Adapter is transient usually, but let's check DI registration
                // Safer to resolve from a new scope or new provider if specific lifecycle needed
                // But simplified: just get new instance from provider if transient
                // NetShaper.Composition likely registers it as Transient or Scoped?
                // Let's assume typical usage: create new instance manually if needed or via provider
                
                // Better approach: Create new provider scope or just new instance directly
                // given we know concrete type is WinDivertAdapter
                return new NetShaper.Native.WinDivertAdapter();
            };
            
            return new Engine.EnginePool(logger, captureFactory, threadCount);
        }
    }
}
