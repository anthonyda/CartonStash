using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 006 amendment R3.4: which saved answers let a source be used.</summary>
public class SourceAgreementPolicyTests
{
    private const string Text = "text-a";

    public static TheoryData<SourceAgreementAnswer?, bool, bool> Answers => new()
    {
        { null, false, true },
        { new SourceAgreementAnswer(true, Text), true, false },
        { new SourceAgreementAnswer(true, "old-text"), false, true },
        { new SourceAgreementAnswer(false, Text), false, false },
        { new SourceAgreementAnswer(false, "old-text"), false, false },
    };

    [Theory]
    [MemberData(nameof(Answers))]
    public void Saved_answer_decides_use_and_asking(SourceAgreementAnswer? answer, bool mayUse, bool needsAnswer) // R3.8, R3.9
    {
        Assert.Equal(mayUse, SourceAgreementPolicy.MayUse(answer, Text));
        Assert.Equal(needsAnswer, SourceAgreementPolicy.NeedsAnswer(answer, Text));
    }

    [Fact]
    public void Hash_changes_with_any_part_of_the_agreements() // R3.9
    {
        var hash = SourceAgreementPolicy.Hash([new Agreement("Terms", "Text", "https://a")]);

        Assert.Equal(hash, SourceAgreementPolicy.Hash([new Agreement("Terms", "Text", "https://a")]));
        Assert.NotEqual(hash, SourceAgreementPolicy.Hash([new Agreement("Terms 2", "Text", "https://a")]));
        Assert.NotEqual(hash, SourceAgreementPolicy.Hash([new Agreement("Terms", "Text 2", "https://a")]));
        Assert.NotEqual(hash, SourceAgreementPolicy.Hash([new Agreement("Terms", "Text", "https://b")]));
    }

    [Fact]
    public void Only_the_store_source_has_the_region_notice() // R3.6
    {
        Assert.Contains("region", SourceAgreementPolicy.ReceivesNotice("StoreEdgeFD"));
        Assert.Null(SourceAgreementPolicy.ReceivesNotice("Contoso.Source"));
    }
}
