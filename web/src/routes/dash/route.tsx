import {
  createFileRoute,
  Link,
  Outlet,
  useMatches,
} from "@tanstack/react-router"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupAction,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInput,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarRail,
  SidebarTrigger,
} from "@/components/ui/sidebar"
import { HardDrive, Lock, User } from "lucide-react"
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb.tsx"
import { Separator } from "@/components/ui/separator.tsx"
import { Fragment } from "react"

export const Route = createFileRoute("/dash")({
  component: RouteComponent,
})

function RouteComponent() {
  const matches = useMatches()
  const breadcrumbMatches = matches.filter(
    (match) => match.staticData?.breadcrumb
  )
  // @ts-ignore
  const SIDEBAR_KEYBOARD_SHORTCUT = "b"
  // @ts-ignore
  const SIDEBAR_WIDTH = "16rem"
  // @ts-ignore
  const SIDEBAR_WIDTH_MOBILE = "18rem"
  return (
    <SidebarProvider>
      <Sidebar variant="sidebar" collapsible="offcanvas">
        <SidebarHeader />
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel>应用</SidebarGroupLabel>
            <SidebarGroupAction></SidebarGroupAction>
            <SidebarGroupContent>
              <SidebarInput />
            </SidebarGroupContent>

            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton
                  render={
                    <Link
                      to="/dash/drive"
                      activeProps={{
                        "data-active": true,
                      }}
                    />
                  }
                >
                  <HardDrive /> <span>存储</span>
                </SidebarMenuButton>
                <SidebarMenuBadge />
              </SidebarMenuItem>
              <SidebarMenuItem>
                <SidebarMenuButton
                  render={
                    <Link
                      to="/dash/vault"
                      activeProps={{
                        "data-active": true,
                      }}
                    />
                  }
                >
                  <Lock /> <span>密码</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroup>
        </SidebarContent>
        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton>
                <User /> <span>本地用户</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>
        <SidebarRail />
      </Sidebar>

      <main className="flex flex-1 flex-col gap-6 p-6">
        <header className="flex h-5 items-center gap-4">
          <SidebarTrigger size="icon" />
          <Separator orientation="vertical" />
          <Breadcrumb>
            <BreadcrumbList>
              {breadcrumbMatches.map((match, index) => {
                const isLast = index === breadcrumbMatches.length - 1
                const label = match.staticData!.breadcrumb as string
                return (
                  <Fragment key={match.id}>
                    {index > 0 && <BreadcrumbSeparator />}
                    <BreadcrumbItem>
                      {isLast ? (
                        <BreadcrumbPage>{label}</BreadcrumbPage>
                      ) : (
                        <BreadcrumbLink render={<Link to={match.pathname} />}>
                          {label}
                        </BreadcrumbLink>
                      )}
                    </BreadcrumbItem>
                  </Fragment>
                )
              })}
            </BreadcrumbList>
          </Breadcrumb>
        </header>
        <div className="mx-auto w-full max-w-7xl">
          <Outlet />
        </div>
      </main>
    </SidebarProvider>
  )
}
