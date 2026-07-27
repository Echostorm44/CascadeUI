namespace Cascade.UI.Installer;

public sealed record ServiceDefinition
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string BinaryPath { get; init; }
    public string? Description { get; init; }
    public ServiceStartup Startup { get; init; } = ServiceStartup.Automatic;
    public ServiceAccount Account { get; init; } = ServiceAccount.LocalService;
    public ServiceRestartPolicy RestartPolicy { get; init; } = ServiceRestartPolicy.OnFailure;
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}

public enum ServiceStartup { Automatic, Manual, Disabled, DelayedAutomatic }
public enum ServiceAccount { LocalSystem, LocalService, NetworkService, User, VirtualAccount }
public enum ServiceRestartPolicy { Never, OnFailure, Always }
