namespace CMS.Domain.Entities;

/// <summary>Physical address owned by a Branch. State is the branch's jurisdiction FK, not duplicated here.</summary>
public class Address
{
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string Pincode { get; private set; } = string.Empty;

    private Address() { }

    public Address(string line1, string city, string pincode, string? line2 = null)
    {
        if (string.IsNullOrWhiteSpace(line1)) throw new ArgumentException("Address line1 is required", nameof(line1));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required", nameof(city));
        if (string.IsNullOrWhiteSpace(pincode)) throw new ArgumentException("Pincode is required", nameof(pincode));

        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        Pincode = pincode.Trim();
    }
}
