// NetShaper.UI/Controllers/EngineController.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Abstractions;
using NetShaper.UI.Views;

namespace NetShaper.UI.Controllers
{
    public sealed class EngineController : IDisposable
    {
        private readonly IEngine _engine;
        private readonly IPacketLogger _logger;
        private readonly IConsoleView _consoleView;

        private CancellationTokenSource? _linkedCts;
        private Task? _captureTask;
        private Task? _monitorTask;
        private int _running;

        private const int ConsoleUpdateInterval = 1000; // Milisegundos entre actualizaciones

        public EngineController(IEngine engine, IPacketLogger logger, IConsoleView consoleView)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(consoleView);
            
            _engine = engine;
            _logger = logger;
            _consoleView = consoleView;
            _running = 0;
        }

        public bool IsRunning => _engine.IsRunning;
        public long PacketCount => _engine.PacketCount;

        public StartResult Start(string filter, CancellationToken appToken)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                return StartResult.AlreadyRunning;

            // Dispose old CTS if exists (protection against multiple Start calls)
            var oldCts = Interlocked.Exchange(ref _linkedCts, null);
            oldCts?.Dispose();

            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appToken);

            StartResult result = _engine.Start(filter, _linkedCts.Token);
            if (result != StartResult.Success)
            {
                // Cleanup: dispose CTS in error path to prevent memory leak
                _linkedCts?.Dispose();
                _linkedCts = null;
                CleanupRunningFlag();
                return result;
            }

            _captureTask = Task.Run(() =>
            {
                try
                {
                    _engine.RunCaptureLoop();
                }
                finally
                {
                    _linkedCts?.Cancel(); // fuerza salida del monitor
                    CleanupRunningFlag();
                }
            }, _linkedCts.Token);

            _monitorTask = StartMonitorTask(_linkedCts.Token);
            return StartResult.Success;
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _running, 0, 1) != 1)
                return;

            _engine.Stop();
            _linkedCts?.Cancel();
        }

        public async Task ShutdownAsync()
        {
            Stop();

            try
            {
                if (_captureTask != null)
                    await _captureTask.ConfigureAwait(false);

                if (_monitorTask != null)
                    await _monitorTask.ConfigureAwait(false);
            }
            finally
            {
                _linkedCts?.Dispose();
                (_engine as IDisposable)?.Dispose();
            }
        }

        private Task StartMonitorTask(CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            long last = _engine.PacketCount;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);

                    long current = _engine.PacketCount;
                    long delta = current - last;

                    _logger.Log(new PacketLogEntry(
                        Stopwatch.GetTimestamp(),
                        LogLevel.Info,
                        LogCode.PacketProcessed,
                        delta));

                    // Only update console if not cancelled
                    if (!ct.IsCancellationRequested)
                    {
                        _consoleView.UpdateStats(delta, current);
                    }

                    last = current;
                }
                catch (TaskCanceledException)
                {
                    // Expected when stopping
                    break;
                }
            }
        }, ct);
    }

        private void CleanupRunningFlag()
        {
            Interlocked.Exchange(ref _running, 0);
        }

        public void Dispose()
        {
            _linkedCts?.Dispose();
        }
    }
}
