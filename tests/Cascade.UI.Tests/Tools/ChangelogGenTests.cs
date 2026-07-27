using Cascade.Tools.ChangelogGen;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// ── CommitParserTests ────────────────────────────────────────────

public class CommitParserTests
{
    [Test]
    public async Task Parse_FeatWithScope_ReturnsCorrectFields()
    {
        ConventionalCommit? commit = CommitParser.Parse("feat(controls): add Button component");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("feat");
        string? scope = commit.Scope;
        await Assert.That(scope).IsEqualTo("controls");
        string description = commit.Description;
        await Assert.That(description).IsEqualTo("add Button component");
    }

    [Test]
    public async Task Parse_FixWithoutScope_ReturnsNullScope()
    {
        ConventionalCommit? commit = CommitParser.Parse("fix: resolve crash on startup");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("fix");
        await Assert.That(commit.Scope).IsNull();
        string description = commit.Description;
        await Assert.That(description).IsEqualTo("resolve crash on startup");
    }

    [Test]
    public async Task Parse_BreakingBang_SetsIsBreaking()
    {
        ConventionalCommit? commit = CommitParser.Parse("feat!: remove deprecated API");

        await Assert.That(commit).IsNotNull();
        bool isBreaking = commit!.IsBreaking;
        await Assert.That(isBreaking).IsTrue();
    }

    [Test]
    public async Task Parse_BreakingChangeInBody_SetsIsBreaking()
    {
        string message = "feat(controls): update layout\n\nBREAKING CHANGE: old layout removed";
        ConventionalCommit? commit = CommitParser.Parse(message);

        await Assert.That(commit).IsNotNull();
        bool isBreaking = commit!.IsBreaking;
        await Assert.That(isBreaking).IsTrue();
        string? body = commit.Body;
        await Assert.That(body).IsEqualTo("BREAKING CHANGE: old layout removed");
    }

    [Test]
    public async Task Parse_NonConventionalMessage_ReturnsNull()
    {
        ConventionalCommit? commit = CommitParser.Parse("just a random commit message");

        await Assert.That(commit).IsNull();
    }

    [Test]
    public async Task Parse_PerfWithScope_ReturnsCorrectType()
    {
        ConventionalCommit? commit = CommitParser.Parse("perf(animation): reduce allocations");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("perf");
        string? scope = commit.Scope;
        await Assert.That(scope).IsEqualTo("animation");
    }

    [Test]
    public async Task Parse_DocsWithoutScope_ReturnsCorrectType()
    {
        ConventionalCommit? commit = CommitParser.Parse("docs: update README");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("docs");
    }

    [Test]
    public async Task Parse_RefactorWithScope_ReturnsCorrectType()
    {
        ConventionalCommit? commit = CommitParser.Parse("refactor(generator): simplify codegen");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("refactor");
        string? scope = commit.Scope;
        await Assert.That(scope).IsEqualTo("generator");
    }

    [Test]
    public async Task Parse_ChoreWithoutScope_ReturnsCorrectType()
    {
        ConventionalCommit? commit = CommitParser.Parse("chore: bump dependencies");

        await Assert.That(commit).IsNotNull();
        string type = commit!.Type;
        await Assert.That(type).IsEqualTo("chore");
    }

    [Test]
    public async Task Parse_HashIsStored()
    {
        ConventionalCommit? commit = CommitParser.Parse("feat: add feature", "abc1234");

        await Assert.That(commit).IsNotNull();
        string hash = commit!.Hash;
        await Assert.That(hash).IsEqualTo("abc1234");
    }

    [Test]
    public async Task ParseAll_FiltersInvalidMessages()
    {
        string[] messages =
        [
            "feat(controls): add Button",
            "not a conventional commit",
            "fix: resolve bug",
        ];

        IReadOnlyList<ConventionalCommit> results = CommitParser.ParseAll(messages);

        int count = results.Count;
        await Assert.That(count).IsEqualTo(2);
        string firstType = results[0].Type;
        await Assert.That(firstType).IsEqualTo("feat");
        string secondType = results[1].Type;
        await Assert.That(secondType).IsEqualTo("fix");
    }

    [Test]
    public async Task ParseAll_EmptyInput_ReturnsEmptyList()
    {
        IReadOnlyList<ConventionalCommit> results = CommitParser.ParseAll([]);

        int count = results.Count;
        await Assert.That(count).IsEqualTo(0);
    }
}

// ── ChangelogGeneratorTests ──────────────────────────────────────

public class ChangelogGeneratorTests
{
    [Test]
    public async Task Generate_HeaderContainsVersion()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "feat", Description = "add X" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsHeader = result.Contains("## 1.0.0", StringComparison.Ordinal);
        await Assert.That(containsHeader).IsTrue();
    }

    [Test]
    public async Task Generate_GroupsFeaturesUnderNewFeatures()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "feat", Scope = "controls", Description = "add Button" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsSection = result.Contains("### New Features", StringComparison.Ordinal);
        await Assert.That(containsSection).IsTrue();
        bool containsEntry = result.Contains("- **controls**: add Button", StringComparison.Ordinal);
        await Assert.That(containsEntry).IsTrue();
    }

    [Test]
    public async Task Generate_GroupsFixesUnderBugFixes()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "fix", Description = "resolve crash" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsSection = result.Contains("### Bug Fixes", StringComparison.Ordinal);
        await Assert.That(containsSection).IsTrue();
    }

    [Test]
    public async Task Generate_GroupsPerfUnderPerformance()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "perf", Scope = "animation", Description = "reduce allocations" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsSection = result.Contains("### Performance", StringComparison.Ordinal);
        await Assert.That(containsSection).IsTrue();
    }

    [Test]
    public async Task Generate_BreakingChangesPopulatedWhenPresent()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "feat", Description = "remove old API", IsBreaking = true },
        ];

        string result = ChangelogGenerator.Generate("2.0.0", commits);

        bool containsSection = result.Contains("### Breaking Changes", StringComparison.Ordinal);
        await Assert.That(containsSection).IsTrue();
        bool containsEntry = result.Contains("- remove old API", StringComparison.Ordinal);
        await Assert.That(containsEntry).IsTrue();
        bool containsNone = result.Contains("None.", StringComparison.Ordinal);
        await Assert.That(containsNone).IsFalse();
    }

    [Test]
    public async Task Generate_BreakingChangesShowsNoneWhenAbsent()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "feat", Description = "add feature" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsNone = result.Contains("None.", StringComparison.Ordinal);
        await Assert.That(containsNone).IsTrue();
    }

    [Test]
    public async Task Generate_ScopeIsBoldedInOutput()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "fix", Scope = "theme", Description = "fix color" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsBoldScope = result.Contains("**theme**", StringComparison.Ordinal);
        await Assert.That(containsBoldScope).IsTrue();
    }

    [Test]
    public async Task Generate_NoScopeEntriesFormatCorrectly()
    {
        List<ConventionalCommit> commits =
        [
            new ConventionalCommit { Type = "fix", Description = "fix startup", Hash = "abc123" },
        ];

        string result = ChangelogGenerator.Generate("1.0.0", commits);

        bool containsEntry = result.Contains("- fix startup (abc123)", StringComparison.Ordinal);
        await Assert.That(containsEntry).IsTrue();
    }
}
