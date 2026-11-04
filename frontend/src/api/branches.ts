import { apiFetch } from "./client";

export type BranchType = "HeadOffice" | "Branch";
export type TaxRegistrationType = "Gstin" | "Tan" | "Pt" | "Pf" | "Esic" | "Iec";

export interface AddressDto {
  line1: string;
  line2: string | null;
  city: string;
  pincode: string;
}

export interface BranchDto {
  id: string;
  clientId: string;
  code: string;
  name: string;
  type: BranchType;
  stateId: string;
  stateName: string | null;
  address: AddressDto;
  isPrimary: boolean;
  isBillingEnabled: boolean;
  isActive: boolean;
  openedOn: string;
  closedOn: string | null;
  version: number;
}

export interface CreateBranchBody {
  code: string;
  name: string;
  type: BranchType;
  stateId: string;
  address: AddressDto;
  openedOn: string;
  isPrimary: boolean;
  isBillingEnabled: boolean;
}

export interface UpdateBranchBody {
  code: string;
  name: string;
  type: BranchType;
  stateId: string;
  address: AddressDto;
  isBillingEnabled: boolean;
  version: number;
}

export interface TaxRegistrationDto {
  id: string;
  branchId: string;
  type: TaxRegistrationType;
  number: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  isActive: boolean;
  version: number;
}

export interface TaxRegistrationUpsertBody {
  type: TaxRegistrationType;
  number: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

export const branchesApi = {
  list: (clientId: string, active?: boolean) => {
    const qs = active === undefined ? "" : `?active=${active}`;
    return apiFetch<BranchDto[]>(`/api/v1/clients/${clientId}/branches${qs}`);
  },
  create: (clientId: string, body: CreateBranchBody) =>
    apiFetch<BranchDto>(`/api/v1/clients/${clientId}/branches`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  update: (clientId: string, id: string, body: UpdateBranchBody) =>
    apiFetch<BranchDto>(`/api/v1/clients/${clientId}/branches/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  makePrimary: (clientId: string, id: string, version: number) =>
    apiFetch<BranchDto>(
      `/api/v1/clients/${clientId}/branches/${id}/make-primary?version=${version}`,
      { method: "POST" }
    ),
  close: (clientId: string, id: string, version: number, closedOn?: string) => {
    const d = closedOn ? `&closedOn=${closedOn}` : "";
    return apiFetch<BranchDto>(
      `/api/v1/clients/${clientId}/branches/${id}/close?version=${version}${d}`,
      { method: "POST" }
    );
  },
  reopen: (clientId: string, id: string, version: number) =>
    apiFetch<BranchDto>(
      `/api/v1/clients/${clientId}/branches/${id}/reopen?version=${version}`,
      { method: "POST" }
    ),
};

export const taxRegistrationsApi = {
  list: (clientId: string, branchId: string) =>
    apiFetch<TaxRegistrationDto[]>(
      `/api/v1/clients/${clientId}/branches/${branchId}/tax-registrations`
    ),
  create: (clientId: string, branchId: string, body: TaxRegistrationUpsertBody) =>
    apiFetch<TaxRegistrationDto>(
      `/api/v1/clients/${clientId}/branches/${branchId}/tax-registrations`,
      { method: "POST", body: JSON.stringify(body) }
    ),
  update: (clientId: string, branchId: string, id: string, version: number, body: TaxRegistrationUpsertBody) =>
    apiFetch<TaxRegistrationDto>(
      `/api/v1/clients/${clientId}/branches/${branchId}/tax-registrations/${id}?version=${version}`,
      { method: "PUT", body: JSON.stringify(body) }
    ),
  deactivate: (clientId: string, branchId: string, id: string, version: number) =>
    apiFetch<void>(
      `/api/v1/clients/${clientId}/branches/${branchId}/tax-registrations/${id}?version=${version}`,
      { method: "DELETE" }
    ),
};
