import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dash/drive/')({
  component: RouteComponent,
  staticData:{
    breadcrumb:"存储",
  }
})

function RouteComponent() {
  return <div>Hello "/dash/drive/"!</div>
}
