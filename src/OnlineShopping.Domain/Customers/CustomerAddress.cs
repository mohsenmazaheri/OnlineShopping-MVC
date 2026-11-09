using OnlineShopping.Domain.Common;

namespace OnlineShopping.Domain.Customers;

/// <summary>
/// A shipping address in a customer's address book. Orders copy the address they ship to (snapshot), so editing or
/// deleting an entry never changes an order. Entries are deleted for real (personal data), not soft-deleted.
/// </summary>
public class CustomerAddress : BaseEntity
{
    public const int MaxPerCustomer = 20;

    public required string CustomerId { get; set; }

    /// <summary>Optional name the customer gives it ("Home", "Office").</summary>
    public string? Label { get; set; }

    public required string RecipientName { get; set; }

    /// <summary>E.164, e.g. +989121234567 (the courier calls this number).</summary>
    public required string PhoneNumber { get; set; }

    /// <summary>ISO 3166-1 alpha-2, one of the store's shipping countries.</summary>
    public required string CountryCode { get; set; }

    /// <summary>Province / state / region.</summary>
    public string? Region { get; set; }

    public required string City { get; set; }

    /// <summary>Street, number (plaque), building.</summary>
    public required string Line1 { get; set; }

    /// <summary>Floor, unit, extra directions.</summary>
    public string? Line2 { get; set; }

    public string? PostalCode { get; set; }

    public bool IsDefault { get; set; }
}
