import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { useButtonStyle } from '../context/ButtonStyleContext'

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

/** Every row action, with its word plus its glyph in each compact display mode. */
export type ActionKey =
  | 'deploy'
  | 'redeploy'
  | 'start'
  | 'stop'
  | 'restart'
  | 'logs'
  | 'test'
  | 'info'
  | 'edit'
  | 'del'

interface ActionGlyphs {
  word: string
  icons: string // colour emoji
  ascii: string // monochrome glyph
}

/**
 * Single source of truth for row actions across every page, so the same action always looks the
 * same. `word` drives text mode (and the tooltip everywhere); `icons`/`ascii` are the compact modes.
 */
export const ACTIONS: Record<ActionKey, ActionGlyphs> = {
  deploy: { word: 'deploy', icons: '🚀', ascii: '▲' },
  redeploy: { word: 'redeploy', icons: '🚀', ascii: '↥' },
  start: { word: 'start', icons: '▶', ascii: '▶' },
  stop: { word: 'stop', icons: '⏹', ascii: '■' },
  restart: { word: 'restart', icons: '🔁', ascii: '↻' },
  logs: { word: 'logs', icons: '📃', ascii: '≡' },
  test: { word: 'test', icons: '🧪', ascii: '✓' },
  info: { word: 'info', icons: 'ℹ', ascii: 'ℹ' },
  edit: { word: 'edit', icons: '✎', ascii: '✎' },
  del: { word: 'del', icons: '🗑', ascii: '✕' },
}

const BUSY = { text: '···', icons: '⏳', ascii: '…' } as const

/**
 * Compact single-glyph action button. Keeps the bordered terminal look of {@link Button} but shows
 * an icon instead of text, with the word exposed via title/aria-label. Used to build
 * {@link ActionButton}; prefer that so the display mode is honoured.
 */
export function IconButton({
  icon,
  label,
  variant = 'ghost',
  className = '',
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { icon: ReactNode; label: string; variant?: Variant }) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      className={`inline-flex h-7 w-7 items-center justify-center border text-[13px] leading-none transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${variantCls[variant]} ${className}`}
      {...rest}
    >
      <span aria-hidden="true">{icon}</span>
    </button>
  )
}

/**
 * A row action rendered per the user's display preference: full-width word (text), or a compact
 * square glyph (icons / ascii). The word is always the tooltip. `busy` swaps in a working indicator
 * and `label` overrides the shown word (e.g. an orphan's "remove" reusing the `del` action).
 */
export function ActionButton({
  action,
  variant = 'ghost',
  busy = false,
  label,
  ...rest
}: Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> & {
  action: ActionKey
  variant?: Variant
  busy?: boolean
  label?: string
}) {
  const { style } = useButtonStyle()
  const glyphs = ACTIONS[action]
  const word = label ?? glyphs.word

  if (style === 'text') {
    // Actions are never form submits — default to type="button" so a text-mode button placed inside
    // a <form> (e.g. the settings preview) doesn't submit it. A caller can still override via rest.
    return (
      <Button type="button" variant={variant} {...rest}>
        {busy ? BUSY.text : word}
      </Button>
    )
  }

  const glyph = style === 'ascii' ? glyphs.ascii : glyphs.icons
  return <IconButton variant={variant} label={word} icon={busy ? BUSY[style] : glyph} {...rest} />
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
