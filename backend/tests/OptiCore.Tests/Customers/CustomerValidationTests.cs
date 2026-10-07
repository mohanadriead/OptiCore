using OptiCore.Domain.Customers;

namespace OptiCore.Tests.Customers;

public sealed class CustomerValidationTests
{
    private static Customer Create(string? id = null, string? mobile = null, string? home = null, string gender = "Male") =>
        new(id ?? new string('0', 9), "First", "Last", new DateOnly(2000, 1, 1), mobile ?? new string('0', 10),
            "City", gender, Guid.NewGuid(), homePhone: home);

    [Fact]
    public void ValidDigitsPreserveLeadingZerosAndTrimming()
    {
        var customer = Create(" " + new string('0', 9) + " ", " " + new string('0', 10) + " ", new string('0', 9));
        Assert.Equal(new string('0', 9), customer.NationalId);
        Assert.Equal(new string('0', 10), customer.MobilePhone);
        Assert.Equal(new string('0', 9), customer.HomePhone);
    }

    [Fact]
    public void InvalidNationalIdsAreRejectedWithoutEchoingValues()
    {
        foreach (var value in new[] { "Jax", new string('1', 8), new string('1', 10), new string('1', 8) + "A", new string('١', 9), new string('１', 9) })
        {
            var error = Assert.Throws<ArgumentException>(() => Create(id: value));
            Assert.Equal("National ID must contain exactly 9 ASCII digits.", error.Message);
        }
    }

    [Fact]
    public void InvalidMobileAndHomeAreRejected()
    {
        foreach (var value in new[] { new string('1', 9), new string('1', 11), new string('1', 9) + "A", new string('١', 10), new string('1', 4) + "-" + new string('1', 5) })
            Assert.Throws<ArgumentException>(() => Create(mobile: value));
        foreach (var value in new[] { new string('1', 8), new string('1', 10), "letters", new string('١', 9) })
            Assert.Throws<ArgumentException>(() => Create(home: value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void OptionalHomeNormalizesToNull(string? home) => Assert.Null(Create(home: home).HomePhone);

    [Theory]
    [InlineData("Male")]
    [InlineData("Female")]
    public void CanonicalGenderAccepted(string gender) => Assert.Equal(gender, Create(gender: gender).Gender);

    [Theory]
    [InlineData("Other")]
    [InlineData("male")]
    [InlineData("זכר")]
    public void OtherGenderRejected(string gender) => Assert.Throws<ArgumentException>(() => Create(gender: gender));

    [Fact]
    public void InvalidUpdatesAreAtomicAndValidUpdatePreservesInactiveState()
    {
        var customer = Create();
        foreach (var invalid in new[] { (new string('0', 9), (string?)null, "Male"),
            (new string('0', 10), "bad-home", "Male"), (new string('0', 10), (string?)null, "Other") })
        {
            Assert.Throws<ArgumentException>(() => customer.UpdateDetails("Changed", "Name", new DateOnly(2001, 2, 3),
                invalid.Item1, "City", invalid.Item3, Guid.NewGuid(), homePhone: invalid.Item2));
            Assert.Equal("First", customer.FirstName);
            Assert.Null(customer.UpdatedAtUtc);
        }
        customer.Deactivate(Guid.NewGuid());
        customer.UpdateDetails("Changed", "Name", new DateOnly(2001, 2, 3), new string('0', 10), "City", "Female", Guid.NewGuid(), homePhone: new string('0', 9));
        Assert.Equal("Female", customer.Gender);
        Assert.False(customer.IsActive);
    }
}
