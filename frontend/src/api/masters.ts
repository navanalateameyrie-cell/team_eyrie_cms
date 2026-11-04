import { apiFetch } from "./client";

export interface MasterBase {
  id: string;
  isActive: boolean;
}

export interface EntityTypeDto extends MasterBase {
  code: string;
  name: string;
  description: string | null;
  displayOrder: number;
}
export interface StateDto extends MasterBase {
  code: string;
  name: string;
  countryCode: string;
  displayOrder: number;
}
export interface ServiceDto extends MasterBase {
  code: string;
  name: string;
  category: string | null;
  description: string | null;
  displayOrder: number;
  requiredFieldsJson?: string | null;
  optionalFieldsJson?: string | null;
}

export interface ServiceFieldDef {
  name: string;
  label: string;
  type: "string" | "date" | "number" | "select";
  required?: boolean;
  pattern?: string | null;
  options?: string[] | null;
  placeholder?: string | null;
  helpText?: string | null;
}

export interface ServiceSchemaDto {
  serviceId: string;
  code: string;
  name: string;
  required: ServiceFieldDef[];
  optional: ServiceFieldDef[];
}
export interface StaffDto extends MasterBase {
  code: string;
  fullName: string;
  email: string;
  phone: string | null;
  role: string | null;
}
export interface ClientGroupDto extends MasterBase {
  code: string;
  name: string;
  description: string | null;
}

function crud<TDto extends MasterBase, TUpsert>(resource: string) {
  const base = `/api/v1/masters/${resource}`;
  return {
    list: (search?: string, active?: boolean) => {
      const params = new URLSearchParams();
      if (search) params.set("search", search);
      if (active !== undefined) params.set("active", String(active));
      const qs = params.toString();
      return apiFetch<TDto[]>(qs ? `${base}?${qs}` : base);
    },
    get: (id: string) => apiFetch<TDto>(`${base}/${id}`),
    create: (body: TUpsert) =>
      apiFetch<TDto>(base, { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: TUpsert) =>
      apiFetch<TDto>(`${base}/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    deactivate: (id: string) =>
      apiFetch<void>(`${base}/${id}`, { method: "DELETE" }),
  };
}

export interface StaffUpsertBody {
  fullName: string;
  email: string;
  code: string | null;
  phone: string | null;
  role: string | null;
}

const servicesCrud = crud<ServiceDto, Omit<ServiceDto, "id" | "isActive">>("services");
export const mastersApi = {
  entityTypes: crud<EntityTypeDto, Omit<EntityTypeDto, "id" | "isActive">>("entity-types"),
  states: crud<StateDto, Omit<StateDto, "id" | "isActive">>("states"),
  services: {
    ...servicesCrud,
    schema: (id: string) => apiFetch<ServiceSchemaDto>(`/api/v1/masters/services/${id}/schema`),
  },
  staff: crud<StaffDto, StaffUpsertBody>("staff"),
  clientGroups: crud<ClientGroupDto, Omit<ClientGroupDto, "id" | "isActive">>("client-groups"),
};
