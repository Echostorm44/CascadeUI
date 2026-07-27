#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI.Installer;
using Cascade.UI.Installer.Pages;

namespace Cascade.UI.Tests.Installer;

public sealed class WizardPageBaseTests
{
    [Test]
    public async Task ShouldShow_ReturnsTrue_WhenShowWhenIsNull()
    {
        var page = new WelcomePage();
        bool result = page.ShouldShow();
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldShow_ReturnsFalse_WhenShowWhenReturnsFalse()
    {
        var page = new WelcomePage { ShowWhen = () => false };
        bool result = page.ShouldShow();
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetDefaultValue_ReturnsNull_ByDefault()
    {
        var page = new WelcomePage();
        object? result = page.GetDefaultValue();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Validate_ReturnsTrue_ByDefault()
    {
        var page = new WelcomePage();
        bool result = page.Validate();
        await Assert.That(result).IsTrue();
    }
}

public sealed class WelcomePageTests
{
    [Test]
    public async Task DefaultTitle_IsWelcome()
    {
        var page = new WelcomePage();
        string title = page.Title;
        await Assert.That(title).IsEqualTo("Welcome");
    }

    [Test]
    public async Task StoresAppNameAndVersion()
    {
        var page = new WelcomePage { AppName = "MyApp", AppVersion = "1.0.0" };
        string name = page.AppName;
        string version = page.AppVersion;
        await Assert.That(name).IsEqualTo("MyApp");
        await Assert.That(version).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task Position_IsAfterWelcome()
    {
        var page = new WelcomePage();
        PagePosition position = page.Position;
        var expected = PagePosition.AfterWelcome;
        await Assert.That(position).IsEqualTo(expected);
    }
}

public sealed class LicensePageTests
{
    [Test]
    public async Task Validate_ReturnsFalse_WhenNotAccepted()
    {
        var page = new LicensePage();
        bool result = page.Validate();
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Validate_ReturnsTrue_WhenAccepted()
    {
        var page = new LicensePage { Accepted = true };
        bool result = page.Validate();
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task DefaultTitle_IsLicenseAgreement()
    {
        var page = new LicensePage();
        string title = page.Title;
        await Assert.That(title).IsEqualTo("License Agreement");
    }
}

public sealed class ComponentsPageTests
{
    [Test]
    public async Task Components_AreAccessible()
    {
        var components = new List<InstallComponent>
        {
            new() { Id = "core", Name = "Core", Required = true },
            new() { Id = "extras", Name = "Extras" }
        };
        var page = new ComponentsPage { Components = components };
        int count = page.Components.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task RequiredComponents_Identified()
    {
        var components = new List<InstallComponent>
        {
            new() { Id = "core", Name = "Core", Required = true },
            new() { Id = "extras", Name = "Extras", Required = false }
        };
        var page = new ComponentsPage { Components = components };
        int requiredCount = page.Components.Count(c => c.Required);
        await Assert.That(requiredCount).IsEqualTo(1);
    }

    [Test]
    public async Task SelectedComponents_IsMutable()
    {
        var page = new ComponentsPage();
        page.SelectedComponents = ["core", "extras"];
        int count = page.SelectedComponents.Count;
        await Assert.That(count).IsEqualTo(2);
    }
}

public sealed class DirectoryPageTests
{
    [Test]
    public async Task GetDefaultValue_ReturnsDefaultDirectory_WhenSelectedDirectoryEmpty()
    {
        var page = new DirectoryPage { DefaultDirectory = @"C:\Program Files\MyApp" };
        object? result = page.GetDefaultValue();
        string value = (string)result!;
        await Assert.That(value).IsEqualTo(@"C:\Program Files\MyApp");
    }

    [Test]
    public async Task GetDefaultValue_ReturnsSelectedDirectory_WhenSet()
    {
        var page = new DirectoryPage
        {
            DefaultDirectory = @"C:\Program Files\MyApp",
            SelectedDirectory = @"D:\Apps\MyApp"
        };
        object? result = page.GetDefaultValue();
        string value = (string)result!;
        await Assert.That(value).IsEqualTo(@"D:\Apps\MyApp");
    }

    [Test]
    public async Task Validate_Passes_WithDefaultDirectory()
    {
        var page = new DirectoryPage { DefaultDirectory = @"C:\Program Files\MyApp" };
        bool result = page.Validate();
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validate_Fails_WhenBothEmpty()
    {
        var page = new DirectoryPage();
        bool result = page.Validate();
        await Assert.That(result).IsFalse();
    }
}

public sealed class PrerequisitesPageTests
{
    [Test]
    public async Task AllMet_True_WhenAllMet()
    {
        var page = new PrerequisitesPage
        {
            Results =
            [
                new() { Name = ".NET 10", Met = true, Required = true },
                new() { Name = "VC++ Runtime", Met = true, Required = true }
            ]
        };
        bool result = page.AllMet;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task AllMet_False_WhenRequiredPrerequisiteNotMet()
    {
        var page = new PrerequisitesPage
        {
            Results =
            [
                new() { Name = ".NET 10", Met = false, Required = true },
                new() { Name = "VC++ Runtime", Met = true, Required = true }
            ]
        };
        bool result = page.AllMet;
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AllMet_True_WhenOptionalPrerequisiteNotMet()
    {
        var page = new PrerequisitesPage
        {
            Results =
            [
                new() { Name = ".NET 10", Met = true, Required = true },
                new() { Name = "Optional Tool", Met = false, Required = false }
            ]
        };
        bool result = page.AllMet;
        await Assert.That(result).IsTrue();
    }
}

public sealed class InstallingPageTests
{
    [Test]
    public async Task Cancel_SetsIsCancelled()
    {
        var page = new InstallingPage();
        page.Cancel();
        bool result = page.IsCancelled;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Progress_TracksValue()
    {
        var page = new InstallingPage { Progress = 0.5 };
        double progress = page.Progress;
        await Assert.That(progress).IsEqualTo(0.5);
    }

    [Test]
    public async Task CurrentFile_TracksValue()
    {
        var page = new InstallingPage { CurrentFile = "setup.dll" };
        string file = page.CurrentFile;
        await Assert.That(file).IsEqualTo("setup.dll");
    }
}

public sealed class FinishPageTests
{
    [Test]
    public async Task LaunchOnClose_DefaultsTrue()
    {
        var page = new FinishPage();
        bool result = page.LaunchOnClose;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task PostInstallMessage_Stored()
    {
        var page = new FinishPage { PostInstallMessage = "Thank you for installing!" };
        string? message = page.PostInstallMessage;
        await Assert.That(message).IsEqualTo("Thank you for installing!");
    }
}

public sealed class ChoicePageTests
{
    [Test]
    public async Task SelectedOption_ReturnsCorrectOption()
    {
        var page = new ChoicePage { Options = ["A", "B", "C"], SelectedIndex = 1 };
        string result = page.SelectedOption;
        await Assert.That(result).IsEqualTo("B");
    }

    [Test]
    public async Task Validate_Passes_WithValidIndex()
    {
        var page = new ChoicePage { Options = ["A", "B"], SelectedIndex = 0 };
        bool result = page.Validate();
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validate_Fails_WithInvalidIndex()
    {
        var page = new ChoicePage { Options = ["A", "B"], SelectedIndex = 5 };
        bool result = page.Validate();
        await Assert.That(result).IsFalse();
    }
}

public sealed class InputPageTests
{
    [Test]
    public async Task Validate_Passes_WhenRequiredFieldsFilled()
    {
        var page = new InputPage
        {
            Fields = [new() { Name = "username", Label = "Username", Required = true }],
            Values = new Dictionary<string, string> { ["username"] = "admin" }
        };
        bool result = page.Validate();
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validate_Fails_WhenRequiredFieldEmpty()
    {
        var page = new InputPage
        {
            Fields = [new() { Name = "username", Label = "Username", Required = true }],
            Values = new Dictionary<string, string> { ["username"] = "" }
        };
        bool result = page.Validate();
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Validate_Fails_WhenRequiredFieldMissing()
    {
        var page = new InputPage
        {
            Fields = [new() { Name = "username", Label = "Username", Required = true }],
            Values = []
        };
        bool result = page.Validate();
        await Assert.That(result).IsFalse();
    }
}

public sealed class ChecklistPageTests
{
    [Test]
    public async Task CheckedItems_IsMutable()
    {
        var page = new ChecklistPage();
        page.CheckedItems = ["item1", "item2"];
        int count = page.CheckedItems.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Items_AreAccessible()
    {
        var page = new ChecklistPage
        {
            Items = [new() { Id = "opt1", Label = "Option 1" }]
        };
        int count = page.Items.Count;
        await Assert.That(count).IsEqualTo(1);
    }
}

public sealed class InfoPageTests
{
    [Test]
    public async Task Content_Stored()
    {
        var page = new InfoPage { Content = "Some information text." };
        string content = page.Content;
        await Assert.That(content).IsEqualTo("Some information text.");
    }

    [Test]
    public async Task ContentType_DefaultsToText()
    {
        var page = new InfoPage();
        InfoContentType contentType = page.ContentType;
        var expected = InfoContentType.Text;
        await Assert.That(contentType).IsEqualTo(expected);
    }
}

public sealed class CustomPageTests
{
    [Test]
    public async Task GetContent_ReturnsNodeEmpty_WhenNoFactory()
    {
        var page = new CustomPage();
        Node result = page.GetContent();
        Node expected = Node.Empty;
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task GetContent_ReturnsFactoryResult()
    {
        Node node = Node.Empty;
        var page = new CustomPage { ContentFactory = () => node };
        Node result = page.GetContent();
        await Assert.That(result).IsEqualTo(node);
    }
}
