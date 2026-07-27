#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

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
    public async Task RegisterField_IncreasesCount()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Ok);
        scope.RegisterField(() => ValidationResult.Ok);

        int count = scope.FieldCount;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ValidateAll_StoresResultsAndReportsErrors()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Ok);
        scope.RegisterField(() => ValidationResult.Error("Invalid"));

        bool allValid = scope.ValidateAll();
        int errorCount = scope.ErrorCount;
        bool isValid = scope.IsValid;

        await Assert.That(allValid).IsFalse();
        await Assert.That(errorCount).IsEqualTo(1);
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Reset_ClearsValidationResults()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Error("Invalid"));
        scope.ValidateAll();

        scope.Reset();
        int errorCount = scope.ErrorCount;
        bool isValid = scope.IsValid;

        await Assert.That(errorCount).IsEqualTo(0);
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task ClearFields_EmptiesFieldList()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Ok);
        scope.RegisterField(() => ValidationResult.Ok);

        scope.ClearFields();
        int count = scope.FieldCount;

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task FormScope_IsValidTrueWhenNoErrors()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Ok);
        scope.ValidateAll();

        bool isValid = scope.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task ValidateAll_ReturnsTrueWhenAllPass()
    {
        var scope = new FormScope();
        scope.RegisterField(() => ValidationResult.Ok);
        scope.RegisterField(() => ValidationResult.Ok);

        bool allValid = scope.ValidateAll();
        await Assert.That(allValid).IsTrue();
    }
}
