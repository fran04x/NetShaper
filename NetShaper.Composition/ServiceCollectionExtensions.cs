// NetShaper.Composition/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Infrastructure;
using NetShaper.Native;

namespace NetShaper.Composition
{
    /// <summary>
    /// Composition root for NetShaper dependency injection.
    /// This is the ONLY place where concrete implementations are wired to abstractions.
    /// </summary>
    [CompositionRoot]
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all NetShaper core services including packet capture, logging, and engine.
        /// This method wires all concrete implementations to their abstractions.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddNetShaperServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            // Register packet logger as singleton (maintains state across application lifetime)
            services.AddSingleton<IPacketLogger, RingBufferPacketLogger>();
            
            // Register packet capture as transient (new instance per engine)
            services.AddTransient<IPacketCapture, WinDivertAdapter>();
            
            // Register Engine as IEngine
            // Factory pattern: creates fresh IPacketCapture instance per thread
            services.AddTransient<IEngine>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<IPacketLogger>();
                
                // Factory to create IPacketCapture instances (one per thread in pool)
                Func<IPacketCapture> captureFactory = () => 
                    serviceProvider.GetRequiredService<IPacketCapture>();
                
                // Default: 1 thread
                // Future: make configurable via settings
                const int threadCount = 1;
                
                return new Engine.Engine(logger, captureFactory, threadCount);
            });
            
            return services;
        }
    }
}
