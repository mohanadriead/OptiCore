using OptiCore.Domain.Common;

namespace OptiCore.Domain.Customers;

public class Customer : AuditableEntity
{
    public int CustomerNumber { get; private set; }

    public string NationalId { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public DateOnly DateOfBirth { get; private set; }

    public string MobilePhone { get; private set; } = string.Empty;

    public string? HomePhone { get; private set; }

    public string? Email { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string? Street { get; private set; }

    public string Gender { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public bool WhatsAppConsent { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Customer()
    {
    }

    public Customer(
        string nationalId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        string mobilePhone,
        string city,
        string gender,
        Guid createdByEmployeeId,
        string? homePhone = null,
        string? email = null,
        string? street = null,
        string? notes = null,
        bool whatsAppConsent = false)
        : base(createdByEmployeeId)
    {
        ValidateRequiredFields(
            nationalId,
            firstName,
            lastName,
            mobilePhone,
            city,
            gender);

        ValidateFormats(nationalId, mobilePhone, homePhone, gender);

        NationalId = nationalId.Trim();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DateOfBirth = dateOfBirth;
        MobilePhone = mobilePhone.Trim();
        HomePhone = NormalizeOptional(homePhone);
        Email = NormalizeOptional(email);
        City = city.Trim();
        Street = NormalizeOptional(street);
        Gender = gender.Trim();
        Notes = NormalizeOptional(notes);
        WhatsAppConsent = whatsAppConsent;
    }

    public void UpdateDetails(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        string mobilePhone,
        string city,
        string gender,
        Guid updatedByEmployeeId,
        string? homePhone = null,
        string? email = null,
        string? street = null,
        string? notes = null)
    {
        ValidateRequiredFields(
            NationalId,
            firstName,
            lastName,
            mobilePhone,
            city,
            gender);

        ValidateFormats(NationalId, mobilePhone, homePhone, gender);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        DateOfBirth = dateOfBirth;
        MobilePhone = mobilePhone.Trim();
        HomePhone = NormalizeOptional(homePhone);
        Email = NormalizeOptional(email);
        City = city.Trim();
        Street = NormalizeOptional(street);
        Gender = gender.Trim();
        Notes = NormalizeOptional(notes);

        MarkUpdated(updatedByEmployeeId);
    }

    public void SetWhatsAppConsent(
        bool consent,
        Guid updatedByEmployeeId)
    {
        WhatsAppConsent = consent;

        MarkUpdated(updatedByEmployeeId);
    }

    public void Deactivate(Guid updatedByEmployeeId)
    {
        IsActive = false;

        MarkUpdated(updatedByEmployeeId);
    }

    private static void ValidateRequiredFields(
        string nationalId,
        string firstName,
        string lastName,
        string mobilePhone,
        string city,
        string gender)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
            throw new ArgumentException("National ID is required.");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.");

        if (string.IsNullOrWhiteSpace(mobilePhone))
            throw new ArgumentException("Mobile phone is required.");

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.");

        if (string.IsNullOrWhiteSpace(gender))
            throw new ArgumentException("Gender is required.");
    }

    private static void ValidateFormats(string nationalId, string mobilePhone, string? homePhone, string gender)
    {
        AsciiDigits.Required(nationalId, 9, "National ID");
        AsciiDigits.Required(mobilePhone, 10, "Mobile phone");
        AsciiDigits.Optional(homePhone, 9, "Home phone");
        if (gender.Trim() is not ("Male" or "Female"))
            throw new ArgumentException("Gender must be Male or Female.");
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
