// NetShaper.UI/Views/IConsoleView.cs
namespace NetShaper.UI.Views
{
    /// <summary>
    /// Interface for console UI rendering.
    /// Separates presentation logic from business logic (SRP).
    /// </summary>
    public interface IConsoleView
    {
        /// <summary>
        /// Updates the statistics display in the console.
        /// </summary>
        /// <param name="packetsPerSecond">Current packets processed per second.</param>
        /// <param name="totalPackets">Total packets processed.</param>
        void UpdateStats(long packetsPerSecond, long totalPackets);
    }
}
