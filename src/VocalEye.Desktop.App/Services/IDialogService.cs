namespace VocalEye.Desktop.App.Services;

/// <summary>Abstraction over modal prompts so view models stay testable.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}
