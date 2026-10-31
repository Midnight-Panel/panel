import { serversApi } from "@/api/servers";
import { useAuth } from "@/auth/AuthContext";
import { PageLoader, RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { TabNav } from "@/components/shared/tab-nav";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { BoxIcon, ChipIcon, CodeIcon, GearIcon, LayoutGridIcon, PencilIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { ServerStateBadge, ServerStatusBadge } from "@/pages/servers/components/badges";
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { Link, Outlet, useNavigate, useParams } from "react-router";
import { useAdminServerQuery } from "./hooks";

function ServerContent() {
  const { id } = useParams();
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const server = useAdminServerQuery(Number(id));
  const uuid = server.data?.uuid;

  const resources = useQuery({
    queryKey: ["servers", uuid, "resources"],
    queryFn: () => serversApi.resources(uuid!),
    enabled: Boolean(uuid),
    refetchInterval: 10_000,
  });

  useEffect(() => {
    if (server.error) {
      notifyError(server.error);
      navigate("/admin/servers");
    }
  }, [server.error, navigate]);

  if (!server.data) return <PageLoader />;

  const base = `/admin/servers/${server.data.id}`;
  const write = can(Permissions.AdminServersWrite);

  return (
    <>
      <PageHeader
        icon={<ChipIcon />}
        title={server.data.name}
        description={
          <span className="font-mono text-xs">
            {server.data.primaryAllocation ?? "-"} · {server.data.uuid}
          </span>
        }
        crumbs={[{ label: t("Nav.Admin.Servers"), to: "/admin/servers" }]}
        actions={
          <>
            <ServerStatusBadge status={server.data.status} suspended={server.data.suspended} />
            <ServerStateBadge state={resources.data?.state} nodeOnline={resources.data?.nodeOnline ?? true} />
            <Button variant="outline" size="sm" render={<Link to={`/servers/${server.data.uuid}`} />} nativeButton={false}>
              <CodeIcon />
              {t("Servers.OpenConsole")}
            </Button>
          </>
        }
      />

      <TabNav
        items={[
          { label: t("Account.Overview"), to: base, icon: <LayoutGridIcon /> },
          ...(write
            ? [
                { label: t("Servers.Tabs.Details"), to: `${base}/details`, icon: <PencilIcon /> },
                { label: t("Servers.Tabs.Build"), to: `${base}/build`, icon: <ChipIcon /> },
                { label: t("Servers.Tabs.Startup"), to: `${base}/startup`, icon: <BoxIcon /> },
                { label: t("Servers.Tabs.Manage"), to: `${base}/manage`, icon: <GearIcon /> },
              ]
            : []),
        ]}
      />

      <Outlet context={server.data} />
    </>
  );
}

export default function AdminServerLayout() {
  return (
    <RequirePermission permission={Permissions.AdminServersRead}>
      <ServerContent />
    </RequirePermission>
  );
}
