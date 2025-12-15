namespace NetShaper.Abstractions
{
    public interface IEngine : System.IDisposable
    {
        bool IsRunning { get; }
        long PacketCount { get; }

        StartResult Start(string filter, System.Threading.CancellationToken ct = default);
        void Stop();
        EngineResult RunCaptureLoop();
    }

    public enum StartResult
    {
        Success = 0,
        InvalidFilter = 1,
        AlreadyRunning = 2,
        Disposed = 3,
        OpenFailed = 4
    }

    public enum EngineResult
    {
        Success = 0,
        Stopped = 1,
        InvalidState = 2,
        InvalidHandle = 3,
        InvalidParameter = 4,
        Aborted = 5,
        TooManyErrors = 6
    }
}