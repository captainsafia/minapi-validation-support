using System.ComponentModel.DataAnnotations;

namespace Models;

public record Product(
    int Id,

    [property: Required(ErrorMessage = "Name is required")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    string Name,

    [property: Required(ErrorMessage = "Description is required")]
    [property: StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    string Description,

    [property: Required(ErrorMessage = "Price is required")]
    [property: Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10000")]
    decimal Price,

    [property: Required(ErrorMessage = "Category is required")]
    [property: StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    string Category,

    [property: Range(0, 10000, ErrorMessage = "Inventory must be between 0 and 10000")]
    int InventoryCount,

    [property: DataType(DataType.Date)]
    DateTime CreatedAt
);
