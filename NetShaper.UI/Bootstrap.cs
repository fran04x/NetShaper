// NetShaper.UI/Bootstrap.cs
using Microsoft.Extensions.DependencyInjection;
using NetShaper.Abstractions;
using NetShaper.Composition;
using NetShaper.UI.Controllers;
using NetShaper.UI.Views;

namespace NetShaper.UI
{
    /// <summary>
    /// Composition root for the NetShaper UI application.
    /// Configures dependency injection and builds the service provider.
    /// </summary>
    static class Bootstrap
    {
        /// <summary>
        /// Builds and configures the application controller with all dependencies.
        /// </summary>
        /// <returns>Configured application controller instance.</returns>
        public static IApplicationController BuildController()
        {
            var services = new ServiceCollection();
            
            // Register core NetShaper services (logger, capture, engine)
            services.AddNetShaperServices();
            
            // Register UI-specific services
            services.AddSingleton<IConsoleView, ConsoleStatsView>();
            services.AddSingleton<IApplicationController, ConsoleApplicationController>();
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Resolve the application controller from the container
            return serviceProvider.GetRequiredService<IApplicationController>();
        }
    }
}