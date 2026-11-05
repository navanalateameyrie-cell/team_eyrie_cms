import { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import {
  rbacApi,
  type PermissionMetaDto,
  type RoleDto,
  type UserRbacDto,
} from "../../api/rbac";
import { mastersApi, type StaffDto } from "../../api/masters";
import { Button, LoadingSpinner } from "../../components/ui";

export default function RbacPage() {
  const { roles: myRoles, permissions: myPerms, userName: myUserName } = useAuth();

  // Data state
  const [permissions, setPermissions] = useState<PermissionMetaDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [users, setUsers] = useState<UserRbacDto[]>([]);
  const [staffList, setStaffList] = useState<StaffDto[]>([]);

  // Page state
  const [activeTab, setActiveTab] = useState<"matrix" | "users" | "self">("matrix");
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [roleDraftPerms, setRoleDraftPerms] = useState<Record<string, string[]>>({});
  const [loading, setLoading] = useState(true);
  const [savingRole, setSavingRole] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  // Search & Filter
  const [userSearch, setUserSearch] = useState("");
  const [userRoleFilter, setUserRoleFilter] = useState("all");

  // Modals
  const [createRoleModalOpen, setCreateRoleModalOpen] = useState(false);
  const [newRoleName, setNewRoleName] = useState("");
  const [newRoleDesc, setNewRoleDesc] = useState("");
  const [creatingRole, setCreatingRole] = useState(false);

  const [createUserModalOpen, setCreateUserModalOpen] = useState(false);
  const [newUserEmail, setNewUserEmail] = useState("");
  const [newUserPassword, setNewUserPassword] = useState("");
  const [newUserStaffId, setNewUserStaffId] = useState("");
  const [newUserRoles, setNewUserRoles] = useState<string[]>(["StaffUser"]);
  const [creatingUser, setCreatingUser] = useState(false);

  const [editRolesModalUser, setEditRolesModalUser] = useState<UserRbacDto | null>(null);
  const [editUserRoles, setEditUserRoles] = useState<string[]>([]);
  const [updatingUserRoles, setUpdatingUserRoles] = useState(false);

  const [resetPwdModalUser, setResetPwdModalUser] = useState<UserRbacDto | null>(null);
  const [newResetPassword, setNewResetPassword] = useState("");
  const [resettingPwd, setResettingPwd] = useState(false);

  const showToast = (message: string, type: "success" | "error" = "success") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3500);
  };

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [pList, rList, uList, sList] = await Promise.all([
        rbacApi.getPermissions(),
        rbacApi.getRoles(),
        rbacApi.getUsers(),
        mastersApi.staff.list(undefined, true).catch(() => [] as StaffDto[]),
      ]);

      setPermissions(pList);
      setRoles(rList);
      setUsers(uList);
      setStaffList(sList);

      // Initialize draft permissions per role
      const drafts: Record<string, string[]> = {};
      rList.forEach((r) => {
        drafts[r.id] = [...r.permissions];
      });
      setRoleDraftPerms(drafts);

      // Default selected role to first role if none selected
      if (!selectedRoleId && rList.length > 0) {
        // Prefer SystemAdmin or Manager
        const def = rList.find((r) => r.name === "Manager") || rList[0];
        setSelectedRoleId(def.id);
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to load RBAC configuration");
    } finally {
      setLoading(false);
    }
  }, [selectedRoleId]);

  useEffect(() => {
    void loadAll();
  }, []);

  const selectedRole = useMemo(() => {
    return roles.find((r) => r.id === selectedRoleId) || null;
  }, [roles, selectedRoleId]);

  const currentRolePerms = useMemo(() => {
    if (!selectedRoleId) return [];
    return roleDraftPerms[selectedRoleId] || [];
  }, [roleDraftPerms, selectedRoleId]);

  const isRoleDirty = useMemo(() => {
    if (!selectedRole || !selectedRoleId) return false;
    const original = new Set(selectedRole.permissions);
    const draft = new Set(roleDraftPerms[selectedRoleId] || []);
    if (original.size !== draft.size) return true;
    for (const p of draft) {
      if (!original.has(p)) return true;
    }
    return false;
  }, [selectedRole, selectedRoleId, roleDraftPerms]);

  // Group permissions by functional category
  const groupedPermissions = useMemo(() => {
    const map = new Map<string, PermissionMetaDto[]>();
    for (const p of permissions) {
      const list = map.get(p.group) || [];
      list.push(p);
      map.set(p.group, list);
    }
    return Array.from(map.entries());
  }, [permissions]);

  // Permission toggle handler
  const handleTogglePermission = (permId: string) => {
    if (!selectedRoleId) return;
    setRoleDraftPerms((prev) => {
      const list = prev[selectedRoleId] ? [...prev[selectedRoleId]] : [];
      const index = list.indexOf(permId);
      if (index >= 0) {
        list.splice(index, 1);
      } else {
        list.push(permId);
      }
      return { ...prev, [selectedRoleId]: list };
    });
  };

  // Group toggle
  const handleToggleGroup = (groupPerms: PermissionMetaDto[], select: boolean) => {
    if (!selectedRoleId) return;
    setRoleDraftPerms((prev) => {
      const current = new Set(prev[selectedRoleId] || []);
      groupPerms.forEach((p) => {
        if (select) current.add(p.id);
        else current.delete(p.id);
      });
      return { ...prev, [selectedRoleId]: Array.from(current) };
    });
  };

  // Save Role Permissions
  const handleSaveRolePermissions = async () => {
    if (!selectedRole || !selectedRoleId) return;
    setSavingRole(true);
    try {
      const draftPerms = roleDraftPerms[selectedRoleId] || [];
      const updated = await rbacApi.updateRole(selectedRoleId, {
        description: selectedRole.description || undefined,
        permissions: draftPerms,
      });

      setRoles((prev) => prev.map((r) => (r.id === updated.id ? updated : r)));
      showToast(`Permissions for role "${updated.name}" updated successfully.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to update role permissions", "error");
    } finally {
      setSavingRole(false);
    }
  };

  // Create Role
  const handleCreateRole = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newRoleName.trim()) return;
    setCreatingRole(true);
    try {
      const created = await rbacApi.createRole({
        name: newRoleName.trim(),
        description: newRoleDesc.trim() || undefined,
        permissions: [],
      });
      setRoles((prev) => [...prev, created]);
      setRoleDraftPerms((prev) => ({ ...prev, [created.id]: [] }));
      setSelectedRoleId(created.id);
      setCreateRoleModalOpen(false);
      setNewRoleName("");
      setNewRoleDesc("");
      showToast(`Role "${created.name}" created.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to create role", "error");
    } finally {
      setCreatingRole(false);
    }
  };

  // Delete Custom Role
  const handleDeleteRole = async (role: RoleDto) => {
    if (!confirm(`Are you sure you want to permanently delete custom role "${role.name}"?`)) return;
    try {
      await rbacApi.deleteRole(role.id);
      setRoles((prev) => prev.filter((r) => r.id !== role.id));
      if (selectedRoleId === role.id) {
        setSelectedRoleId(roles[0]?.id || null);
      }
      showToast(`Role "${role.name}" deleted.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Cannot delete role", "error");
    }
  };

  // Create User
  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUserEmail.trim()) return;
    setCreatingUser(true);
    try {
      const created = await rbacApi.createUser({
        email: newUserEmail.trim(),
        password: newUserPassword.trim() || undefined,
        staffId: newUserStaffId ? newUserStaffId : undefined,
        roles: newUserRoles,
      });
      setUsers((prev) => [created, ...prev]);
      setCreateUserModalOpen(false);
      setNewUserEmail("");
      setNewUserPassword("");
      setNewUserStaffId("");
      setNewUserRoles(["StaffUser"]);
      showToast(`User account created for ${created.email}.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to create user", "error");
    } finally {
      setCreatingUser(false);
    }
  };

  // Update User Roles
  const handleSaveUserRoles = async () => {
    if (!editRolesModalUser) return;
    setUpdatingUserRoles(true);
    try {
      const updated = await rbacApi.updateUserRoles(editRolesModalUser.id, {
        roles: editUserRoles,
      });
      setUsers((prev) => prev.map((u) => (u.id === updated.id ? updated : u)));
      setEditRolesModalUser(null);
      showToast(`Roles updated for ${updated.email}.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to update roles", "error");
    } finally {
      setUpdatingUserRoles(false);
    }
  };

  // Toggle User Status
  const handleToggleUserStatus = async (user: UserRbacDto) => {
    const nextStatus = !user.isEnabled;
    const actionLabel = nextStatus ? "enable" : "disable";
    if (!confirm(`Are you sure you want to ${actionLabel} account ${user.email}?`)) return;
    try {
      const updated = await rbacApi.toggleUserStatus(user.id, nextStatus);
      setUsers((prev) => prev.map((u) => (u.id === updated.id ? updated : u)));
      showToast(`Account for ${user.email} is now ${nextStatus ? "active" : "disabled"}.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to update account status", "error");
    }
  };

  // Reset Password
  const handleAdminResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetPwdModalUser || !newResetPassword.trim()) return;
    setResettingPwd(true);
    try {
      await rbacApi.adminResetPassword(resetPwdModalUser.id, newResetPassword.trim());
      setResetPwdModalUser(null);
      setNewResetPassword("");
      showToast(`Password successfully reset for ${resetPwdModalUser.email}.`);
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : "Failed to reset password", "error");
    } finally {
      setResettingPwd(false);
    }
  };

  // Filtered users
  const filteredUsers = useMemo(() => {
    return users.filter((u) => {
      const matchSearch =
        !userSearch ||
        u.email.toLowerCase().includes(userSearch.toLowerCase()) ||
        (u.staffName && u.staffName.toLowerCase().includes(userSearch.toLowerCase())) ||
        (u.staffCode && u.staffCode.toLowerCase().includes(userSearch.toLowerCase()));

      const matchRole =
        userRoleFilter === "all" ||
        u.roles.includes(userRoleFilter);

      return matchSearch && matchRole;
    });
  }, [users, userSearch, userRoleFilter]);

  if (loading) {
    return (
      <div className="min-h-[60vh] flex flex-col items-center justify-center p-12">
        <LoadingSpinner size="lg" />
        <p className="mt-4 text-sm font-semibold text-slate-600">Loading Role-Based Access Control matrix...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 pb-12">
      {/* Toast Notification */}
      {toast && (
        <div
          className={`fixed bottom-6 right-6 z-50 px-5 py-3 rounded-xl shadow-2xl border text-sm font-semibold flex items-center gap-3 transition-all transform animate-bounce ${
            toast.type === "success"
              ? "bg-[#102747] text-white border-[#C86A24]"
              : "bg-rose-900 text-white border-rose-700"
          }`}
        >
          <span>{toast.type === "success" ? "✓" : "⚠"}</span>
          <span>{toast.message}</span>
        </div>
      )}

      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-6 rounded-2xl border border-slate-200/80 shadow-sm">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold bg-[#EEF4FB] text-[#102747] border border-[#D4E2F0] mb-2">
            <span className="w-2 h-2 rounded-full bg-[#16A477] animate-pulse" />
            Security & Compliance Governance
          </div>
          <h1 className="text-2xl font-black text-[#102747] tracking-tight">Role-Based Access Control (RBAC)</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Manage practice security profiles, statutory permission matrices, and staff user account assignments.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCreateRoleModalOpen(true)}
            className="border-slate-300 text-slate-700 hover:bg-slate-50 font-semibold"
          >
            + Add Custom Role
          </Button>
          <Button
            size="sm"
            onClick={() => setCreateUserModalOpen(true)}
            className="bg-[#102747] hover:bg-[#0C1E36] text-white font-semibold shadow-sm"
          >
            + Create User Account
          </Button>
        </div>
      </div>

      {error && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-200 text-sm text-rose-800 flex justify-between items-center">
          <span>{error}</span>
          <Button size="sm" variant="outline" onClick={() => void loadAll()}>
            Retry
          </Button>
        </div>
      )}

      {/* KPI Stats Row */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="bg-white p-4 rounded-xl border border-slate-200/80 shadow-sm">
          <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Defined Roles</div>
          <div className="text-2xl font-black text-[#102747] mt-1">{roles.length}</div>
          <div className="text-[11px] text-slate-500 mt-0.5">4 System + {Math.max(0, roles.length - 4)} Custom</div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200/80 shadow-sm">
          <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">User Accounts</div>
          <div className="text-2xl font-black text-[#102747] mt-1">{users.length}</div>
          <div className="text-[11px] text-emerald-600 font-semibold mt-0.5">
            {users.filter((u) => u.isEnabled).length} active accounts
          </div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200/80 shadow-sm">
          <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Granular Permissions</div>
          <div className="text-2xl font-black text-[#102747] mt-1">{permissions.length}</div>
          <div className="text-[11px] text-slate-500 mt-0.5">{groupedPermissions.length} functional domains</div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200/80 shadow-sm">
          <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">My Active Role</div>
          <div className="text-sm font-bold text-[#C86A24] mt-2 truncate">
            {myRoles.join(", ") || "Standard User"}
          </div>
          <div className="text-[11px] text-slate-500 mt-0.5">{myPerms.length} capabilities granted</div>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="flex border-b border-slate-200 bg-white px-4 rounded-t-xl pt-2">
        <button
          type="button"
          onClick={() => setActiveTab("matrix")}
          className={`px-5 py-3 font-semibold text-xs border-b-2 transition-all flex items-center gap-2 ${
            activeTab === "matrix"
              ? "border-[#C86A24] text-[#C86A24] bg-orange-50/40 rounded-t-lg"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
          </svg>
          Role Permission Matrix
        </button>
        <button
          type="button"
          onClick={() => setActiveTab("users")}
          className={`px-5 py-3 font-semibold text-xs border-b-2 transition-all flex items-center gap-2 ${
            activeTab === "users"
              ? "border-[#C86A24] text-[#C86A24] bg-orange-50/40 rounded-t-lg"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
          </svg>
          User Accounts & Assignments ({users.length})
        </button>
        <button
          type="button"
          onClick={() => setActiveTab("self")}
          className={`px-5 py-3 font-semibold text-xs border-b-2 transition-all flex items-center gap-2 ${
            activeTab === "self"
              ? "border-[#C86A24] text-[#C86A24] bg-orange-50/40 rounded-t-lg"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
          </svg>
          My Effective Access
        </button>
      </div>

      {/* ========================================================= */}
      {/* TAB 1: ROLE PERMISSION MATRIX */}
      {/* ========================================================= */}
      {activeTab === "matrix" && (
        <div className="space-y-6">
          {/* Role Selector Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            {roles.map((r) => {
              const isSelected = r.id === selectedRoleId;
              const draftCount = (roleDraftPerms[r.id] || []).length;

              return (
                <div
                  key={r.id}
                  onClick={() => setSelectedRoleId(r.id)}
                  className={`p-4 rounded-xl border text-left cursor-pointer transition-all relative ${
                    isSelected
                      ? "bg-gradient-to-br from-[#102747] to-[#19385F] text-white border-[#C86A24] shadow-md ring-2 ring-[#C86A24]/30"
                      : "bg-white text-slate-800 border-slate-200 hover:border-slate-300 hover:shadow-sm"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span
                      className={`text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-full ${
                        isSelected
                          ? "bg-white/20 text-white"
                          : r.isSystemRole
                          ? "bg-blue-100 text-[#102747]"
                          : "bg-emerald-100 text-emerald-800"
                      }`}
                    >
                      {r.isSystemRole ? "System Role" : "Custom"}
                    </span>
                    <span className={`text-[11px] font-semibold ${isSelected ? "text-slate-200" : "text-slate-500"}`}>
                      {r.userCount} {r.userCount === 1 ? "user" : "users"}
                    </span>
                  </div>

                  <h3 className={`text-base font-bold mt-2 ${isSelected ? "text-white" : "text-[#102747]"}`}>
                    {r.name}
                  </h3>
                  <p className={`text-xs mt-0.5 line-clamp-1 ${isSelected ? "text-slate-300" : "text-slate-500"}`}>
                    {r.description || "No description configured"}
                  </p>

                  <div className="mt-3 pt-2.5 border-t border-white/10 flex items-center justify-between text-xs">
                    <span className={isSelected ? "text-[#FDBA74] font-medium" : "text-slate-600"}>
                      {draftCount} permissions
                    </span>
                    {isSelected && (
                      <span className="text-[10px] font-bold uppercase tracking-wider text-white bg-[#C86A24] px-2 py-0.5 rounded-md">
                        Selected
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Selected Role Editor Banner */}
          {selectedRole && (
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              <div className="bg-[#102747] text-white px-6 py-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                  <div className="flex items-center gap-3">
                    <h2 className="text-lg font-black tracking-tight">{selectedRole.name}</h2>
                    {selectedRole.isSystemRole ? (
                      <span className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-white/20 text-white uppercase tracking-wider">
                        Core Built-In
                      </span>
                    ) : (
                      <span className="px-2 py-0.5 text-[10px] font-bold rounded-md bg-emerald-500/20 text-emerald-200 border border-emerald-400/30 uppercase tracking-wider">
                        Custom Role
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-slate-300 mt-0.5">
                    {selectedRole.description || "Practice standard access role"}
                  </p>
                </div>

                <div className="flex items-center gap-3">
                  {!selectedRole.isSystemRole && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => void handleDeleteRole(selectedRole)}
                      className="border-rose-300 text-rose-200 hover:bg-rose-900/50"
                    >
                      Delete Role
                    </Button>
                  )}
                  <Button
                    size="sm"
                    disabled={savingRole || !isRoleDirty}
                    onClick={() => void handleSaveRolePermissions()}
                    className={`font-semibold shadow transition-all ${
                      isRoleDirty
                        ? "bg-[#C86A24] hover:bg-[#A34E14] text-white ring-2 ring-[#FDBA74]"
                        : "bg-white/20 text-white hover:bg-white/30"
                    }`}
                  >
                    {savingRole ? "Saving..." : isRoleDirty ? "Save Changes *" : "Saved"}
                  </Button>
                </div>
              </div>

              {/* Matrix of Groups */}
              <div className="p-6 divide-y divide-slate-100 space-y-6">
                {groupedPermissions.map(([groupName, groupPerms]) => {
                  return (
                    <div key={groupName} className="pt-6 first:pt-0">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                          <h3 className="font-bold text-sm text-[#102747]">{groupName}</h3>
                          <span className="text-[11px] font-medium text-slate-400">
                            ({groupPerms.filter((p) => currentRolePerms.includes(p.id)).length} / {groupPerms.length})
                          </span>
                        </div>
                        <div className="flex items-center gap-2 text-xs">
                          <button
                            type="button"
                            onClick={() => handleToggleGroup(groupPerms, true)}
                            className="text-[#C86A24] hover:underline font-semibold"
                          >
                            Grant All
                          </button>
                          <span className="text-slate-300">|</span>
                          <button
                            type="button"
                            onClick={() => handleToggleGroup(groupPerms, false)}
                            className="text-slate-500 hover:underline"
                          >
                            Revoke All
                          </button>
                        </div>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                        {groupPerms.map((perm) => {
                          const isChecked = currentRolePerms.includes(perm.id);
                          const isSpecialAdminFull = perm.id === "admin.full";

                          return (
                            <label
                              key={perm.id}
                              className={`flex items-start gap-3 p-3 rounded-xl border text-left cursor-pointer transition-all ${
                                isChecked
                                  ? "bg-blue-50/50 border-[#102747]/20 shadow-xs"
                                  : "bg-slate-50/50 border-slate-200/60 hover:bg-white hover:border-slate-300"
                              }`}
                            >
                              <input
                                type="checkbox"
                                checked={isChecked}
                                onChange={() => handleTogglePermission(perm.id)}
                                className="mt-0.5 h-4 w-4 rounded border-slate-300 text-[#102747] focus:ring-[#C86A24]"
                              />
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center justify-between gap-1">
                                  <span className="text-xs font-bold text-slate-900">{perm.name}</span>
                                  {isSpecialAdminFull && (
                                    <span className="text-[9px] font-bold px-1.5 py-0.2 rounded bg-amber-100 text-amber-800 border border-amber-300">
                                      SUPERADMIN
                                    </span>
                                  )}
                                </div>
                                <code className="block text-[10px] font-mono text-slate-500 mt-0.5">
                                  {perm.id}
                                </code>
                                <p className="text-[11px] text-slate-500 mt-1 leading-snug">
                                  {perm.description}
                                </p>
                              </div>
                            </label>
                          );
                        })}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      )}

      {/* ========================================================= */}
      {/* TAB 2: USER ACCOUNTS & ASSIGNMENTS */}
      {/* ========================================================= */}
      {activeTab === "users" && (
        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          {/* Filter Bar */}
          <div className="p-4 bg-slate-50/60 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-center gap-3 flex-1">
              <input
                type="text"
                placeholder="Search staff user by email, name, or code..."
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
                className="w-full max-w-sm px-3.5 py-2 text-xs rounded-xl border border-slate-300 bg-white shadow-xs focus:ring-1 focus:ring-[#102747] focus:outline-none"
              />
              <select
                value={userRoleFilter}
                onChange={(e) => setUserRoleFilter(e.target.value)}
                className="px-3 py-2 text-xs rounded-xl border border-slate-300 bg-white font-medium text-slate-700 focus:outline-none"
              >
                <option value="all">All Roles</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.name}>
                    {r.name}
                  </option>
                ))}
              </select>
            </div>

            <Button
              size="sm"
              onClick={() => setCreateUserModalOpen(true)}
              className="bg-[#102747] hover:bg-[#0C1E36] text-white font-semibold"
            >
              + Create User
            </Button>
          </div>

          {/* User Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-100/75 border-b border-slate-200 text-slate-600 font-bold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="py-3 px-4">User Account</th>
                  <th className="py-3 px-4">Linked Staff Profile</th>
                  <th className="py-3 px-4">Assigned Roles</th>
                  <th className="py-3 px-4">Status</th>
                  <th className="py-3 px-4">Last Login</th>
                  <th className="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-slate-700">
                {filteredUsers.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-slate-400">
                      No user accounts found matching current search/filter.
                    </td>
                  </tr>
                ) : (
                  filteredUsers.map((u) => {
                    const initials = u.email.substring(0, 2).toUpperCase();

                    return (
                      <tr key={u.id} className="hover:bg-slate-50/80 transition-colors">
                        <td className="py-3.5 px-4">
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-full bg-[#102747] text-white flex items-center justify-center font-bold text-xs shadow-xs">
                              {initials}
                            </div>
                            <div>
                              <div className="font-bold text-slate-900">{u.email}</div>
                              <div className="text-[11px] text-slate-400 font-mono">ID: {u.id.substring(0, 8)}...</div>
                            </div>
                          </div>
                        </td>

                        <td className="py-3.5 px-4">
                          {u.staffName ? (
                            <div>
                              <span className="font-semibold text-slate-900">{u.staffName}</span>
                              {u.staffCode && (
                                <span className="ml-1.5 px-1.5 py-0.5 rounded text-[10px] font-mono bg-slate-100 text-slate-600 border border-slate-200">
                                  {u.staffCode}
                                </span>
                              )}
                            </div>
                          ) : u.clientName ? (
                            <span className="text-slate-600">Client: {u.clientName}</span>
                          ) : (
                            <span className="text-slate-400 italic">No linked staff</span>
                          )}
                        </td>

                        <td className="py-3.5 px-4">
                          <div className="flex flex-wrap gap-1">
                            {u.roles.map((r) => (
                              <span
                                key={r}
                                className={`px-2 py-0.5 rounded-md font-semibold text-[10px] tracking-wide ${
                                  r === "SystemAdmin"
                                    ? "bg-[#102747] text-white"
                                    : r === "Manager"
                                    ? "bg-blue-100 text-blue-900 border border-blue-200"
                                    : r === "StaffUser"
                                    ? "bg-orange-100 text-orange-900 border border-orange-200"
                                    : "bg-emerald-100 text-emerald-900 border border-emerald-200"
                                }`}
                              >
                                {r}
                              </span>
                            ))}
                          </div>
                        </td>

                        <td className="py-3.5 px-4">
                          {u.isEnabled ? (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                              Active
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-medium bg-rose-50 text-rose-700 border border-rose-200">
                              <span className="w-1.5 h-1.5 rounded-full bg-rose-500" />
                              Disabled
                            </span>
                          )}
                        </td>

                        <td className="py-3.5 px-4 text-slate-500 text-[11px]">
                          {u.lastLoginAtUtc
                            ? new Date(u.lastLoginAtUtc).toLocaleDateString("en-IN", {
                                day: "2-digit",
                                month: "short",
                                year: "numeric",
                                hour: "2-digit",
                                minute: "2-digit",
                              })
                            : "Never"}
                        </td>

                        <td className="py-3.5 px-4 text-right">
                          <div className="inline-flex items-center gap-1.5">
                            <button
                              type="button"
                              onClick={() => {
                                setEditRolesModalUser(u);
                                setEditUserRoles([...u.roles]);
                              }}
                              className="px-2.5 py-1 text-xs font-semibold rounded-md border border-slate-300 text-slate-700 hover:bg-slate-100 transition-colors"
                            >
                              Edit Roles
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setResetPwdModalUser(u);
                                setNewResetPassword("");
                              }}
                              className="px-2.5 py-1 text-xs font-semibold rounded-md border border-slate-300 text-slate-700 hover:bg-slate-100 transition-colors"
                            >
                              Reset Pwd
                            </button>
                            <button
                              type="button"
                              onClick={() => void handleToggleUserStatus(u)}
                              className={`px-2.5 py-1 text-xs font-semibold rounded-md border transition-colors ${
                                u.isEnabled
                                  ? "border-rose-200 text-rose-700 hover:bg-rose-50"
                                  : "border-emerald-200 text-emerald-700 hover:bg-emerald-50"
                              }`}
                            >
                              {u.isEnabled ? "Disable" : "Enable"}
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ========================================================= */}
      {/* TAB 3: SELF EFFECTIVE ACCESS (MY ACCESS) */}
      {/* ========================================================= */}
      {activeTab === "self" && (
        <div className="space-y-6">
          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
            <h2 className="text-base font-bold text-[#102747]">Active Session Identity & Credentials</h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Current authenticated session verified by ASP.NET Core Identity & JWT claims token.
            </p>

            <div className="mt-4 grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200">
                <span className="text-slate-400 font-medium">Account / Username</span>
                <div className="font-bold text-slate-900 mt-1">{myUserName}</div>
              </div>
              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200">
                <span className="text-slate-400 font-medium">Assigned Role Membership</span>
                <div className="flex gap-1 flex-wrap mt-1">
                  {myRoles.map((r) => (
                    <span key={r} className="px-2 py-0.5 rounded font-bold bg-blue-100 text-[#102747] text-[11px]">
                      {r}
                    </span>
                  ))}
                </div>
              </div>
              <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200">
                <span className="text-slate-400 font-medium">Total Granted Permissions</span>
                <div className="font-black text-xl text-[#C86A24] mt-1">{myPerms.length}</div>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
            <h2 className="text-base font-bold text-[#102747] mb-4">Effective Permission Grants</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2.5">
              {myPerms.map((p) => (
                <div
                  key={p}
                  className="px-3 py-2 rounded-lg bg-emerald-50/60 border border-emerald-200 flex items-center justify-between text-xs"
                >
                  <code className="font-mono text-emerald-900 text-[11px] font-semibold">{p}</code>
                  <span className="text-emerald-600 font-bold">✓</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* ========================================================= */}
      {/* MODAL: CREATE ROLE */}
      {/* ========================================================= */}
      {createRoleModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 max-w-md w-full p-6 text-left">
            <h3 className="text-lg font-bold text-[#102747]">Create New Practice Role</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Define a new practice role and configure custom permission matrix grants.
            </p>

            <form onSubmit={handleCreateRole} className="mt-4 space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Role Name *</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. SeniorTaxReviewer, AuditLead"
                  value={newRoleName}
                  onChange={(e) => setNewRoleName(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Description (Optional)</label>
                <textarea
                  rows={2}
                  placeholder="Brief description of responsibilities..."
                  value={newRoleDesc}
                  onChange={(e) => setNewRoleDesc(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setCreateRoleModalOpen(false)}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  disabled={creatingRole || !newRoleName.trim()}
                  className="bg-[#102747] text-white"
                >
                  {creatingRole ? "Creating..." : "Create Role"}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================= */}
      {/* MODAL: CREATE USER ACCOUNT */}
      {/* ========================================================= */}
      {createUserModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 max-w-lg w-full p-6 text-left">
            <h3 className="text-lg font-bold text-[#102747]">Create Staff User Account</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Provision login credentials and assign security roles to practice staff.
            </p>

            <form onSubmit={handleCreateUser} className="mt-4 space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">Email Address *</label>
                <input
                  type="email"
                  required
                  placeholder="staff.name@cms.local"
                  value={newUserEmail}
                  onChange={(e) => setNewUserEmail(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Initial Password (Optional — defaults to <code className="font-mono text-slate-800">Staff!23</code>)
                </label>
                <input
                  type="password"
                  placeholder="Leave blank for Staff!23"
                  value={newUserPassword}
                  onChange={(e) => setNewUserPassword(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Link to Staff Directory Profile (Optional)
                </label>
                <select
                  value={newUserStaffId}
                  onChange={(e) => setNewUserStaffId(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                >
                  <option value="">-- No Linked Staff Member --</option>
                  {staffList.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.fullName} ({s.email}) — [{s.code}]
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">Assign Role(s)</label>
                <div className="grid grid-cols-2 gap-2">
                  {roles.map((r) => {
                    const checked = newUserRoles.includes(r.name);
                    return (
                      <label
                        key={r.id}
                        className={`flex items-center gap-2 p-2 rounded-lg border text-xs cursor-pointer ${
                          checked ? "bg-blue-50 border-blue-300 font-semibold text-[#102747]" : "border-slate-200"
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => {
                            if (checked) {
                              setNewUserRoles(newUserRoles.filter((name) => name !== r.name));
                            } else {
                              setNewUserRoles([...newUserRoles, r.name]);
                            }
                          }}
                          className="h-3.5 w-3.5 text-[#102747]"
                        />
                        <span>{r.name}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setCreateUserModalOpen(false)}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  disabled={creatingUser || !newUserEmail.trim()}
                  className="bg-[#102747] text-white"
                >
                  {creatingUser ? "Creating..." : "Create Account"}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================= */}
      {/* MODAL: EDIT USER ROLES */}
      {/* ========================================================= */}
      {editRolesModalUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 max-w-md w-full p-6 text-left">
            <h3 className="text-lg font-bold text-[#102747]">Edit User Roles</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Assign or revoke roles for <strong className="text-slate-800">{editRolesModalUser.email}</strong>.
            </p>

            <div className="mt-4 space-y-2">
              {roles.map((r) => {
                const checked = editUserRoles.includes(r.name);
                return (
                  <label
                    key={r.id}
                    className={`flex items-center justify-between p-3 rounded-xl border text-xs cursor-pointer ${
                      checked
                        ? "bg-blue-50/70 border-blue-300 font-semibold text-[#102747]"
                        : "border-slate-200 hover:bg-slate-50"
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => {
                          if (checked) {
                            setEditUserRoles(editUserRoles.filter((n) => n !== r.name));
                          } else {
                            setEditUserRoles([...editUserRoles, r.name]);
                          }
                        }}
                        className="h-4 w-4 text-[#102747]"
                      />
                      <span>{r.name}</span>
                    </div>
                    <span className="text-[10px] text-slate-400">{r.permissions.length} perms</span>
                  </label>
                );
              })}
            </div>

            <div className="flex justify-end gap-2 pt-4 border-t border-slate-100 mt-5">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setEditRolesModalUser(null)}
              >
                Cancel
              </Button>
              <Button
                size="sm"
                disabled={updatingUserRoles}
                onClick={() => void handleSaveUserRoles()}
                className="bg-[#102747] text-white"
              >
                {updatingUserRoles ? "Saving..." : "Save Roles"}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================= */}
      {/* MODAL: RESET PASSWORD */}
      {/* ========================================================= */}
      {resetPwdModalUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-xs p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 max-w-sm w-full p-6 text-left">
            <h3 className="text-lg font-bold text-[#102747]">Reset Account Password</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Set a new password for <strong className="text-slate-800">{resetPwdModalUser.email}</strong>.
            </p>

            <form onSubmit={handleAdminResetPassword} className="mt-4 space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">New Password *</label>
                <input
                  type="password"
                  required
                  minLength={8}
                  placeholder="Minimum 8 characters"
                  value={newResetPassword}
                  onChange={(e) => setNewResetPassword(e.target.value)}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-1 focus:ring-[#102747]"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setResetPwdModalUser(null)}
                >
                  Cancel
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  disabled={resettingPwd || newResetPassword.length < 8}
                  className="bg-[#102747] text-white"
                >
                  {resettingPwd ? "Resetting..." : "Set Password"}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
