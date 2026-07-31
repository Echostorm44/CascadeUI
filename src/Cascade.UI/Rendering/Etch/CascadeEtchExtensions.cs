namespace Cascade.UI.Backend.Etch;

public static class CascadeEtchExtensions
{
    /// <summary>
    /// Selects the Etch GPU renderer. Optional — Etch is already the default in
    /// <see cref="App.Run{T}(System.Action{AppConfig})"/>; calling this is a no-op-equivalent
    /// kept for back-compat and explicitness.
    /// </summary>
    public static AppConfig UseEtch(this AppConfig config)
    {
        config.BackendProvider = new EtchBackendProvider();
        return config;
    }
}
