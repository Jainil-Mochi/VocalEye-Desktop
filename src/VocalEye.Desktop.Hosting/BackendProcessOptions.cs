namespace VocalEye.Desktop.Hosting;
public sealed class BackendProcessOptions { public const string SectionName = "Backend"; public bool LaunchBackend { get; set; } = false; public string ProjectPath { get; set; } = ""; public string BaseUrl { get; set; } = "http://127.0.0.1:5000"; public int StartupTimeoutSeconds { get; set; } = 45; }
