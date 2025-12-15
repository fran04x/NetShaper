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

        public ConsoleStatsView()
        {
            _displayLine = Console.CursorTop;
        }

        public void UpdateStats(long packetsPerSecond, long totalPackets)
        {
            Console.SetCursorPosition(0, _displayLine);
            Console.Write(
                $"PPS: {packetsPerSecond,10:N0} | Total paquetes: {totalPackets,12:N0}   ");
        }
    }
}
