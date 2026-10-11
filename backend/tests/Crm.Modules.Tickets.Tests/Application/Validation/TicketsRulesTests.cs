using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Tickets.Application.Localization;
using Crm.Modules.Tickets.Application.Validation;
using Crm.Modules.Tickets.Tests.Support;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Modules.Tickets.Tests.Application.Validation;

/// <summary>The nine public rule extensions, run through a tiny <see cref="InlineValidator{T}"/>.</summary>
public sealed class TicketsRulesTests
{
    private sealed record Probe(string? Value);

    private static ValidationResult Run(Action<IRuleBuilder<Probe, string?>, ILocalizationService> configure, string? value, ILocalizationService? localization = null)
    {
        var validator = new InlineValidator<Probe>();
        configure(validator.RuleFor(p => p.Value), localization ?? FakeLocalization.Keys());
        return validator.Validate(new Probe(value));
    }

    private static ValidationResult RunNonNullable(Action<IRuleBuilder<Probe, string>, ILocalizationService> configure, string? value, ILocalizationService? localization = null)
    {
        var validator = new InlineValidator<Probe>();
        configure(validator.RuleFor(p => p.Value ?? string.Empty), localization ?? FakeLocalization.Keys());
        return validator.Validate(new Probe(value));
    }

    // ---- SupportedQueue ----

    [Theory]
    [InlineData(null, true)]
    [InlineData("mine", true)]
    [InlineData("ALL", true)]
    [InlineData("unassigned", true)]
    [InlineData("inbox", false)]
    [InlineData("", false)]
    public void TheQueueRule(string? value, bool valid)
    {
        ValidationResult result = Run((rule, l) => rule.SupportedQueue(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.QueueUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedSort ----

    [Theory]
    [InlineData(null, true)]
    [InlineData("createdAt", true)]
    [InlineData("MODIFIEDAT", true)]
    [InlineData("priority", true)]
    [InlineData("number", true)]
    [InlineData("price", false)]
    [InlineData("", false)]
    public void TheSortRule(string? value, bool valid)
    {
        ValidationResult result = Run((rule, l) => rule.SupportedSort(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.SortUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedPortalTab ----

    [Theory]
    [InlineData(null, true)]
    [InlineData("open", true)]
    [InlineData("CLOSED", true)]
    [InlineData("all", false)]
    [InlineData("", false)]
    public void ThePortalTabRule(string? value, bool valid)
    {
        ValidationResult result = Run((rule, l) => rule.SupportedPortalTab(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.PortalTabUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedPortalStatus ----

    [Theory]
    [InlineData(null, true)]
    [InlineData("Received", true)]
    [InlineData("INPROGRESS", true)]
    [InlineData("awaitingyourreply", true)]
    [InlineData("Resolved", true)]
    [InlineData("closed", true)]
    [InlineData("Open", false)]
    [InlineData("PendingCustomer", false)]
    [InlineData("", false)]
    public void ThePortalStatusRule(string? value, bool valid)
    {
        ValidationResult result = Run((rule, l) => rule.SupportedPortalStatus(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.PortalStatusUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedStatus ----

    [Theory]
    [InlineData("New", true)]
    [InlineData("open", true)]
    [InlineData("PENDINGCUSTOMER", true)]
    [InlineData("OnHold", true)]
    [InlineData("Resolved", true)]
    [InlineData("closed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Archived", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void TheStatusRule(string value, bool valid)
    {
        ValidationResult result = RunNonNullable((rule, l) => rule.SupportedStatus(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.StatusUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedPriority ----

    [Theory]
    [InlineData("Low", true)]
    [InlineData("normal", true)]
    [InlineData("HIGH", true)]
    [InlineData("urgent", true)]
    [InlineData("1", false)]
    [InlineData("2", false)]
    [InlineData("Critical", false)]
    [InlineData("", false)]
    public void ThePriorityRule(string value, bool valid)
    {
        ValidationResult result = RunNonNullable((rule, l) => rule.SupportedPriority(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.PriorityUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedOptionalPriority ----

    [Theory]
    [InlineData(null, true)]
    [InlineData("Low", true)]
    [InlineData("URGENT", true)]
    [InlineData("", false)]
    [InlineData("1", false)]
    [InlineData("Critical", false)]
    public void TheOptionalPriorityRule(string? value, bool valid)
    {
        ValidationResult result = Run((rule, l) => rule.SupportedOptionalPriority(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.PriorityUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedVisibility ----

    [Theory]
    [InlineData("public", true)]
    [InlineData("INTERNAL", true)]
    [InlineData("System", true)]
    [InlineData("private", false)]
    [InlineData("", false)]
    public void TheVisibilityRule(string value, bool valid)
    {
        ValidationResult result = RunNonNullable((rule, l) => rule.SupportedVisibility(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.VisibilityUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- SupportedTransitionAction ----

    [Theory]
    [InlineData("Open", true)]
    [InlineData("awaitCustomer", true)]
    [InlineData("PUTONHOLD", true)]
    [InlineData("Resolve", true)]
    [InlineData("close", true)]
    [InlineData("Reopen", true)]
    [InlineData("Cancel", true)]
    [InlineData("Assign", false)]
    [InlineData("Claim", false)]
    [InlineData("Reply", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void TheTransitionActionRule(string value, bool valid)
    {
        ValidationResult result = RunNonNullable((rule, l) => rule.SupportedTransitionAction(l), value);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Equal(TicketsMessageKeys.Validation.ActionUnsupported, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    // ---- localised text ----

    [Theory]
    [InlineData("en", "Queue must be one of mine, unassigned, all.")]
    public void TheMessageIsLocalisedInEnglish(string culture, string expected)
    {
        using UiCulture ui = UiCulture.Use(culture);

        ValidationResult result = Run((rule, l) => rule.SupportedQueue(l), "inbox", FakeLocalization.Resources());

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void TheMessageIsLocalisedInArabic()
    {
        using UiCulture ui = UiCulture.Use("ar");

        ValidationResult result = Run((rule, l) => rule.SupportedQueue(l), "inbox", FakeLocalization.Resources());

        Assert.Contains("قائمة الانتظار", Assert.Single(result.Errors).ErrorMessage, StringComparison.Ordinal);
    }

    // ---- null guards ----

    [Fact]
    public void ANullRuleOrLocalizationIsRefusedByEveryRule()
    {
        var validator = new InlineValidator<Probe>();
        IRuleBuilder<Probe, string?> nullable = validator.RuleFor(p => p.Value);
        IRuleBuilder<Probe, string> plain = validator.RuleFor(p => p.Value ?? string.Empty);
        ILocalizationService localization = FakeLocalization.Keys();

        Func<IRuleBuilder<Probe, string?>, ILocalizationService, IRuleBuilderOptions<Probe, string?>>[] optional =
        [
            (r, l) => r.SupportedQueue(l), (r, l) => r.SupportedSort(l), (r, l) => r.SupportedPortalTab(l),
            (r, l) => r.SupportedPortalStatus(l), (r, l) => r.SupportedOptionalPriority(l),
        ];
        foreach (var rule in optional)
        {
            NullArguments.AssertThrowsArgumentNull(rule, null, localization);
            NullArguments.AssertThrowsArgumentNull(rule, nullable, null);
        }

        Func<IRuleBuilder<Probe, string>, ILocalizationService, IRuleBuilderOptions<Probe, string>>[] required =
        [
            (r, l) => r.SupportedStatus(l), (r, l) => r.SupportedPriority(l), (r, l) => r.SupportedVisibility(l),
            (r, l) => r.SupportedTransitionAction(l),
        ];
        foreach (var rule in required)
        {
            NullArguments.AssertThrowsArgumentNull(rule, null, localization);
            NullArguments.AssertThrowsArgumentNull(rule, plain, null);
        }
    }
}
