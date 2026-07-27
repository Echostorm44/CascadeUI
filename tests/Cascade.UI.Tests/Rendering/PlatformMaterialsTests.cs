using Cascade.UI;
using Cascade.UI.Rendering.Internal;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class PlatformMaterialsTests
{
    // ── Availability ────────────────────────────────────────────────────

    [Test]
    public async Task None_IsAlwaysAvailable()
    {
        var result = PlatformMaterials.IsAvailable(WindowMaterial.None);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Mica_Availability_MatchesPlatform()
    {
        bool expected = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        var result = PlatformMaterials.IsAvailable(WindowMaterial.Mica);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task MicaAlt_Availability_MatchesPlatform()
    {
        bool expected = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        var result = PlatformMaterials.IsAvailable(WindowMaterial.MicaAlt);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Acrylic_Availability_MatchesPlatform()
    {
        bool expected = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393);
        var result = PlatformMaterials.IsAvailable(WindowMaterial.Acrylic);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Vibrancy_Availability_MatchesPlatform()
    {
        bool expected = OperatingSystem.IsMacOS();
        var result = PlatformMaterials.IsAvailable(WindowMaterial.Vibrancy);
        await Assert.That(result).IsEqualTo(expected);
    }

    // ── Resolve ─────────────────────────────────────────────────────────

    [Test]
    public async Task Resolve_None_ReturnsNone()
    {
        var result = PlatformMaterials.Resolve(WindowMaterial.None);
        await Assert.That(result).IsEqualTo(WindowMaterial.None);
    }

    [Test]
    public async Task Resolve_UnavailableMaterial_ReturnsNone()
    {
        // Vibrancy is macOS-only, Mica is Windows 11-only.
        // At least one of these must be unavailable on any given platform.
        if (!OperatingSystem.IsMacOS())
        {
            var result = PlatformMaterials.Resolve(WindowMaterial.Vibrancy);
            await Assert.That(result).IsEqualTo(WindowMaterial.None);
        }
        else
        {
            var result = PlatformMaterials.Resolve(WindowMaterial.Mica);
            await Assert.That(result).IsEqualTo(WindowMaterial.None);
        }
    }

    // ── Fallback ────────────────────────────────────────────────────────

    [Test]
    public async Task Fallback_UnavailableMaterial_ReturnsNoneWithColor()
    {
        var fallbackColor = new ColorValue("#FF0000");

        if (!OperatingSystem.IsMacOS())
        {
            var (material, color) = PlatformMaterials.Fallback(WindowMaterial.Vibrancy, fallbackColor);
            await Assert.That(material).IsEqualTo(WindowMaterial.None);
            await Assert.That(color).IsEqualTo(fallbackColor);
        }
        else
        {
            var (material, color) = PlatformMaterials.Fallback(WindowMaterial.Mica, fallbackColor);
            await Assert.That(material).IsEqualTo(WindowMaterial.None);
            await Assert.That(color).IsEqualTo(fallbackColor);
        }
    }

    // ── ReducedTransparency ─────────────────────────────────────────────

    [Test]
    [NotInParallel("PlatformMaterials")]
    public async Task ReducedTransparency_OverridesAvailability()
    {
        PlatformMaterials.ReducedTransparency = true;
        try
        {
            await Assert.That(PlatformMaterials.IsAvailable(WindowMaterial.None)).IsTrue();
            await Assert.That(PlatformMaterials.IsAvailable(WindowMaterial.Mica)).IsFalse();
            await Assert.That(PlatformMaterials.IsAvailable(WindowMaterial.MicaAlt)).IsFalse();
            await Assert.That(PlatformMaterials.IsAvailable(WindowMaterial.Acrylic)).IsFalse();
            await Assert.That(PlatformMaterials.IsAvailable(WindowMaterial.Vibrancy)).IsFalse();
        }
        finally
        {
            PlatformMaterials.ReducedTransparency = false;
        }
    }

    [Test]
    [NotInParallel("PlatformMaterials")]
    public async Task ReducedTransparency_Resolve_ReturnsNone()
    {
        PlatformMaterials.ReducedTransparency = true;
        try
        {
            await Assert.That(PlatformMaterials.Resolve(WindowMaterial.Mica)).IsEqualTo(WindowMaterial.None);
            await Assert.That(PlatformMaterials.Resolve(WindowMaterial.Acrylic)).IsEqualTo(WindowMaterial.None);
            await Assert.That(PlatformMaterials.Resolve(WindowMaterial.Vibrancy)).IsEqualTo(WindowMaterial.None);
            await Assert.That(PlatformMaterials.Resolve(WindowMaterial.None)).IsEqualTo(WindowMaterial.None);
        }
        finally
        {
            PlatformMaterials.ReducedTransparency = false;
        }
    }

    // ── MaterialSurface ─────────────────────────────────────────────────

    [Test]
    public async Task MaterialSurface_WithNone_IsFallback_IsFalse()
    {
        var surface = new MaterialSurface(WindowMaterial.None);
        await Assert.That(surface.IsFallback).IsFalse();
    }

    [Test]
    public async Task MaterialSurface_UnavailableMaterial_IsFallback_IsTrue()
    {
        // Use a material that is definitely unavailable on this platform.
        var unavailable = OperatingSystem.IsMacOS()
            ? WindowMaterial.Mica
            : WindowMaterial.Vibrancy;

        var surface = new MaterialSurface(unavailable, new ColorValue("#333333"));
        await Assert.That(surface.IsFallback).IsTrue();
        await Assert.That(surface.EffectiveMaterial).IsEqualTo(WindowMaterial.None);
    }

    [Test]
    public async Task MaterialSurface_StoresProperties()
    {
        var color = new ColorValue("#AABBCC");
        var surface = new MaterialSurface(WindowMaterial.Acrylic, color, 8f);

        await Assert.That(surface.Material).IsEqualTo(WindowMaterial.Acrylic);
        await Assert.That(surface.FallbackColor).IsEqualTo(color);
        await Assert.That(surface.CornerRadius).IsEqualTo(8f);
    }

    // ── Theme integration ───────────────────────────────────────────────

    [Test]
    public async Task FluentTheme_DefaultMaterial_IsMica()
    {
        var theme = new FluentTheme();
        await Assert.That(theme.WindowMaterial).IsEqualTo(WindowMaterial.Mica);
    }

    [Test]
    public async Task AppleTheme_DefaultMaterial_IsVibrancy()
    {
        var theme = new AppleTheme();
        await Assert.That(theme.WindowMaterial).IsEqualTo(WindowMaterial.Vibrancy);
    }

    [Test]
    public async Task Material3Theme_DefaultMaterial_IsNone()
    {
        var theme = new Material3Theme();
        await Assert.That(theme.WindowMaterial).IsEqualTo(WindowMaterial.None);
    }

    [Test]
    public async Task CascadeTheme_BaseDefault_IsNone()
    {
        // FluentTheme overrides to Mica, but the base default is None.
        // Material3Theme does not override, so it uses the base default.
        CascadeTheme theme = new Material3Theme();
        await Assert.That(theme.WindowMaterial).IsEqualTo(WindowMaterial.None);
    }
}
