using System.Net;

namespace VocalEye.Desktop.Core.Exceptions;

public enum ChatApiErrorKind
{
    /// <summary>Non-success HTTP status before/without a stream.</summary>
    Http,
    /// <summary>Backend unreachable / connection dropped.</summary>
    Network,
    /// <summary>An SSE "error" event or a stream that ended without "completed".</summary>
    Stream,
    /// <summary>Response could not be understood.</summary>
    Protocol
}

/// <summary>Single exception type view models need to handle for all chat API failures.</summary>
public sealed class ChatApiException : Exception
{
    public ChatApiException(ChatApiErrorKind kind, string title, string? detail = null,
        HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(string.IsNullOrWhiteSpace(detail) ? title : $"{title}: {detail}", inner)
    {
        Kind = kind;
        Title = title;
        Detail = detail;
        StatusCode = statusCode;
    }

    public ChatApiErrorKind Kind { get; }
    public string Title { get; }
    public string? Detail { get; }
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when retrying the same request may reasonably succeed.</summary>
    public bool IsTransient =>
        Kind is ChatApiErrorKind.Network or ChatApiErrorKind.Stream
        || StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;
}
