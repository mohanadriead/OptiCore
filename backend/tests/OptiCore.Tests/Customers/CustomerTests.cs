using OptiCore.Domain.Customers;

namespace OptiCore.Tests.Customers;

public class CustomerTests
{
    [Fact]
    public void CreateCustomer_WithValidData_CreatesActiveCustomer()
    {
        // Arrange
        var employeeId = Guid.CreateVersion7();

        // Act
        var customer = new Customer(
            nationalId: "123456789",
            firstName: "Mohanad",
            lastName: "Test",
            dateOfBirth: new DateOnly(2000, 1, 15),
            mobilePhone: "0501234567",
            city: "Haifa",
            gender: "Male",
            createdByEmployeeId: employeeId);

        // Assert
        Assert.Equal("123456789", customer.NationalId);
        Assert.Equal("Mohanad", customer.FirstName);
        Assert.Equal("Test", customer.LastName);
        Assert.Equal("0501234567", customer.MobilePhone);
        Assert.Equal("Haifa", customer.City);

        Assert.True(customer.IsActive);

        Assert.Equal(employeeId, customer.CreatedByEmployeeId);
        Assert.NotEqual(default, customer.CreatedAtUtc);

        Assert.Null(customer.UpdatedAtUtc);
        Assert.Null(customer.UpdatedByEmployeeId);
    }

    [Fact]
    public void CreateCustomer_WithEmptyFirstName_ThrowsArgumentException()
    {
        var employeeId = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() =>
            new Customer(
                nationalId: "123456789",
                firstName: "",
                lastName: "Test",
                dateOfBirth: new DateOnly(2000, 1, 15),
                mobilePhone: "0501234567",
                city: "Haifa",
                gender: "Male",
                createdByEmployeeId: employeeId));
    }

    [Fact]
    public void CreateCustomer_TrimsTextValues()
    {
        var customer = new Customer(
            nationalId: " 123456789 ",
            firstName: " Mohanad ",
            lastName: " Test ",
            dateOfBirth: new DateOnly(2000, 1, 15),
            mobilePhone: " 0501234567 ",
            city: " Haifa ",
            gender: " Male ",
            createdByEmployeeId: Guid.CreateVersion7());

        Assert.Equal("123456789", customer.NationalId);
        Assert.Equal("Mohanad", customer.FirstName);
        Assert.Equal("Test", customer.LastName);
        Assert.Equal("0501234567", customer.MobilePhone);
        Assert.Equal("Haifa", customer.City);
        Assert.Equal("Male", customer.Gender);
    }

    [Fact]
    public void Deactivate_MakesCustomerInactiveAndRecordsUpdater()
    {
        var creatorId = Guid.CreateVersion7();
        var updaterId = Guid.CreateVersion7();

        var customer = CreateValidCustomer(creatorId);

        customer.Deactivate(updaterId);

        Assert.False(customer.IsActive);
        Assert.Equal(updaterId, customer.UpdatedByEmployeeId);
        Assert.NotNull(customer.UpdatedAtUtc);
    }

    [Fact]
    public void SetWhatsAppConsent_UpdatesConsentAndAuditMetadata()
    {
        var customer = CreateValidCustomer(Guid.CreateVersion7());
        var updaterId = Guid.CreateVersion7();

        Assert.False(customer.WhatsAppConsent);

        customer.SetWhatsAppConsent(true, updaterId);

        Assert.True(customer.WhatsAppConsent);
        Assert.Equal(updaterId, customer.UpdatedByEmployeeId);
        Assert.NotNull(customer.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_ChangesCustomerDetails()
    {
        var customer = CreateValidCustomer(Guid.CreateVersion7());
        var updaterId = Guid.CreateVersion7();

        customer.UpdateDetails(
            firstName: "Ahmad",
            lastName: "Ali",
            dateOfBirth: new DateOnly(1995, 5, 20),
            mobilePhone: "0529999999",
            city: "Nazareth",
            gender: "Male",
            updatedByEmployeeId: updaterId,
            email: "ahmad@example.com");

        Assert.Equal("Ahmad", customer.FirstName);
        Assert.Equal("Ali", customer.LastName);
        Assert.Equal("0529999999", customer.MobilePhone);
        Assert.Equal("Nazareth", customer.City);
        Assert.Equal("ahmad@example.com", customer.Email);

        Assert.Equal(updaterId, customer.UpdatedByEmployeeId);
        Assert.NotNull(customer.UpdatedAtUtc);
    }

    private static Customer CreateValidCustomer(Guid employeeId)
    {
        return new Customer(
            nationalId: "123456789",
            firstName: "Mohanad",
            lastName: "Test",
            dateOfBirth: new DateOnly(2000, 1, 15),
            mobilePhone: "0501234567",
            city: "Haifa",
            gender: "Male",
            createdByEmployeeId: employeeId);
    }
}