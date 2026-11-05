import { apiFetch } from "./client";

export interface PermissionMetaDto {
  id: string;
  name: string;
  description: string;
  group: string;
}

export interface SpecialPermDto {
  id: string;
  name: string;
  description: string;
}

export interface RbacModuleDto {
  key: string;
  name: string;
  group: string;
  menuPerm: string | null;
  createPerm: string | null;
  updatePerm: string | null;
  deletePerm: string | null;
  specialPerms: SpecialPermDto[];
}

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  userCount: number;
  permissions: string[];
}

export interface CreateRoleRequest {
  name: string;
  description?: string;
  permissions?: string[];
}

export interface UpdateRoleRequest {
  description?: string;
  permissions: string[];
}

export interface UserRbacDto {
  id: string;
  email: string;
  userName: string;
  staffId: string | null;
  staffName: string | null;
  staffCode: string | null;
  clientId: string | null;
  clientName: string | null;
  roles: string[];
  isEnabled: boolean;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface CreateUserRequest {
  email: string;
  password?: string;
  staffId?: string;
  roles?: string[];
  isEnabled?: boolean;
}

export interface UpdateUserRolesRequest {
  roles: string[];
}

export interface UpdateUserStatusRequest {
  isEnabled: boolean;
}

export const rbacApi = {
  getModules: () => apiFetch<RbacModuleDto[]>("/api/v1/rbac/modules"),

  getPermissions: () => apiFetch<PermissionMetaDto[]>("/api/v1/rbac/permissions"),

  getRoles: () => apiFetch<RoleDto[]>("/api/v1/rbac/roles"),

  createRole: (data: CreateRoleRequest) =>
    apiFetch<RoleDto>("/api/v1/rbac/roles", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateRole: (roleId: string, data: UpdateRoleRequest) =>
    apiFetch<RoleDto>(`/api/v1/rbac/roles/${roleId}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  deleteRole: (roleId: string) =>
    apiFetch<void>(`/api/v1/rbac/roles/${roleId}`, {
      method: "DELETE",
    }),

  getUsers: () => apiFetch<UserRbacDto[]>("/api/v1/rbac/users"),

  createUser: (data: CreateUserRequest) =>
    apiFetch<UserRbacDto>("/api/v1/rbac/users", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateUserRoles: (userId: string, data: UpdateUserRolesRequest) =>
    apiFetch<UserRbacDto>(`/api/v1/rbac/users/${userId}/roles`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),

  toggleUserStatus: (userId: string, isEnabled: boolean) =>
    apiFetch<UserRbacDto>(`/api/v1/rbac/users/${userId}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isEnabled }),
    }),

  adminResetPassword: (userId: string, newPassword: string) =>
    apiFetch<{ status: string; message: string }>(`/api/v1/rbac/users/${userId}/reset-password`, {
      method: "POST",
      body: JSON.stringify({ newPassword }),
    }),
};
