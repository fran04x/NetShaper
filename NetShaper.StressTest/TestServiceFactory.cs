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
            // For custom instances, create Engine with single thread
            Func<IPacketCapture> captureFactory = () => capture;
            return new Engine.Engine(logger, captureFactory, threadCount: 1);
        }

        /// <summary>
        /// Creates a new Engine instance with N threads using batch mode.
        /// </summary>
        public static Engine.Engine CreateEngine(int threadCount = 4)
        {
            var services = new ServiceCollection();
            services.AddNetShaperServices();
            var provider = services.BuildServiceProvider();
            
            var logger = provider.GetRequiredService<IPacketLogger>();
            
            // Factory uses interface instead of concrete type
            Func<IPacketCapture> captureFactory = () => 
                new NetShaper.Native.WinDivertAdapter();
            
            return new Engine.Engine(logger, captureFactory, threadCount);
        }
    }
}
