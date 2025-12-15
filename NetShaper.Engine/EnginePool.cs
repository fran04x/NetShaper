// NetShaper.Engine/EnginePool.cs
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NetShaper.Abstractions;

namespace NetShaper.Engine
{
    /// <summary>
    /// Manages multiple threads processing packets in parallel.
    /// CRITICAL: Uses N independent IPacketCapture instances (N WinDivert handles)
    /// to ensure true load balancing by the driver without user-space contention.
    /// </summary>
    public sealed class EnginePool : IDisposable
    {
        private readonly IPacketLogger _logger;
        private readonly Func<IPacketCapture> _captureFactory;
        private readonly int _threadCount;
        private readonly CancellationTokenSource _cts;
        
        // Per-thread resources
        private readonly IPacketCapture[] _captures;
        private readonly byte[][] _buffers;
        private readonly Thread[] _threads;  // Changed from Task[] to Thread[]
        
        private readonly EngineTelemetry _telemetry;
        private int _isRunning;
        private int _disposed;
        
        private const int BufferSize = 65535;
        
        /// <summary>
        /// Creates a pool with N threads, EACH with its own dedicated capture handle.
        /// </summary>
        public EnginePool(IPacketLogger logger, Func<IPacketCapture> captureFactory, int threadCount)
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
            _threads = new Thread[threadCount]; // Using dedicated threads
            
            // Pre-allocate buffers and create capture instances
            for (int i = 0; i < threadCount; i++)
            {
                _captures[i] = _captureFactory();
                _buffers[i] = System.Buffers.ArrayPool<byte>.Shared.Rent(BufferSize);
                ArrayPoolDiagnostics.RecordRent();
            }
        }
        
        public StartResult Start(string filter)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                return StartResult.AlreadyRunning;
            
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
                    Name = $"NetShaper-Worker-{threadIdx}",
                    Priority = ThreadPriority.AboveNormal // Critical for packet processing
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
            PacketMetadata metadata = default;
            
            // Local counters to avoid Interlocked contention in hot path
            long localPackets = 0;
            long localRecvErrors = 0;
            long localSendErrors = 0;
            long localInvalidPackets = 0;
            
            try 
            {
                while (true)
                {
                    if (ShouldStop())
                        break;
                    
                    Span<byte> buffer = bufferArray.AsSpan(0, BufferSize);
                    
                    CaptureResult recv = capture.Receive(buffer, out uint len, ref metadata);
                    
                    if (recv != CaptureResult.Success)
                    {
                        if (recv == CaptureResult.InvalidHandle || recv == CaptureResult.OperationAborted)
                            break;
                            
                        localRecvErrors++;
                        continue;
                    }
                    
                    if (len < 20 || len > BufferSize)
                    {
                        localInvalidPackets++;
                        continue;
                    }
                    
                    // Inline processing to avoid passing refs to counters
                    Span<byte> packet = buffer.Slice(0, (int)len);
                    capture.CalculateChecksums(packet, len, ref metadata);
                    
                    CaptureResult sendResult = capture.Send(packet, ref metadata);
                    if (sendResult == CaptureResult.Success)
                    {
                        localPackets++;
                    }
                    else if (sendResult != CaptureResult.InvalidHandle)
                    {
                        localSendErrors++;
                    }
                }
            }
            catch (Exception)
            {
                localRecvErrors++;
            }
            finally
            {
                // Update global telemetry ONCE at the end
                if (localPackets > 0) _telemetry.AddPackets(localPackets);
                if (localRecvErrors > 0) _telemetry.AddRecvErrors(localRecvErrors);
                if (localSendErrors > 0) _telemetry.AddSendErrors(localSendErrors);
                if (localInvalidPackets > 0) _telemetry.AddInvalidPackets(localInvalidPackets);
            }
        }
        
        public void Stop()
        {
            _cts.Cancel();
            
            // Critical: Unblock WinDivertRecv calls so threads can exit and report counters
            // WinDivertRecv requires explicit shutdown to unblock
            if (_captures != null)
            {
                foreach (var capture in _captures)
                {
                    try { capture?.Shutdown(); } catch { /* best effort */ }
                }
            }
            
            // Wait for threads to join with timeout
            // Threads will break loop on cancellation token or handle close
            bool allJoined = true;
            for (int i = 0; i < _threadCount; i++)
            {
                if (_threads[i] != null && _threads[i].IsAlive)
                {
                    if (!_threads[i].Join(2000)) // Wait 2s per thread max
                        allJoined = false;
                }
            }
            
            if (!allJoined)
            {
                // Force close optimization: disposing implementation usually breaks blocking calls
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
                    // Note: IPacketCapture doesn't implement IDisposable explicitly in base interface?
                    // Let's check. WinDivertAdapter DOES implement IDisposable via IPacketCapture?
                    // Usually yes. Assuming IPacketCapture extends IDisposable or checking type.
                    if (_captures[i] is IDisposable disposableCapture)
                    {
                        disposableCapture.Dispose();
                    }
                }
            }
        }
    }
}
