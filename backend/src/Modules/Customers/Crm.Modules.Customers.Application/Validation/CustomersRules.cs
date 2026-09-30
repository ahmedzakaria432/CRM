using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application.Localization;
using Crm.Modules.Customers.Application.Specifications;
using FluentValidation;

namespace Crm.Modules.Customers.Application.Validation;

public static class CustomersRules
{
    /// <summary>The preferred language is en or ar.</summary>
    public static IRuleBuilderOptions<T, string> SupportedLanguage<T>(this IRuleBuilder<T, string> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(l => l is "en" or "ar")
            .WithMessage(_ => localization.Get(CustomersMessageKeys.Validation.LanguageUnsupported));
    }

    /// <summary>The sort key is one of createdAt, number, nameEn, nameAr (or absent).</summary>
    public static IRuleBuilderOptions<T, string?> SupportedSort<T>(this IRuleBuilder<T, string?> rule, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);

        return rule
            .Must(s => s is null || CustomerSortParser.TryParse(s, out _))
            .WithMessage(_ => localization.Get(CustomersMessageKeys.Validation.SortUnsupported));
    }
}
