import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dash/vault/')({
  component: RouteComponent,
  staticData:{
    breadcrumb:"密钥",
  }
})

function RouteComponent() {
  return <div>Hello "/dash/vault/"!</div>
}
