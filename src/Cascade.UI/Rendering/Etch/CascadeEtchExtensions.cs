namespace Cascade.UI.Backend.Etch;

public static class CascadeEtchExtensions
{
    public static AppConfig UseEtch(this AppConfig config)
    {
        config.BackendProvider = new EtchBackendProvider();
        return config;
    }
}
