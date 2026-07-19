import type {
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from 'react'
import type { EnvDocKey } from '../data/envCatalog'
import { EnvVarDoc } from './EnvVarDoc'

const base =
  'w-full border bg-panel-2 px-2 py-1 text-[13px] text-ink placeholder:text-faint outline-none ' +
  'focus:border-phosphor-dim transition-colors'

function ring(error?: string) {
  return error ? 'border-danger-dim' : 'border-line'
}

/** Label + control + always-visible env doc + inline validation error. */
export function Field({
  label,
  doc,
  error,
  required,
  children,
}: {
  label: string
  doc?: EnvDocKey
  error?: string
  required?: boolean
  children: ReactNode
}) {
  return (
    <div>
      <label className="mb-1 flex items-center gap-1 text-[11px] uppercase tracking-wide text-muted">
        {label}
        {required && <span className="text-danger">*</span>}
      </label>
      {children}
      {error && <div className="mt-1 text-[11px] text-danger">▸ {error}</div>}
      {doc && <EnvVarDoc doc={doc} />}
    </div>
  )
}

export function Input({ error, className = '', ...rest }: InputHTMLAttributes<HTMLInputElement> & { error?: string }) {
  return <input className={`${base} ${ring(error)} ${className}`} {...rest} />
}

export function TextArea({
  error,
  className = '',
  ...rest
}: TextareaHTMLAttributes<HTMLTextAreaElement> & { error?: string }) {
  return <textarea className={`${base} ${ring(error)} resize-y ${className}`} {...rest} />
}

export function Select({
  error,
  className = '',
  children,
  ...rest
}: SelectHTMLAttributes<HTMLSelectElement> & { error?: string }) {
  return (
    <select className={`${base} ${ring(error)} ${className}`} {...rest}>
      {children}
    </select>
  )
}
