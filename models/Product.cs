using System.ComponentModel.DataAnnotations;

namespace Models;

public record Product(
    int Id,

    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    string Name,

    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    string Description,

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 10000, ErrorMessage = "Price must be between 0.01 and 10000")]
    decimal Price,

    [Required(ErrorMessage = "Category is required")]
    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    string Category,

    [Range(0, 10000, ErrorMessage = "Inventory must be between 0 and 10000")]
    int InventoryCount,

    [DataType(DataType.Date)]
    DateTime CreatedAt
);
