import { useNavigate } from "react-router-dom";
import { PageHeader } from "../../components/ui";
import ClientForm from "./ClientForm";

export default function ClientCreatePage() {
  const nav = useNavigate();
  return (
    <div className="space-y-6 max-w-4xl">
      <PageHeader
        breadcrumbs={[
          { label: "Clients", to: "/admin/clients" },
          { label: "New Client" },
        ]}
        title="Register New Client"
        subtitle="Create a new client entity, configure legal details and PAN verification."
      />
      <ClientForm
        onSaved={(c) => nav(`/admin/clients/${c.id}`, { replace: true })}
        onCancel={() => nav("/admin/clients")}
      />
    </div>
  );
}

