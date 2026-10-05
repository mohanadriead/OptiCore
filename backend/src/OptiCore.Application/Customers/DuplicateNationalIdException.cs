namespace OptiCore.Application.Customers;

public sealed class DuplicateNationalIdException() : Exception("A customer with this NationalId already exists.");
