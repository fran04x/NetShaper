// NetShaper.Engine/Engine.cs
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using NetShaper.Abstractions;

namespace NetShaper.Engine
{
    public sealed class Engine : IEngine
    {
        private const int BufferSize = 2048;
        private const int MaxConsecutiveErrors = 1000;
        private const uint MinPacketSize = 20;
        private const uint MaxIpPacketSize = 65535;

        private const int StateIdle = 0;
        private const int StateRunning = 1;
        private const int StateStopping = 2;
        private const int StateFaulted = 3;
        private const int StateDisposed = 4;

        private readonly EngineTelemetry _telemetry;
        private readonly IPacketLogger _logger;
        private readonly IPacketCapture _capture;
        private readonly byte[] _buffer;

        private int _state;
        private int _cancelRequested;
        private int _captureThreadActive;

        public bool IsRunning => Interlocked.CompareExchange(ref _state, 0, 0) == StateRunning;
        public long PacketCount => _telemetry.PacketsProcessed;

        public Engine(IPacketLogger logger, IPacketCapture capture)
        {
            _telemetry = new EngineTelemetry();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));

            _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            ArrayPoolDiagnostics.RecordRent();

            _state = StateIdle;
            _cancelRequested = 0;
        }

        public StartResult Start(string filter, CancellationToken ct = default)
        {
            int state = Interlocked.CompareExchange(ref _state, 0, 0);
            if (state == StateRunning || state == StateStopping)
                return StartResult.AlreadyRunning;
            if (state == StateDisposed)
                return StartResult.Disposed;
            if (string.IsNullOrWhiteSpace(filter))
                return StartResult.InvalidFilter;

            if (Interlocked.CompareExchange(ref _state, StateRunning, StateIdle) != StateIdle)
                return StartResult.AlreadyRunning;

            Interlocked.Exchange(ref _cancelRequested, 0);

            CaptureResult result = _capture.Open(filter);
            if (result != CaptureResult.Success)
            {
                Interlocked.Exchange(ref _state, StateIdle);
                return result == CaptureResult.InvalidFilter
                    ? StartResult.InvalidFilter
                    : StartResult.OpenFailed;
            }

            _telemetry.Reset();
            Log(LogCode.EngineStarted, 0);
            return StartResult.Success;
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _state, StateStopping, StateRunning) != StateRunning)
                return;

            Interlocked.Exchange(ref _cancelRequested, 1);
            _capture.Shutdown();
        }

        public EngineResult RunCaptureLoop()
        {
            int currentState = Interlocked.CompareExchange(ref _state, 0, 0);
            
            // If Stop() was called before we entered, return Stopped gracefully
            if (currentState == StateStopping)
                return EngineResult.Stopped;
            
            // Only StateRunning is valid for starting the capture loop
            if (currentState != StateRunning)
                return EngineResult.InvalidState;

            // Ensure only one thread can execute capture loop at a time
            // This protects the shared _buffer from concurrent access
            if (Interlocked.CompareExchange(ref _captureThreadActive, 1, 0) != 0)
                return EngineResult.InvalidState;

            EngineResult result;
            try
            {
                result = ProcessLoop();
            }
            finally
            {
                Interlocked.Exchange(ref _captureThreadActive, 0);
            }

            Interlocked.Exchange(
                ref _state,
                result == EngineResult.Success || result == EngineResult.Stopped
                    ? StateIdle
                    : StateFaulted);

            Log(LogCode.EngineStopped, _telemetry.PacketsProcessed);
            return result;
        }

        private EngineResult ProcessLoop()
        {
            PacketMetadata metadata = default;
            Span<byte> buffer = _buffer.AsSpan(0, BufferSize);

            while (true)
            {
                if (ShouldStop())
                    return EngineResult.Stopped;

                CaptureResult recv = _capture.Receive(buffer, out uint len, ref metadata);

                EngineResult? errorResult = HandleReceiveError(recv);
                if (errorResult.HasValue)
                    return errorResult.Value;

                if (!IsValidPacket(len))
                {
                    _telemetry.RecordInvalidPacket();
                    continue;
                }

                ProcessPacket(buffer, len, ref metadata);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EngineResult? HandleReceiveError(CaptureResult recv)
        {
            if (recv == CaptureResult.Success)
                return null;

            if (recv == CaptureResult.OperationAborted || recv == CaptureResult.InvalidHandle)
                return EngineResult.Stopped;

            _telemetry.RecordRecvError();
            if (_telemetry.ConsecutiveErrors > MaxConsecutiveErrors)
                return EngineResult.TooManyErrors;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldStop()
        {
            return Interlocked.CompareExchange(ref _state, 0, 0) != StateRunning ||
                   Interlocked.CompareExchange(ref _cancelRequested, 0, 0) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidPacket(uint length)
        {
            return length >= MinPacketSize &&
                   length <= BufferSize &&
                   length <= MaxIpPacketSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessPacket(Span<byte> buffer, uint length, ref PacketMetadata metadata)
        {
            Span<byte> packet = buffer.Slice(0, (int)length);
            _capture.CalculateChecksums(packet, length, ref metadata);

            CaptureResult sendResult = _capture.Send(packet, ref metadata);
            
            if (sendResult == CaptureResult.Success)
            {
                _telemetry.RecordPacket();
            }
            else if (sendResult == CaptureResult.InvalidHandle)
            {
                // Handle was disposed during shutdown - this is expected during rapid stop
                // Don't record as error, just ignore
            }
            else
            {
                _telemetry.RecordSendError();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Log(LogCode code, long value)
        {
            _logger.Log(new PacketLogEntry(
                Stopwatch.GetTimestamp(),
                LogLevel.Info,
                code,
                value));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _state, StateDisposed) == StateDisposed)
                return;

            _capture.Dispose();

            ArrayPool<byte>.Shared.Return(_buffer);
            ArrayPoolDiagnostics.RecordReturn();
            ArrayPoolDiagnostics.ValidateBalance();
        }
    }
}
