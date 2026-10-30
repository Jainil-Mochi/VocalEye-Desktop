using System.Net;
using VocalEye.Desktop.Core.Exceptions;

namespace VocalEye.Desktop.App.ViewModels;

/// <summary>Turns <see cref="ChatApiException"/> into short, friendly UI text.</summary>
public static class ChatErrorFormatter
{
    public static string Format(ChatApiException ex, string operation = "complete that request")
    {
        var detail = string.IsNullOrWhiteSpace(ex.Detail) ? string.Empty : " " + ex.Detail;

        switch (ex.Kind)
        {
            case ChatApiErrorKind.Network:
                return "Can't reach the VocalEye backend. Make sure it is running, then try again.";
            case ChatApiErrorKind.Stream:
                return $"{ex.Title}.{detail}".Trim();
            case ChatApiErrorKind.Protocol:
                return $"The backend sent a response the app didn't understand.{detail}";
        }

        return ex.StatusCode switch
        {
            HttpStatusCode.BadRequest => $"The request was invalid.{detail}",
            HttpStatusCode.Unauthorized => "Not authorized. The backend access token is missing or invalid.",
            HttpStatusCode.NotFound => $"Couldn't {operation}: the conversation was not found.",
            HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented =>
                $"Couldn't {operation}: this backend doesn't support it yet (backend slice B4 is required).",
            HttpStatusCode.BadGateway => $"The model call failed.{detail}",
            HttpStatusCode.GatewayTimeout => "The model took too long to respond.",
            HttpStatusCode.InternalServerError => $"The backend hit an unexpected error.{detail}",
            _ => $"{ex.Title}.{detail}".Trim()
        };
    }
}
