namespace TaskManager.SharedKernel;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,

    /// <summary>A transient failure the client can retry (HTTP 503).</summary>
    Unavailable
}
