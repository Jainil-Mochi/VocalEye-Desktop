namespace VocalEye.Desktop.ApiClient;
public sealed class BackendApiOptions { public const string SectionName = "Backend"; public string BaseUrl { get; set; } = "http://127.0.0.1:5000"; public string HealthPath { get; set; } = "/health"; public string InfoPath { get; set; } = "/api/info"; public string? AccessToken { get; set; } }
