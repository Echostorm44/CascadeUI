#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

// How a FormValidator collects its fields and validates them is covered by ValidationTests.
public sealed class FormValidatorTests
{
    private sealed class TestNode : Node
    {
    }

    [Test]
    public async Task Constructor_OutParam_CreatesScope()
    {
        var content = new TestNode();
        var validator = new FormValidator(out var scope, content);

        bool sameScope = ReferenceEquals(validator.Scope, scope);
        Node storedContent = validator.Content;

        await Assert.That(sameScope).IsTrue();
        await Assert.That(storedContent).IsEqualTo(content);
    }

    [Test]
    public async Task Constructor_WithExistingScope_UsesProvidedScope()
    {
        var scope = new FormScope();
        var validator = new FormValidator(scope, Node.Empty);

        bool sameScope = ReferenceEquals(scope, validator.Scope);
        await Assert.That(sameScope).IsTrue();
    }

    [Test]
    public async Task EmptyScope_IsValid_AndValidateAllPasses()
    {
        var scope = new FormScope();
        await Assert.That(scope.FieldCount).IsEqualTo(0);
        await Assert.That(scope.IsValid).IsTrue();
        await Assert.That(scope.ValidateAll()).IsTrue();
    }
}
