using System.ComponentModel.DataAnnotations;

namespace Models;

public class Store
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Store name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required")]
    [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "State must be a 2-character code")]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "State must be a 2-letter uppercase code")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zip code is required")]
    [StringLength(10, ErrorMessage = "Zip code cannot exceed 10 characters")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Zip code must be in format 12345 or 12345-6789")]
    public string ZipCode { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format")]
    [Required(ErrorMessage = "Phone number is required")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string? Email { get; set; }

    [Range(0, 24, ErrorMessage = "Opening hours must be between 0 and 24")]
    public int OpeningHour { get; set; } = 9;

    [Range(0, 24, ErrorMessage = "Closing hours must be between 0 and 24")]
    public int ClosingHour { get; set; } = 17;

    [DataType(DataType.Date)]
    public DateTime EstablishedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public List<string> StoreAmenities { get; set; } = new();
}