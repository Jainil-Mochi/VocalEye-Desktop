using VocalEye.Desktop.Core.DTOs;

namespace VocalEye.Desktop.Core.Interfaces;

/// <summary>GET /api/tools: names, descriptions and risk levels (used for the risk badge on approval cards).</summary>
public interface IToolCatalogService
{
    Task<IReadOnlyList<ToolDto>> GetToolsAsync(CancellationToken cancellationToken);
}
