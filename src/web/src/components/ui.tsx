import type { ButtonHTMLAttributes, ReactNode } from 'react'

type Variant = 'primary' | 'ghost' | 'danger' | 'amber'

const variantCls: Record<Variant, string> = {
  primary:
    'border-phosphor-dim text-phosphor hover:bg-phosphor/10 hover:border-phosphor',
  ghost: 'border-line text-ink hover:bg-panel-2 hover:border-line-bright',
  danger: 'border-danger-dim text-danger hover:bg-danger/10 hover:border-danger',
  amber: 'border-amber/50 text-amber hover:bg-amber/10 hover:border-amber',
}

export function Button({
  variant = 'ghost',
  children,
  className = '',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return (
    <button
      className={`inline-flex items-center gap-1.5 border px-2.5 py-1 text-[12px] uppercase tracking-wide transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${variantCls[variant]} ${className}`}
      {...rest}
    >
      {children}
    </button>
  )
}

export function Toggle({
  checked,
  onChange,
  label,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  label?: string
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="inline-flex items-center gap-2 text-[12px]"
    >
      <span
        className={`flex h-4 w-8 items-center border px-0.5 transition-colors ${
          checked ? 'border-phosphor-dim bg-phosphor/20' : 'border-line bg-panel'
        }`}
      >
        <span
          className={`h-2.5 w-2.5 transition-transform ${
            checked ? 'translate-x-4 bg-phosphor' : 'translate-x-0 bg-muted'
          }`}
        />
      </span>
      {label && <span className="uppercase tracking-wide text-muted">{label}</span>}
    </button>
  )
}

const statusColors: Record<string, string> = {
  running: 'text-phosphor border-phosphor-dim',
  created: 'text-muted border-line',
  exited: 'text-amber border-amber/50',
  dead: 'text-danger border-danger-dim',
  error: 'text-danger border-danger-dim',
  restarting: 'text-amber border-amber/50',
}

export function StatusBadge({ status }: { status: string }) {
  const cls = statusColors[status] ?? 'text-muted border-line'
  return (
    <span className={`inline-flex items-center gap-1 border px-1.5 py-px text-[11px] uppercase ${cls}`}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {status}
    </span>
  )
}

export function Panel({
  title,
  children,
  right,
}: {
  title?: ReactNode
  children: ReactNode
  right?: ReactNode
}) {
  return (
    <section className="border border-line bg-panel">
      {title && (
        <header className="flex items-center justify-between border-b border-line bg-panel-2 px-3 py-2">
          <h2 className="text-[12px] uppercase tracking-widest text-phosphor">{title}</h2>
          {right}
        </header>
      )}
      <div className="p-3">{children}</div>
    </section>
  )
}

export function Spinner({ label }: { label?: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-muted">
      <span className="inline-block h-3 w-3 animate-spin border border-phosphor border-t-transparent" />
      {label}
    </span>
  )
}

export function Banner({
  kind,
  children,
}: {
  kind: 'error' | 'ok' | 'info' | 'warn'
  children: ReactNode
}) {
  const cls =
    kind === 'error'
      ? 'border-danger-dim text-danger'
      : kind === 'ok'
        ? 'border-phosphor-dim text-phosphor'
        : kind === 'warn'
          ? 'border-amber/50 text-amber'
          : 'border-cyan/40 text-cyan'
  return <div className={`border ${cls} bg-panel-2 px-3 py-2 text-[12px]`}>{children}</div>
}

export function EmptyRow({ colSpan, children }: { colSpan: number; children: ReactNode }) {
  return (
    <tr>
      <td colSpan={colSpan} className="px-3 py-8 text-center text-muted">
        {children}
      </td>
    </tr>
  )
}
