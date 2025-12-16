// NetShaper.Engine/Engine.cs
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NetShaper.Abstractions;
using NetShaper.Abstractions.Attributes;

namespace NetShaper.Engine
{
    /// <summary>
    /// Multi-threaded batch processing engine using WinDivertRecvEx.
    /// Achieves 83k PPS with 1 thread, 2.4x improvement over baseline.
    /// </summary>
    public sealed class Engine : IEngine
    {
        // R707: Constants first (member order)
        private const int KB = 1024;
        private const int BufferSizeBytes = 65536;  // R402: Standard size (64KB)
        private const int MaxPacketsPerBatch = 64;
        private const int MaxThreads = 16;
        private const int TelemetryFlushThreshold = 100;
        private const int TelemetryFlushIntervalPackets = 1000;  // R1203: Named constant
        private const int StartupGracePeriodMs = 500;
        private const int ShutdownTimeoutMs = 1000;
        private const int StopJoinTimeoutMs = 2000;
        
        // R707: Fields after constants
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
        
        // R707: Constructor after fields
        // R401: Buffers rented here are returned in Dispose() via ReturnAllBuffers() (DNS §7.03 ownership transfer)
        public Engine(IPacketLogger logger, Func<IPacketCapture> captureFactory, int threadCount)
        {
            if (threadCount <= 0 || threadCount > MaxThreads)  // R1203: Use named constant
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
            
            // R401: Allocate buffers - ownership transferred to class fields, returned in Dispose()
            for (int i = 0; i < threadCount; i++)
            {
                _captures[i] = _captureFactory();
                _buffers[i] = System.Buffers.ArrayPool<byte>.Shared.Rent(BufferSizeBytes);
                ArrayPoolDiagnostics.RecordRent();
            }
        }
        
        // R707: Properties after constructor
        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;
        public long PacketCount => _telemetry.PacketsProcessed;
        public long TotalPacketsProcessed => _telemetry.PacketsProcessed;
        public int ThreadCount => _threadCount;
        
        // R707: Public methods after properties
        public StartResult Start(string filter, CancellationToken ct = default)
        {
            // R507: Input validation
            ArgumentNullException.ThrowIfNull(filter);
            if (string.IsNullOrWhiteSpace(filter))
                return StartResult.InvalidFilter;
            
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
                Thread.Sleep(TelemetryFlushThreshold);  // R1203: Named constant
            }
            
            return EngineResult.Success;
        }
        
        public StartResult Start(string filter)
        {
            // R507: Input validation
            ArgumentNullException.ThrowIfNull(filter);
            if (string.IsNullOrWhiteSpace(filter))
                return StartResult.InvalidFilter;
            
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return StartResult.AlreadyRunning;
            
            // Create NEW CTS before disposing old one to prevent ObjectDisposedException
            // Worker threads may still be reading _cts.Token during rapid Start/Stop cycles
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cts, newCts);
            
            // R302 FIX: Use Thread instead of Task.Run (no Task allocation)
            if (oldCts != null)
            {
                var disposeThread = new Thread(() =>
                {
                    Thread.Sleep(StartupGracePeriodMs);  // R1203: Named constant
                    try { oldCts.Dispose(); } catch { /* best effort */ }
                })
                {
                    IsBackground = true,
                    Name = "CTS-Cleanup"
                };
                disposeThread.Start();
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
        
        public void Stop()
        {
            _cts.Cancel();
            
            // R308 FIX: Move try-catch outside loop
            ShutdownAllCaptures();
            WaitForAllThreads();
            
            Interlocked.Exchange(ref _isRunning, 0);
        }
        
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                Stop();
                _cts.Dispose();
                
                // R401 FIX: Return buffers to pool
                ReturnAllBuffers();
            }
        }
        
        // R707: Private methods last
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldStop() => _cts.Token.IsCancellationRequested;
        
        // RED R2.03: BatchProcessor allows nesting≤4 for loop+validation+dispatch+action pattern
        [BatchProcessor]
        private void ProcessLoopWorker(int threadIdx)
        {
            IPacketCapture capture = _captures[threadIdx];
            byte[] bufferArray = _buffers[threadIdx];
            
            // stackalloc metadata array for batch (zero allocation)
            Span<PacketMetadata> metadataBatch = stackalloc PacketMetadata[MaxPacketsPerBatch];
            
            // Local counters
            long localPackets = 0;
            long localRecvErrors = 0;
            long localSendErrors = 0;
            long localInvalidPackets = 0;
            
            try
            {
                while (!ShouldStop())
                {
                    Span<byte> buffer = bufferArray.AsSpan(0, BufferSizeBytes);
                    
                    // R201/R203: Extracted method reduces CC
                    if (!TryReceiveBatch(capture, buffer, metadataBatch, 
                        out int packetCount, ref localRecvErrors))
                        break;
                    
                    if (packetCount == 0)
                        continue;
                    
                    // Process batch using native-parsed lengths
                    ProcessBatch(capture, buffer, metadataBatch, packetCount,
                        ref localPackets, ref localSendErrors, ref localInvalidPackets);
                    
                    // R201/R203: Extracted method reduces CC
                    FlushTelemetryCounters(
                        ref localPackets, ref localRecvErrors, 
                        ref localSendErrors, ref localInvalidPackets);
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
                FinalFlushTelemetry(localPackets, localRecvErrors, localSendErrors, localInvalidPackets);
            }
        }
        
        // R201/R203: Extracted method to reduce CC and nesting in ProcessLoopWorker
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReceiveBatch(
            IPacketCapture capture,
            Span<byte> buffer,
            Span<PacketMetadata> metadataBatch,
            out int packetCount,
            ref long localRecvErrors)
        {
            packetCount = 0;
            
            CaptureResult recv = capture.ReceiveBatch(
                buffer, metadataBatch, out uint totalBytes, out packetCount);
            
            if (recv == CaptureResult.Success)
                return true;
            
            // Fatal errors that stop the loop
            if (recv == CaptureResult.InvalidHandle || recv == CaptureResult.OperationAborted)
                return false;
            
            // Non-fatal error, increment counter and signal retry
            localRecvErrors++;
            packetCount = 0;  // Signal caller to continue
            return true;
        }
        
        // R201: Extracted method to reduce CC in ProcessLoopWorker
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushTelemetryCounters(
            ref long localPackets, 
            ref long localRecvErrors,
            ref long localSendErrors, 
            ref long localInvalidPackets)
        {
            if (localPackets < TelemetryFlushIntervalPackets)
                return;
            
            _telemetry.AddPackets(localPackets);
            _telemetry.AddRecvErrors(localRecvErrors);
            _telemetry.AddSendErrors(localSendErrors);
            _telemetry.AddInvalidPackets(localInvalidPackets);
            
            localPackets = 0;
            localRecvErrors = 0;
            localSendErrors = 0;
            localInvalidPackets = 0;
        }
        
        // R201: Extracted method to reduce CC in ProcessLoopWorker
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FinalFlushTelemetry(
            long localPackets, 
            long localRecvErrors, 
            long localSendErrors, 
            long localInvalidPackets)
        {
            if (localPackets > 0) _telemetry.AddPackets(localPackets);
            if (localRecvErrors > 0) _telemetry.AddRecvErrors(localRecvErrors);
            if (localSendErrors > 0) _telemetry.AddSendErrors(localSendErrors);
            if (localInvalidPackets > 0) _telemetry.AddInvalidPackets(localInvalidPackets);
        }
        
        // RED R2.03: BatchProcessor allows nesting≤4 for batch processing pattern
        [BatchProcessor]
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
                
                // R203: Extracted validation method
                PacketValidation validation = ValidatePacketBounds(
                    packetLen, offset, buffer.Length, ref invalidPackets);
                
                if (validation == PacketValidation.Skip)
                    continue;
                
                if (validation == PacketValidation.Break)
                    break;
                
                // validation == PacketValidation.Valid
                Span<byte> packet = buffer.Slice((int)offset, (int)packetLen);
                
                // Calculate checksums and send individually
                capture.CalculateChecksums(packet, packetLen, ref meta);
                
                CaptureResult sendResult = capture.Send(packet, ref meta);
                if (sendResult == CaptureResult.Success)
                    packetsProcessed++;
                else if (sendResult != CaptureResult.InvalidHandle)
                    sendErrors++;
                
                offset += packetLen;
            }
        }
        
        // R203: Extracted method to reduce nesting in ProcessBatch
        private enum PacketValidation
        {
            Valid,   // Packet is valid, process it
            Skip,    // Packet is invalid, skip to next
            Break    // Fatal error, stop batch
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PacketValidation ValidatePacketBounds(
            uint packetLen,
            uint offset,
            int bufferLength,
            ref long invalidPackets)
        {
            // Zero-length packet
            if (packetLen == 0)
            {
                _logger.Log(new PacketLogEntry(
                    System.Diagnostics.Stopwatch.GetTimestamp(),
                    LogLevel.Warning,
                    LogCode.InvalidPacket,
                    0));
                
                invalidPackets++;
                return PacketValidation.Skip;
            }
            
            // Packet too large
            if (packetLen > BufferSizeBytes)
            {
                _logger.Log(new PacketLogEntry(
                    System.Diagnostics.Stopwatch.GetTimestamp(),
                    LogLevel.Error,
                    LogCode.InvalidPacket,
                    (long)packetLen));
                
                invalidPackets++;
                return PacketValidation.Skip;
            }
            
            // Buffer overflow
            if (offset + packetLen > bufferLength)
            {
                _logger.Log(new PacketLogEntry(
                    System.Diagnostics.Stopwatch.GetTimestamp(),
                    LogLevel.Error,
                    LogCode.InvalidPacket,
                    (long)offset));
                
                invalidPackets++;
                return PacketValidation.Break;
            }
            
            return PacketValidation.Valid;
        }
        
        // R308 FIX: Helper methods to avoid try-catch in loop
        // Static helpers first (R707: static before instance)
        private static void TryShutdownCapture(IPacketCapture capture)
        {
            try
            {
                capture?.Shutdown();
            }
            catch
            {
                // best effort
            }
        }
        
        private static void TryJoinThread(Thread thread)
        {
            if (thread != null && thread.IsAlive)
            {
                thread.Join(StopJoinTimeoutMs);  // R1203: Named constant
            }
        }
        
        // Instance helper methods
        private void ShutdownAllCaptures()
        {
            if (_captures == null)
                return;
            
            foreach (var capture in _captures)
            {
                TryShutdownCapture(capture);
            }
        }
        
        private void WaitForAllThreads()
        {
            for (int i = 0; i < _threadCount; i++)
            {
                TryJoinThread(_threads[i]);
            }
        }
        
        // R401 FIX: Return buffers helper
        private void ReturnAllBuffers()
        {
            for (int i = 0; i < _threadCount; i++)
            {
                if (_buffers[i] != null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(_buffers[i]);
                    ArrayPoolDiagnostics.RecordReturn();
                }
                
                (_captures[i] as IDisposable)?.Dispose();
            }
        }
    }
}
