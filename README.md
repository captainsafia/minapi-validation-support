# Built-in Validation Support in Minimal APIs

This app demonstrates built-in support for System.ComponentModel.DataAnnotations-based validations in minimal APIs.

## Run Sample App

To run the API, navigate to the `api` directory and execute `dotnet run`.

```
$ cd api
$ dotnet run
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5040
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: ~/git/minapi-validation-support/api
```

Use the `requests.http` file located in the `api` directory with your favorite HTTP client of choice to test out the end-to-end experience.

## Using built-in Validation Support

To enable built-in validation support for minimal APIs, call the `AddValidation` extension method to register the required services into the service container for your application.

```csharp
builder.Services.AddValidation();
```

The implementation automatically discovers types that are defined in minimal API handlers or as base types of types defined in minimal API handlers. To explicitly opt-in a type to validation, add the `[ValidatableType]` attribute to the type definition.

```csharp
[ValidatableType]
public class Todo
{
      [Required]
      [Range(1, 10)]
      public int Id { get; set; }

      [StringLength(10)]
      public string Title { get; set; }
}
```

## Implementation Details

### Default Validation Behavior of Validatable Type Info

The `ValidatableTypeInfo.Validate` method follows these steps when validating an object:

1. **Null check**: If the value being validated is null, it immediately returns without validation unless the type is marked as required.

2. **RequiredAttribute handling**: `RequiredAttribute`s are validated before other attributes. If the requiredness check fails, remaining validation attributes are not applied.

3. **Depth limit check**: Before processing nested objects, it checks if the current validation depth exceeds `MaxDepth` (default 32) to prevent stack overflows from circular references or extremely deep object graphs.

4. **Property validation**: Iterates through each property defined in `Members` collection:
   - Gets the property value from the object
   - Applies validation attributes defined on that property
   - For nullable properties, skips validation if the value is null (unless marked required)
   - Handles collections by validating each item in the collection if the property is enumerable

5. **IValidatableObject support**: If the type implements `IValidatableObject`, it calls the `Validate` method after validating individual properties, collecting any additional validation results.

6. **Error aggregation**: Validation errors are added to the `ValidationErrors` dictionary in the context with property names as keys (prefixed if nested) and error messages as values.

7. **Recursive validation**: For properties with complex types that have their own validation requirements, it recursively validates those objects with an updated context prefix to maintain the property path.

### Validation Error Handling

Validation errors are collected in a `Dictionary<string, string[]>` where:
- Keys are property names (including paths for nested properties like `Customer.HomeAddress.Street`)
- Values are arrays of error messages for each property

This format is compatible with ASP.NET Core's `ValidationProblemDetails` for consistent error responses.

### Parameter Validation

The `ValidatableParameterInfo` class provides similar validation for method parameters:

1. Validates attributes applied directly to parameters
2. For complex types, delegates to the appropriate `ValidatableTypeInfo`
3. Supports special handling for common parameter types (primitives, strings, collections)

The validation endpoint filter demonstrates integration with minimal APIs, automatically validating all parameters before the endpoint handler executes.

### Source Generation

The validation system leverages a source generator to:

1. Analyze types marked with `[ValidatableType]` at build time
2. Analyze minimal API endpoints at build-time to automatically discover validatable types without an attribute
3. Generate concrete implementations of `ValidatableTypeInfo` and `ValidatablePropertyInfo`
4. Intercept the `AddValidation` call in user code and add the generated `IValidatableInfoResolver` to the list of resolvers available in the `ValidationOptions`
5. Pre-compiles and caches instances of ValidationAttributes uniquely hashed by their type and initialization arguments

The source generator creates a specialized `IValidatableInfoResolver` implementation that can handle all your validatable types and parameters without runtime reflection overhead.

```csharp
file class GeneratedValidatableInfoResolver : IValidatableInfoResolver
{
    public ValidatableTypeInfo? GetValidatableTypeInfo(Type type)
    {
        // Fast type lookups with no reflection
        if (type == typeof(Customer))
        {
            return CreateCustomerType();
        }
        if (type == typeof(Address))
        {
            return CreateAddressType();
        }
        // Other types...

        return null;
    }

    public ValidatableParameterInfo? GetValidatableParameterInfo(ParameterInfo parameterInfo)
    {
        // ParameterInfo-based validations are resolved at runtime
        return null;
    }

    // Pre-generated factory methods for each type
    private ValidatableTypeInfo CreateCustomerType()
    {
        return new GeneratedValidatableTypeInfo(
            type: typeof(Customer),
            members: [
                // Pre-compiled property validation info
                new GeneratedValidatablePropertyInfo(
                    containingType: typeof(Customer),
                    propertyType: typeof(string),
                    name: "Name",
                    displayName: "Name"),
                // Other properties...
            ]);
    }

    // Other factory methods...
}
```

The generator emits a `ValidationAttributeCache` to support compiling and caching `ValidationAttributes` by their type and arguments.

```csharp
// Generated ValidationAttribute storage and creation
[GeneratedCode("Microsoft.AspNetCore.Http.ValidationsGenerator", "42.42.42.42")]
file static class ValidationAttributeCache
{
      private sealed record CacheKey(global::System.Type ContainingType, string PropertyName);
      private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<CacheKey, global::System.ComponentModel.DataAnnotations.ValidationAttribute[]> _cache = new();

      public static global::System.ComponentModel.DataAnnotations.ValidationAttribute[] GetValidationAttributes(
      global::System.Type containingType,
      string propertyName)
      {
      var key = new CacheKey(containingType, propertyName);
      return _cache.GetOrAdd(key, static k =>
      {
            var property = k.ContainingType.GetProperty(k.PropertyName);
            if (property == null)
            {
                  return [];
            }

            return [.. global::System.Reflection.CustomAttributeExtensions.GetCustomAttributes<global::System.ComponentModel.DataAnnotations.ValidationAttribute>(property, inherit: true)];
      });
      }
}
```

The generator also creates strongly-typed implementations of the abstract validation classes:

```csharp
file sealed class GeneratedValidatablePropertyInfo : ValidatablePropertyInfo
{
    private readonly ValidationAttribute[] _validationAttributes;

    public GeneratedValidatablePropertyInfo(
        Type containingType,
        Type propertyType,
        string name,
        string displayName,
        bool isEnumerable,
        bool isNullable,
        bool isRequired,
        bool hasValidatableType,
        ValidationAttribute[] validationAttributes)
        : base(containingType, propertyType, name, displayName,
              isEnumerable, isNullable, isRequired, hasValidatableType)
    {
        _validationAttributes = validationAttributes;
    }

    protected override ValidationAttribute[] GetValidationAttributes() => _validationAttributes;
}
```

The generator emits an interceptor to the `AddValidation` method that injects the generated `ITypeInfoResolver` into the options object.

```csharp
file static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddValidation(
        this IServiceCollection services,
        Action<ValidationOptions>? configureOptions)
    {
        return ValidationServiceCollectionExtensions.AddValidation(services, options =>
        {
            options.Resolvers.Insert(0, new GeneratedValidatableInfoResolver());
            if (configureOptions is not null)
            {
                configureOptions(options);
            }
        });
    }
}
```

### Validation Extensibility

Similar to existing validation options solutions, users can customize the behavior of the validation system by:

- Custom `ValidationAttribute` implementations
- `IValidatableObject` implementations for complex validation logic

In addition to this, this implementation supports defining vustom validation behavior by defining custom `IValidatableInfoResolver` implementations and inserting them into the `ValidationOptions.Resolvers` property.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation(options =>
{
    // Add custom resolver before the generated one to give it higher priority
    options.Resolvers.Insert(0, new CustomValidatableInfoResolver());
});


var app = builder.Build();

app.MapPost("/payments", (PaymentInfo payment, [FromQuery] decimal amount) =>
{
    // Both payment and amount will be validated using the custom validators
    return TypedResults.Ok(new { PaymentAccepted = true });
});

app.Run();

public class PaymentInfo
{
    public string CreditCardNumber { get; set; } = string.Empty;
    public string CardholderName { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public string CVV { get; set; } = string.Empty;
}

public class CustomValidatableInfoResolver : IValidatableInfoResolver
{
    // Provide validation info for specific types
    public ValidatableTypeInfo? GetValidatableTypeInfo(Type type)
    {
        // Example: Special handling for a specific type
        if (type == typeof(PaymentInfo))
        {
            // Create custom validation rules for PaymentInfo type
            return new CustomPaymentInfoTypeInfo();
        }

        return null; // Return null to let other resolvers handle other types
    }

    // Provide validation info for parameters
    public ValidatableParameterInfo? GetValidatableParameterInfo(ParameterInfo parameterInfo)
    {
        // Example: Special validation for payment amount parameters
        if (parameterInfo.Name == "amount" && parameterInfo.ParameterType == typeof(decimal))
        {
            return new CustomAmountParameterInfo();
        }

        return null; // Return null to let other resolvers handle other parameters
    }

    // Example of custom ValidatableTypeInfo implementation
    private class CustomPaymentInfoTypeInfo : ValidatableTypeInfo
    {
        public CustomPaymentInfoTypeInfo()
            : base(typeof(PaymentInfo), CreateValidatableProperties(), implementsIValidatableObject: false)
        {
        }

        private static IEnumerable<ValidatablePropertyInfo> CreateValidatableProperties()
        {
            // Define custom validation logic for properties
            yield return new CustomPropertyInfo(
                typeof(PaymentInfo),
                typeof(string),
                "CreditCardNumber",
                "Credit Card Number",
                isEnumerable: false,
                isNullable: false,
                isRequired: true,
                hasValidatableType: false);

            // Add more properties as needed
        }
    }

    // Example of custom ValidatableParameterInfo implementation
    private class CustomAmountParameterInfo : ValidatableParameterInfo
    {
        private static readonly ValidationAttribute[] _attributes = new ValidationAttribute[]
        {
            new RangeAttribute(0.01, 10000.00) { ErrorMessage = "Amount must be between $0.01 and $10,000.00" }
        };

        public CustomAmountParameterInfo()
            : base("amount", "Payment Amount", isNullable: false, isRequired: true,
                  hasValidatableType: false, isEnumerable: false)
        {
        }

        protected override ValidationAttribute[] GetValidationAttributes() => _attributes;
    }

    // Example of custom property info implementation
    private class CustomPropertyInfo : ValidatablePropertyInfo
    {
        private static readonly ValidationAttribute[] _ccAttributes = new ValidationAttribute[]
        {
            new CreditCardAttribute(),
            new RequiredAttribute(),
            new StringLengthAttribute(19) { MinimumLength = 13, ErrorMessage = "Credit card number must be between 13 and 19 digits" }
        };

        public CustomPropertyInfo(
            Type containingType, Type propertyType, string name, string displayName,
            bool isEnumerable, bool isNullable, bool isRequired, bool hasValidatableType)
            : base(containingType, propertyType, name, displayName,
                  isEnumerable, isNullable, isRequired, hasValidatableType)
        {
        }

        protected override ValidationAttribute[] GetValidationAttributes() => _ccAttributes;
    }
}
```

## Frequently Asked Questions

## Architecture

### What is an `IValidatableInfoResolver`?
`IValidatableInfoResolver` is an interface that resolves validation information for types and parameters. It defines two methods:
- `TryGetValidatableTypeInfo`: Resolves validation info for a given type
- `TryGetValidatableParameterInfo`: Resolves validation info for method parameters

### What's the difference between compile-time and runtime validation?
- **Compile-time**: Uses source generators to create strongly-typed validation logic during build
- **Runtime**: Uses reflection to discover validation attributes dynamically at execution time

### How do the resolvers work together?
Resolvers are registered in a chain within `ValidationOptions`. When validating an object, the system queries each resolver in sequence until one returns successful validation information.

## Validation Types

### What types of validation are supported?
- Data annotation attributes (e.g., `[Required]`, `[Range]`, `[EmailAddress]`)
- `IValidatableObject` implementation for custom validation
- Complex type validation (nested object graph validation)
- Polymorphic type validation (inheritance hierarchies)
- Recursive type validation

### What is a `ValidatableTypeInfo`?
`ValidatableTypeInfo` represents validation metadata about a type, including its properties and their validation requirements. It's used to validate instances of that type.

### What is a `ValidatableParameterInfo`?
`ValidatableParameterInfo` represents validation metadata for parameters in methods, including validation attributes and type information.

## Configuration

### How do I configure the validation system?
Configure validation by calling the `AddValidation` extension method on `IServiceCollection`, optionally providing a configuration delegate for `ValidationOptions`.

### How do I add custom validation logic?
You can:
1. Implement `IValidatableObject` on your models
2. Create custom validation attributes
3. Implement custom `IValidatableInfoResolver`s

## Advanced Features

### Does the validation system support polymorphic types?
Yes, it can validate properties with base types that might hold derived instances at runtime, ensuring proper validation regardless of the actual concrete type. This validation depends on the validations-source generator and builds on top of the `JsonDerivedType` attribute. 

### How does validation work with complex object graphs?
The system validates deeply nested objects, reporting path-based validation errors (e.g., "PropertyWithInheritance.EmailString").

### How does the system handle recursive types?
It supports recursive types (types that contain themselves either directly or indirectly) by safely traversing the object graph to avoid infinite loops.

### What happens when validation fails?
The system collects validation errors in a `ValidateContext`, mapping property paths to error messages, which can be used to report errors back to users. The minimal APIs implementation captures these errors into a HTTP Validation Problem Details response.

## License
[MIT](https://choosealicense.com/licenses/mit/)

