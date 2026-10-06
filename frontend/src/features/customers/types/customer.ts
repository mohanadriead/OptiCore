export interface CustomerDetails {
  firstName: string; lastName: string; dateOfBirth: string; mobilePhone: string;
  homePhone: string | null; email: string | null; city: string; street: string | null;
  gender: string; notes: string | null;
}
export interface CreateCustomerRequest extends CustomerDetails { nationalId: string; whatsAppConsent: boolean }
export type UpdateCustomerRequest = CustomerDetails
export interface Customer extends CreateCustomerRequest {
  id: string; customerNumber: number; isActive: boolean; createdAtUtc: string; updatedAtUtc: string | null;
}
