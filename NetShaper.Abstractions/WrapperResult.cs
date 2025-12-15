namespace NetShaper.Abstractions
{
    public enum WrapperResult
    {
        Success = 0,
        InvalidFilter = 1,
        InvalidHandle = 2,
        InvalidParameter = 3,
        OperationAborted = 4,
        ElementNotFound = 5,
        BufferTooSmall = 6,
        Unknown = 99
    }
}