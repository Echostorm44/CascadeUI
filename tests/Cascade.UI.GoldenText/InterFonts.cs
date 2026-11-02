using Cascade.UI.Backend.Etch;
using IOPath = System.IO.Path;

namespace Cascade.UI.GoldenText;

/// <summary>
/// The fonts the golden tests probe: the framework's bundled Inter variable font at the weights
/// controls use most (regular, medium, semibold), loaded as the renderer loads them.
/// </summary>
internal static class InterFonts
{
    /// <summary>Weight names, as the tests label their failures.</summary>
    public static readonly string[] Weights = ["Regular", "Medium", "SemiBold"];

    public static string VariableFontPath => IOPath.Combine(GoldenHarness.RepoRoot, "src", "Cascade.UI", "Fonts", "InterVariable.ttf");

    /// <summary>The font locator for a weight name (what TextLayoutEngine and DrawContext take).</summary>
    public static string Locator(string weightName) => FontLocator.WithWeight(VariableFontPath, weightName switch
    {
        "Regular" => FontWeight.Regular,
        "Medium" => FontWeight.Medium,
        "SemiBold" => FontWeight.SemiBold,
        _ => throw new ArgumentException($"Unknown weight '{weightName}'", nameof(weightName)),
    });

    /// <summary>Loads the weight into <paramref name="backend"/> as DrawContext does.</summary>
    public static ulong Load(EtchBackend backend, string weightName)
    {
        string locator = Locator(weightName);
        return backend.LoadSharedFont(FontFiles.Bytes(FontLocator.FilePath(locator)), FontLocator.Variations(locator));
    }
}
