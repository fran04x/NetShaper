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
        private const int PpsFieldWidth = 10;
        private const int TotalFieldWidth = 12;

        private int _displayLine;
        private long _lastTotal = 0;

        public ConsoleStatsView()
        {
        }

        public void Initialize()
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
                $"{status} PPS: {packetsPerSecond,PpsFieldWidth:N0} | Total paquetes: {totalPackets,TotalFieldWidth:N0}   ");
            
            _lastTotal = totalPackets;
        }
    }
}
