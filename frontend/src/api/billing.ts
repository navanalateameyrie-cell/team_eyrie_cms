import { apiFetch } from "./client";

export interface BillingAddress {
  line1: string;
  line2: string | null;
  city: string;
  pincode: string;
}

export interface ActiveFileRef {
  id: string;
  fileNumber: string;
  serviceCode: string;
  serviceName: string;
  openedOn: string;
}

export interface BillingContextDto {
  clientId: string;
  clientCode: string;
  legalName: string;
  tradeName: string | null;
  pan: string | null;
  branchId: string;
  branchCode: string;
  branchName: string;
  branchType: string;
  branchIsPrimary: boolean;
  branchIsBillingEnabled: boolean;
  branchIsActive: boolean;
  stateCode: string | null;
  stateName: string | null;
  countryCode: string;
  address: BillingAddress;
  gstin: string | null;
  activeServiceFiles: ActiveFileRef[];
}

export const billingApi = {
  forBranch: (clientId: string, branchId: string) =>
    apiFetch<BillingContextDto>(`/api/v1/clients/${clientId}/branches/${branchId}/billing-context`),
  forPrimary: (clientId: string) =>
    apiFetch<BillingContextDto>(`/api/v1/clients/${clientId}/billing-context`),
};
