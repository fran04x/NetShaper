// NetShaper.UI/Views/ConsoleStatsView.cs
using System;

namespace NetShaper.UI.Views
{
    /// <summary>
    /// Console implementation of statistics view.
    /// Handles direct console manipulation (cursor positioning, formatting).
    /// </summary>
    public sealed class ConsoleStatsView : IConsoleView
    {
        private readonly int _displayLine;
        private long _lastTotal = 0;

        public ConsoleStatsView()
        {
            _displayLine = Console.CursorTop;
            Console.WriteLine(); // Reserve line for stats
        }

        public void UpdateStats(long packetsPerSecond, long totalPackets)
        {
            Console.SetCursorPosition(0, _displayLine);
            
            // Visual indicator if capturing
            string status = totalPackets > _lastTotal ? "🟢" : (totalPackets > 0 ? "⏸️" : "⚪");
            
            Console.Write(
                $"{status} PPS: {packetsPerSecond,10:N0} | Total paquetes: {totalPackets,12:N0}   ");
            
            _lastTotal = totalPackets;
        }
    }
}
