// NetShaper.Engine/Engine.cs
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NetShaper.Abstractions;

namespace NetShaper.Engine
{
    /// <summary>
    /// Multi-threaded batch processing engine using WinDivertRecvEx.
    /// Achieves 83k PPS with 1 thread, 2.4x improvement over baseline.
    /// </summary>
    public sealed class Engine : IEngine
    {
        private readonly IPacketLogger _logger;
        private readonly Func<IPacketCapture> _captureFactory;
        private readonly int _threadCount;
        private CancellationTokenSource _cts;  // Not readonly - recreated on each Start()
        
        // Per-thread resources
        private readonly IPacketCapture[] _captures;
        private readonly byte[][] _buffers;
        private readonly Thread[] _threads;
        
        private readonly EngineTelemetry _telemetry;
        private int _isRunning;
        private int _disposed;
        
        private const int BufferSize = 128 * 1024; // 128KB for batch
        private const int BatchSize = 64; // Max packets per batch
        
        public Engine(IPacketLogger logger, Func<IPacketCapture> captureFactory, int threadCount)
        {
            if (threadCount <= 0 || threadCount > 16)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            
            _logger = logger;
            _captureFactory = captureFactory ?? throw new ArgumentNullException(nameof(captureFactory));
            _threadCount = threadCount;
            _cts = new CancellationTokenSource();
            _telemetry = new EngineTelemetry();
            
            // Initialize arrays
            _captures = new IPacketCapture[threadCount];
            _buffers = new byte[threadCount][];
            _threads = new Thread[threadCount];
            
            // Pre-allocate buffers and create capture instances
            for (int i = 0; i < threadCount; i++)
            {
                _captures[i] = _captureFactory();
                _buffers[i] = System.Buffers.ArrayPool<byte>.Shared.Rent(BufferSize);
                ArrayPoolDiagnostics.RecordRent();
            }
        }
        
        // IEngine interface implementation
        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;
        public long PacketCount => _telemetry.PacketsProcessed;
        
        public StartResult Start(string filter, CancellationToken ct = default)
        {
            // Link external cancellation token to internal CTS
            if (ct != default && ct.CanBeCanceled)
            {
                ct.Register(() => _cts.Cancel());
            }
            
            return Start(filter);
        }
        
        public EngineResult RunCaptureLoop()
        {
            // Engine manages threads internally, so this is a no-op
            // Threads are already running after Start()
            // Wait for cancellation or Stop()
            while (IsRunning && !_cts.Token.IsCancellationRequested)
            {
                Thread.Sleep(100);
            }
            
            return EngineResult.Success;
        }
        
        public StartResult Start(string filter)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return StartResult.AlreadyRunning;
            
            // Create NEW CTS before disposing old one to prevent ObjectDisposedException
            // Worker threads may still be reading _cts.Token during rapid Start/Stop cycles
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cts, newCts);
            
            // Dispose old CTS asynchronously with grace period for pending reads
            if (oldCts != null)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    Thread.Sleep(500);  // Grace period for threads to finish reading Token
                    try { oldCts.Dispose(); } catch { /* best effort */ }
                });
            }
            
            // Reset telemetry for new session
            _telemetry.Reset();
            
            // Open ALL handles with the same filter
            for (int i = 0; i < _threadCount; i++)
            {
                CaptureResult result = _captures[i].Open(filter);
                if (result != CaptureResult.Success)
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                    return StartResult.OpenFailed;
                }
            }
            
            _telemetry.Reset();
            
            // Launch worker threads
            for (int i = 0; i < _threadCount; i++)
            {
                int threadIdx = i;
                _threads[threadIdx] = new Thread(() => ProcessLoopWorker(threadIdx))
                {
                    IsBackground = true,
                    Name = $"BatchWorker-{threadIdx}",
                    Priority = ThreadPriority.AboveNormal
                };
                _threads[threadIdx].Start();
            }
            
            return StartResult.Success;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldStop() => _cts.Token.IsCancellationRequested;
        
        private void ProcessLoopWorker(int threadIdx)
        {
            IPacketCapture capture = _captures[threadIdx];
            byte[] bufferArray = _buffers[threadIdx];
            
            // stackalloc metadata array for batch (zero allocation)
            Span<PacketMetadata> metadataBatch = stackalloc PacketMetadata[BatchSize];
            
            // Local counters
            long localPackets = 0;
            long localRecvErrors = 0;
            long localSendErrors = 0;
            long localInvalidPackets = 0;
            
            // For periodic telemetry updates (so UI shows real-time counts)
            const long TelemetryFlushInterval = 1000;
            
            try
            {
                while (true)
                {
                    if (ShouldStop())
                        break;
                    
                    Span<byte> buffer = bufferArray.AsSpan(0, BufferSize);
                    
                    // Receive batch of packets
                    CaptureResult recv = capture.ReceiveBatch(
                        buffer,
                        metadataBatch,
                        out uint totalBytes,
                        out int packetCount);
                    
                    if (recv != CaptureResult.Success)
                    {
                        if (recv == CaptureResult.InvalidHandle || recv == CaptureResult.OperationAborted)
                            break;
                        
                        localRecvErrors++;
                        continue;
                    }
                    
                    // Process batch using native-parsed lengths
                    ProcessBatch(capture, buffer, metadataBatch, packetCount,
                        ref localPackets, ref localSendErrors, ref localInvalidPackets);
                    
                    // Periodic telemetry flush for real-time UI updates
                    if (localPackets >= TelemetryFlushInterval)
                    {
                        _telemetry.AddPackets(localPackets);
                        _telemetry.AddRecvErrors(localRecvErrors);
                        _telemetry.AddSendErrors(localSendErrors);
                        _telemetry.AddInvalidPackets(localInvalidPackets);
                        
                        localPackets = 0;
                        localRecvErrors = 0;
                        localSendErrors = 0;
                        localInvalidPackets = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during clean shutdown via CancellationToken
            }
            catch (ObjectDisposedException)
            {
                // Expected if capture was disposed externally during rapid Stop
            }
            // Do NOT catch Exception - let critical errors propagate to UnhandledExceptionHandler
            finally
            {
                // Final flush of any remaining counters
                if (localPackets > 0) _telemetry.AddPackets(localPackets);
                if (localRecvErrors > 0) _telemetry.AddRecvErrors(localRecvErrors);
                if (localSendErrors > 0) _telemetry.AddSendErrors(localSendErrors);
                if (localInvalidPackets > 0) _telemetry.AddInvalidPackets(localInvalidPackets);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessBatch(
            IPacketCapture capture,
            Span<byte> buffer,
            Span<PacketMetadata> metadata,
            int count,
            ref long packetsProcessed,
            ref long sendErrors,
            ref long invalidPackets)
        {
            uint offset = 0;
            
            for (int i = 0; i < count; i++)
            {
                ref PacketMetadata meta = ref metadata[i];
                uint packetLen = meta.Length;
                
                // Validate packet length with structured logging
                if (packetLen == 0)
                {
                    _logger.Log(new PacketLogEntry(
                        System.Diagnostics.Stopwatch.GetTimestamp(),
                        LogLevel.Warning,
                        LogCode.InvalidPacket,
                        0));  // Value = 0 indica zero-length
                    
                    invalidPackets++;
                    continue;  // Continue processing next packet, don't break entire batch
                }
                
                if (packetLen > BufferSize)
                {
                    _logger.Log(new PacketLogEntry(
                        System.Diagnostics.Stopwatch.GetTimestamp(),
                        LogLevel.Error,
                        LogCode.InvalidPacket,
                        (long)packetLen));  // Value = packetLen excedido
                    
                    invalidPackets++;
                    continue;  // Continue processing next packet
                }
                
                if (offset + packetLen > buffer.Length)
                {
                    _logger.Log(new PacketLogEntry(
                        System.Diagnostics.Stopwatch.GetTimestamp(),
                        LogLevel.Error,
                        LogCode.InvalidPacket,
                        (long)offset));  // Value = offset donde falló
                    
                    invalidPackets++;
                    break;  // Buffer overflow: error en batch completo, no podemos continuar
                }
                
                Span<byte> packet = buffer.Slice((int)offset, (int)packetLen);
                
                // Calculate checksums and send individually
                capture.CalculateChecksums(packet, packetLen, ref meta);
                
                CaptureResult sendResult = capture.Send(packet, ref meta);
                if (sendResult == CaptureResult.Success)
                {
                    packetsProcessed++;
                }
                else if (sendResult != CaptureResult.InvalidHandle)
                {
                    sendErrors++;
                }
                
                offset += packetLen;
            }
        }
        
        public void Stop()
        {
            _cts.Cancel();
            
            // Unblock WinDivertRecv calls
            if (_captures != null)
            {
                foreach (var capture in _captures)
                {
                    try { capture?.Shutdown(); } catch { /* best effort */ }
                }
            }
            
            // Wait for threads
            for (int i = 0; i < _threadCount; i++)
            {
                if (_threads[i] != null && _threads[i].IsAlive)
                {
                    _threads[i].Join(2000);  // Best effort join with timeout
                }
            }
            
            Interlocked.Exchange(ref _isRunning, 0);
        }
        
        public long TotalPacketsProcessed => _telemetry.PacketsProcessed;
        public int ThreadCount => _threadCount;
        
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                Stop();
                _cts.Dispose();
                
                // Cleanup per-thread resources
                for (int i = 0; i < _threadCount; i++)
                {
                    // Return buffer
                    if (_buffers[i] != null)
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(_buffers[i]);
                        ArrayPoolDiagnostics.RecordReturn();
                    }
                    
                    // Dispose capture handle
                    (_captures[i] as IDisposable)?.Dispose();
                }
            }
        }
    }
}
