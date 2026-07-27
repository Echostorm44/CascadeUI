namespace Cascade.UI.Installer;

public sealed record ProtocolHandler
{
    public required string Scheme { get; init; }
    public required string HandlerExe { get; init; }
    public string? Description { get; init; }
}
