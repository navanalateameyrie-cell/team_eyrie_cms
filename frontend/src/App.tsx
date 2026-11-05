import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/AuthContext";
import Layout from "./components/Layout";
import { RequireAuth, RequirePermission } from "./components/RequireAuth";
import HomePage from "./pages/HomePage";
import LoginPage from "./pages/LoginPage";
import ForgotPasswordPage from "./pages/ForgotPasswordPage";
import ResetPasswordPage from "./pages/ResetPasswordPage";
import PortalLayout from "./pages/portal/PortalLayout";
import PortalInvoicesPage from "./pages/portal/PortalInvoicesPage";
import PortalObligationsPage from "./pages/portal/PortalObligationsPage";
import PortalDocumentsPage from "./pages/portal/PortalDocumentsPage";
import EntityTypesPage from "./pages/masters/EntityTypesPage";
import StatesPage from "./pages/masters/StatesPage";
import ServicesPage from "./pages/masters/ServicesPage";
import StaffPage from "./pages/masters/StaffPage";
import ClientGroupsPage from "./pages/masters/ClientGroupsPage";
import ClientsListPage from "./pages/clients/ClientsListPage";
import ClientCreatePage from "./pages/clients/ClientCreatePage";
import ClientProfilePage from "./pages/clients/ClientProfilePage";
import StaffWorkloadPage from "./pages/staff/StaffWorkloadPage";
import DashboardPage from "./pages/DashboardPage";
import ImportsListPage from "./pages/imports/ImportsListPage";
import ImportBatchPage from "./pages/imports/ImportBatchPage";
import AuditExplorerPage from "./pages/audit/AuditExplorerPage";
import CompliancePage from "./pages/compliance/CompliancePage";
import LetterTemplatesPage from "./pages/letters/LetterTemplatesPage";
import BillingPage from "./pages/billing/BillingPage";
import ReconcilePage from "./pages/billing/ReconcilePage";
import ReportsPage from "./pages/reports/ReportsPage";
import SettingsPage from "./pages/settings/SettingsPage";
import RbacPage from "./pages/settings/RbacPage";
import MfaEnrolPage from "./pages/MfaEnrolPage";
import TimesheetPage from "./pages/timesheet/TimesheetPage";

function RootRoute() {
  const { token, hasPermission } = useAuth();
  if (token && hasPermission("clients.read")) {
    return <Navigate to="/dashboard" replace />;
  }
  return <HomePage />;
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/portal/login" element={<LoginPage />} />
          <Route path="/portal/reset-password" element={<ResetPasswordPage />} />
          <Route path="/portal" element={<PortalLayout />}>
            <Route index element={<PortalInvoicesPage />} />
            <Route path="obligations" element={<PortalObligationsPage />} />
            <Route path="documents" element={<PortalDocumentsPage />} />
          </Route>
          <Route element={<Layout />}>
            <Route index element={<RootRoute />} />
            <Route path="home" element={<HomePage />} />
            <Route
              path="dashboard"
              element={
                <RequirePermission permission="clients.read">
                  <DashboardPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/clients"
              element={
                <RequirePermission permission="clients.read">
                  <ClientsListPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/clients/new"
              element={
                <RequirePermission permission="clients.write">
                  <ClientCreatePage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/clients/:id"
              element={
                <RequirePermission permission="clients.read">
                  <ClientProfilePage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/audit"
              element={
                <RequirePermission permission="audit.read">
                  <AuditExplorerPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/imports"
              element={
                <RequirePermission permission="imports.read">
                  <ImportsListPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/imports/:id"
              element={
                <RequirePermission permission="imports.read">
                  <ImportBatchPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/compliance"
              element={
                <RequirePermission permission="compliance.read">
                  <CompliancePage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/letter-templates"
              element={
                <RequirePermission permission="letters.read">
                  <LetterTemplatesPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/billing"
              element={
                <RequirePermission permission="billing.read">
                  <BillingPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/billing/reconcile"
              element={
                <RequirePermission permission="billing.read">
                  <ReconcilePage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/reports"
              element={
                <RequirePermission permission="reports.read">
                  <ReportsPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/settings"
              element={
                <RequirePermission permission="settings.manage">
                  <SettingsPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/rbac"
              element={
                <RequirePermission permission="users.manage">
                  <RbacPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/timesheet"
              element={
                <RequirePermission permission="timesheet.read">
                  <TimesheetPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/account/mfa"
              element={
                <RequireAuth>
                  <MfaEnrolPage />
                </RequireAuth>
              }
            />
            <Route
              path="admin/staff-workload"
              element={
                <RequirePermission permission="staff.read">
                  <StaffWorkloadPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/masters/entity-types"
              element={
                <RequirePermission permission="masters.read">
                  <EntityTypesPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/masters/states"
              element={
                <RequirePermission permission="masters.read">
                  <StatesPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/masters/services"
              element={
                <RequirePermission permission="masters.read">
                  <ServicesPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/masters/staff"
              element={
                <RequirePermission permission="masters.read">
                  <StaffPage />
                </RequirePermission>
              }
            />
            <Route
              path="admin/masters/client-groups"
              element={
                <RequirePermission permission="masters.read">
                  <ClientGroupsPage />
                </RequirePermission>
              }
            />
            <Route
              path="protected-example"
              element={
                <RequireAuth>
                  <div>Signed-in content.</div>
                </RequireAuth>
              }
            />
            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
