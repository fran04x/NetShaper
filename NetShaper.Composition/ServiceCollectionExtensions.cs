// NetShaper.Composition/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Infrastructure;
using NetShaper.Native;
using NetShaper.Rules;

namespace NetShaper.Composition
{
    /// <summary>
    /// Composition root for NetShaper dependency injection.
    /// This is the ONLY place where concrete implementations are wired to abstractions.
    /// Responsible for: RulePipeline lifecycle, Ruleset swap, IPacketCapture decoration.
    /// </summary>
    [CompositionRoot]
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all NetShaper core services including packet capture, logging, rules, and engine.
        /// Wiring hierarchy: WinDivertAdapter → RulePacketCapture → Engine
        /// </summary>
        public static IServiceCollection AddNetShaperServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            
            // Logger: singleton (maintains state across application lifetime)
            services.AddSingleton<IPacketLogger, RingBufferPacketLogger>();
            
            // RulePipeline: singleton (atomic Ruleset swap)
            services.AddSingleton<RulePipeline>();
            
            // Engine with decorated IPacketCapture
            services.AddTransient<IEngine>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<IPacketLogger>();
                var pipeline = serviceProvider.GetRequiredService<RulePipeline>();
                
                // Factory: creates decorated IPacketCapture per thread
                // WinDivertAdapter → RulePacketCapture
                Func<IPacketCapture> captureFactory = () =>
                {
                    var winDivert = new WinDivertAdapter();
                    return new RulePacketCapture(winDivert, pipeline);
                };
                
                const int threadCount = 1;
                return new Engine.Engine(logger, captureFactory, threadCount);
            });
            
            return services;
        }
        
        /// <summary>
        /// Gets the RulePipeline for ruleset configuration.
        /// Use pipeline.Swap(ruleset) to activate/deactivate rules.
        /// </summary>
        public static RulePipeline GetRulePipeline(this IServiceProvider provider)
        {
            return provider.GetRequiredService<RulePipeline>();
        }
    }
}
