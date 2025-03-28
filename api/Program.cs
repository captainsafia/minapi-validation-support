// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Validation;
using Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddValidation();

var app = builder.Build();

// ValidationEndpointFilterFactory is implicitly enabled on all endpoints
app.MapGet("/customers/{id}", ([Range(1, int.MaxValue)] int id) =>
    $"Getting customer with ID: {id}");

app.MapPost("/customers", (Customer customer) => TypedResults.Created($"/customers/{customer.Name}", customer));

app.MapPost("/orders", (Order order) => TypedResults.Created($"/orders/{order.OrderId}", order));

app.MapPost("/products",
    ([EvenNumber(ErrorMessage = "Product ID must be even")] int productId, [Required] string name)
        => TypedResults.Ok(productId))
    .DisableValidation();

app.MapPost("/product", ([Required] Product product) =>
{
    return TypedResults.Created($"/products/{product.Id}", product);
});

app.MapPost("/stores", ([Required] Store store) =>
{
    return TypedResults.Created($"/stores/{store.Id}", store);
});

app.Run();

// Define validatable types with the ValidatableType attribute
[ValidatableType]
public class Customer
{
    [Required]
    public required string Name { get; set; }

    [EmailAddress]
    public required string Email { get; set; }

    [Range(18, 120)]
    [Display(Name = "Customer Age")]
    public int Age { get; set; }

    // Complex property with nested validation
    public Address HomeAddress { get; set; } = new Address
    {
        Street = "123 Main St",
        City = "Anytown",
        ZipCode = "12345"
    };
}

public class Address
{
    [Required]
    public required string Street { get; set; }

    [Required]
    public required string City { get; set; }

    [StringLength(5)]
    public required string ZipCode { get; set; }
}

// Define a type implementing IValidatableObject for custom validation
public class Order : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    public required string ProductName { get; set; }

    public int Quantity { get; set; }

    // Custom validation logic using IValidatableObject
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Quantity <= 0)
        {
            yield return new ValidationResult(
                "Quantity must be greater than zero",
                [nameof(Quantity)]);
        }
    }
}

// Use a custom validation attribute
public class EvenNumberAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is int number)
        {
            return number % 2 == 0;
        }
        return false;
    }
}

[JsonSerializable(typeof(Customer))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Address))]
[JsonSerializable(typeof(Store))]
[JsonSerializable(typeof(Product))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}