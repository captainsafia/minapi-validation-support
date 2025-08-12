using System.ComponentModel.DataAnnotations;

namespace Api;

/// <summary>
/// Business product with additional validation rules beyond the standard Product model.
/// </summary>
public class BusinessProduct : IValidatableObject
{
    public string Sku { get; set; } = string.Empty;
    public decimal WholesalePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public string Category { get; set; } = string.Empty;

    // Custom validation using IValidatableObject
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Business rule: Retail price must be higher than wholesale price
        if (RetailPrice <= WholesalePrice)
        {
            yield return new ValidationResult(
                "Retail price must be higher than wholesale price",
                [nameof(RetailPrice), nameof(WholesalePrice)]);
        }

        // Business rule: Markup should not exceed 500%
        if (WholesalePrice > 0 && (RetailPrice / WholesalePrice) > 6.0m)
        {
            yield return new ValidationResult(
                "Retail price cannot exceed 500% markup over wholesale price",
                [nameof(RetailPrice)]);
        }
    }
}

/// <summary>
/// Business store with additional validation rules and compliance requirements.
/// </summary>
public class BusinessStore : IValidatableObject
{
    public string BusinessLicenseNumber { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public DateTime LicenseExpiry { get; set; }

    // Custom validation using IValidatableObject
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Business rule: License must not be expired
        if (LicenseExpiry <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Business license has expired and must be renewed",
                [nameof(LicenseExpiry)]);
        }

        // Business rule: License should not expire within 30 days
        if (LicenseExpiry <= DateTime.Now.AddDays(30))
        {
            yield return new ValidationResult(
                "Business license expires within 30 days - renewal recommended",
                [nameof(LicenseExpiry)]);
        }
    }
}
