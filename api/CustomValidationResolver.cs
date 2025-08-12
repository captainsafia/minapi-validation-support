#pragma warning disable ASP0029 // Microsoft.Extensions.Validation is experimental

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Validation;
using Models;

namespace Api;

/// <summary>
/// Custom validation resolver that provides additional validation metadata
/// and business rule validation for specific types.
/// </summary>
public class BusinessRuleValidationResolver : IValidatableInfoResolver
{
    public bool TryGetValidatableTypeInfo(Type type, [NotNullWhen(true)] out IValidatableInfo? validatableInfo)
    {
        validatableInfo = null;

        // Add custom validation for our business types
        if (type == typeof(BusinessProduct))
        {
            validatableInfo = new BusinessProductValidatableInfo();
            return true;
        }

        if (type == typeof(BusinessStore))
        {
            validatableInfo = new BusinessStoreValidatableInfo();
            return true;
        }

        return false;
    }

    public bool TryGetValidatableParameterInfo(ParameterInfo parameterInfo, [NotNullWhen(true)] out IValidatableInfo? validatableInfo)
    {
        validatableInfo = null;

        // Add custom validation for specific parameters
        if (parameterInfo.Name == "discountPercent" && parameterInfo.ParameterType == typeof(decimal))
        {
            validatableInfo = new DiscountParameterValidatableInfo(parameterInfo);
            return true;
        }

        if (parameterInfo.Name == "bulkQuantity" && parameterInfo.ParameterType == typeof(int))
        {
            validatableInfo = new BulkQuantityParameterValidatableInfo(parameterInfo);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Custom ValidatableTypeInfo for BusinessProduct that adds business-specific validation rules.
/// </summary>
public class BusinessProductValidatableInfo : ValidatableTypeInfo
{
    public BusinessProductValidatableInfo() 
        : base(typeof(BusinessProduct), [.. CreateProperties()])
    {
    }

    private static IEnumerable<ValidatablePropertyInfo> CreateProperties()
    {
        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessProduct), typeof(string), nameof(BusinessProduct.Sku), "SKU",
            [new RequiredAttribute(), new BusinessSkuValidationAttribute()]);

        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessProduct), typeof(decimal), nameof(BusinessProduct.WholesalePrice), "Wholesale Price",
            [new RequiredAttribute(), new RangeAttribute(0.01, 999999.99)]);

        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessProduct), typeof(decimal), nameof(BusinessProduct.RetailPrice), "Retail Price",
            [new RequiredAttribute(), new RangeAttribute(0.01, 999999.99)]);

        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessProduct), typeof(string), nameof(BusinessProduct.Category), "Category",
            [new RequiredAttribute(), new BusinessCategoryValidationAttribute()]);
    }
}

/// <summary>
/// Custom ValidatableTypeInfo for BusinessStore that adds business-specific validation rules.
/// </summary>
public class BusinessStoreValidatableInfo : ValidatableTypeInfo
{
    public BusinessStoreValidatableInfo() 
        : base(typeof(BusinessStore), [..CreateProperties()])
    {
    }

    private static IEnumerable<ValidatablePropertyInfo> CreateProperties()
    {
        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessStore), typeof(string), nameof(BusinessStore.BusinessLicenseNumber), "Business License Number",
            [new RequiredAttribute(), new BusinessLicenseValidationAttribute()]);

        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessStore), typeof(string), nameof(BusinessStore.TaxId), "Tax ID",
            [new RequiredAttribute(), new StringLengthAttribute(11)]);

        yield return new BusinessValidatablePropertyInfo(
            typeof(BusinessStore), typeof(DateTime), nameof(BusinessStore.LicenseExpiry), "License Expiry",
            [new RequiredAttribute(), new FutureDateValidationAttribute()]);
    }
}

/// <summary>
/// Custom ValidatablePropertyInfo that uses pre-defined validation attributes.
/// </summary>
public class BusinessValidatablePropertyInfo(
    Type containingType,
    Type propertyType,
    string name,
    string displayName,
    ValidationAttribute[] validationAttributes) : ValidatablePropertyInfo(containingType, propertyType, name, displayName)
{
    private readonly ValidationAttribute[] _validationAttributes = validationAttributes;

    protected override ValidationAttribute[] GetValidationAttributes() => _validationAttributes;
}

/// <summary>
/// Custom ValidatableParameterInfo for discount parameters.
/// Demonstrates implementing a custom ValidateAsync method with business rule validation.
/// </summary>
public class DiscountParameterValidatableInfo(ParameterInfo parameterInfo) : ValidatableParameterInfo(parameterInfo.ParameterType, parameterInfo.Name ?? "discount", parameterInfo.Name ?? "Discount Percentage")
{
    protected override ValidationAttribute[] GetValidationAttributes()
    {
        return
        [
            new RangeAttribute(0.0, 100.0) { ErrorMessage = "Discount percentage must be between 0 and 100" },
            new RequiredAttribute() { ErrorMessage = "Discount percentage is required" }
        ];
    }

    /// <summary>
    /// Custom validation logic that includes business rules beyond basic validation attributes.
    /// </summary>
    public override async Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken)
    {
        // First run the base validation (handles attributes and basic validation)
        await base.ValidateAsync(value, context, cancellationToken);

        // If base validation failed, don't continue with custom validation
        if (context.ValidationErrors?.Count > 0)
        {
            return;
        }

        // Custom business rule validation
        if (value is decimal discountPercent)
        {
            await ValidateDiscountBusinessRules(discountPercent, context, cancellationToken);
        }
    }

    private static async Task ValidateDiscountBusinessRules(decimal discountPercent, ValidateContext context, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrEmpty(context.CurrentValidationPath) ? "discountPercent" : $"{context.CurrentValidationPath}.discountPercent";

        // Business Rule 1: Weekend discounts cannot exceed 25%
        if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
        {
            if (discountPercent > 25.0m)
            {
                AddValidationError(context, "discountPercent", key, 
                    "Weekend discounts cannot exceed 25%");
                return;
            }
        }

        // Business Rule 2: High discounts (over 50%) require manager approval (simulated async check)
        if (discountPercent > 50.0m)
        {
            var isApproved = await SimulateManagerApprovalCheck(discountPercent, cancellationToken);
            if (!isApproved)
            {
                AddValidationError(context, "discountPercent", key, 
                    "Discounts over 50% require manager approval");
                return;
            }
        }

        // Business Rule 3: Check against maximum allowed discount for the day (simulated external service)
        var maxDailyDiscount = await GetMaxDailyDiscountFromExternalService(cancellationToken);
        if (discountPercent > maxDailyDiscount)
        {
            AddValidationError(context, "discountPercent", key, 
                $"Discount cannot exceed the daily maximum of {maxDailyDiscount}%");
        }

        // Business Rule 4: Validate against promotional calendar
        var isPromotionalPeriod = await CheckPromotionalCalendar(cancellationToken);
        if (!isPromotionalPeriod && discountPercent > 15.0m)
        {
            AddValidationError(context, "discountPercent", key, 
                "Discounts over 15% are only allowed during promotional periods");
        }
    }

    private static void AddValidationError(ValidateContext context, string propertyName, string key, string errorMessage)
    {
        context.ValidationErrors ??= [];
        
        if (context.ValidationErrors.TryGetValue(key, out var existingErrors))
        {
            var newErrors = new string[existingErrors.Length + 1];
            existingErrors.CopyTo(newErrors, 0);
            newErrors[existingErrors.Length] = errorMessage;
            context.ValidationErrors[key] = newErrors;
        }
        else
        {
            context.ValidationErrors[key] = [errorMessage];
        }
    }

    /// <summary>
    /// Simulates an async call to check manager approval for high discounts.
    /// </summary>
    private static async Task<bool> SimulateManagerApprovalCheck(decimal discountPercent, CancellationToken cancellationToken)
    {
        // Simulate async operation (e.g., checking a database or calling an external service)
        await Task.Delay(10, cancellationToken);
        
        // For demo purposes, approve discounts up to 75%
        return discountPercent <= 75.0m;
    }

    /// <summary>
    /// Simulates getting the maximum allowed discount from an external service.
    /// </summary>
    private static async Task<decimal> GetMaxDailyDiscountFromExternalService(CancellationToken cancellationToken)
    {
        // Simulate async external service call
        await Task.Delay(5, cancellationToken);
        
        // For demo purposes, return different limits based on day of week
        return DateTime.Now.DayOfWeek switch
        {
            DayOfWeek.Monday => 30.0m,
            DayOfWeek.Tuesday => 35.0m,
            DayOfWeek.Wednesday => 40.0m,
            DayOfWeek.Thursday => 35.0m,
            DayOfWeek.Friday => 45.0m,
            DayOfWeek.Saturday => 25.0m,
            DayOfWeek.Sunday => 20.0m,
            _ => 30.0m
        };
    }

    /// <summary>
    /// Simulates checking a promotional calendar to determine if higher discounts are allowed.
    /// </summary>
    private static async Task<bool> CheckPromotionalCalendar(CancellationToken cancellationToken)
    {
        // Simulate async operation
        await Task.Delay(5, cancellationToken);
        
        // For demo purposes, consider first week of month as promotional period
        return DateTime.Now.Day <= 7;
    }
}

/// <summary>
/// Custom ValidatableParameterInfo for bulk quantity parameters.
/// </summary>
public class BulkQuantityParameterValidatableInfo(ParameterInfo parameterInfo) : ValidatableParameterInfo(parameterInfo.ParameterType, parameterInfo.Name ?? "bulkQuantity", parameterInfo.Name ?? "Bulk Quantity")
{
    protected override ValidationAttribute[] GetValidationAttributes()
    {
        return
        [
            new RangeAttribute(10, 10000) { ErrorMessage = "Bulk quantity must be between 10 and 10,000" },
            new RequiredAttribute() { ErrorMessage = "Bulk quantity is required for bulk orders" }
        ];
    }
}

// Custom validation attributes for business rules

/// <summary>
/// Validates that the SKU follows business format: 3 letters + 4 digits
/// </summary>
public class BusinessSkuValidationAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string sku)
            return false;

        // SKU format: ABC1234 (3 letters followed by 4 digits)
        return System.Text.RegularExpressions.Regex.IsMatch(sku, @"^[A-Z]{3}\d{4}$");
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be in format ABC1234 (3 uppercase letters followed by 4 digits)";
    }
}

/// <summary>
/// Validates that the category is from approved business categories
/// </summary>
public class BusinessCategoryValidationAttribute : ValidationAttribute
{
    private static readonly string[] ValidCategories = 
    [
        "Electronics", "Clothing", "Food", "Books", "Home", "Sports", "Beauty", "Automotive"
    ];

    public override bool IsValid(object? value)
    {
        if (value is not string category)
            return false;

        return ValidCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be one of: {string.Join(", ", ValidCategories)}";
    }
}

/// <summary>
/// Validates business license number format
/// </summary>
public class BusinessLicenseValidationAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string license)
            return false;

        // Business license format: BL-12345678
        return System.Text.RegularExpressions.Regex.IsMatch(license, @"^BL-\d{8}$");
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be in format BL-12345678";
    }
}

/// <summary>
/// Validates that the date is in the future
/// </summary>
public class FutureDateValidationAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not DateTime date)
            return false;

        return date > DateTime.Now;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a future date";
    }
}
