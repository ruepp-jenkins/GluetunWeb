import { useId, useState } from 'react'

/**
 * Multi-value combobox for comma-separated fields (e.g. SERVER_COUNTRIES). Current values render as
 * removable chips; a datalist dropdown suggests options (picking one adds it instantly), and typing
 * a custom value + Enter adds it too. The bound value stays a comma-joined string, so free-text
 * still works when no options are available.
 */
export function TagSelect({
  value,
  onChange,
  options,
  placeholder,
}: {
  value: string | null
  onChange: (v: string | null) => void
  options: string[]
  placeholder?: string
}) {
  const listId = useId()
  const [draft, setDraft] = useState('')

  const tags = (value ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
  const available = options.filter((o) => !tags.includes(o))

  const commit = (next: string[]) => onChange(next.length ? next.join(', ') : null)
  const add = (raw: string) => {
    const v = raw.trim()
    setDraft('')
    if (v && !tags.includes(v)) commit([...tags, v])
  }
  const remove = (t: string) => commit(tags.filter((x) => x !== t))

  return (
    <div className="border border-line bg-panel-2 p-1.5">
      {tags.length > 0 && (
        <div className="mb-1.5 flex flex-wrap gap-1">
          {tags.map((t) => (
            <span
              key={t}
              className="inline-flex items-center gap-1 border border-phosphor-dim/60 bg-phosphor/10 px-1.5 py-0.5 text-[11px] text-phosphor"
            >
              {t}
              <button
                type="button"
                onClick={() => remove(t)}
                className="text-phosphor-dim hover:text-danger"
                aria-label={`remove ${t}`}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}
      <input
        list={listId}
        value={draft}
        onChange={(e) => {
          const v = e.target.value
          // Picking an exact option from the dropdown adds it immediately.
          if (options.includes(v)) add(v)
          else setDraft(v)
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault()
            add(draft)
          }
        }}
        placeholder={placeholder ?? (available.length ? 'type or pick · Enter to add' : 'type · Enter to add')}
        className="w-full bg-transparent px-1 py-0.5 text-[13px] text-ink placeholder:text-faint outline-none"
      />
      <datalist id={listId}>
        {available.map((o) => (
          <option key={o} value={o} />
        ))}
      </datalist>
    </div>
  )
}
