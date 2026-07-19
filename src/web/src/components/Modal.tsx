import type { ReactNode } from 'react'

/** Simple centered modal used for create/edit forms. Scrolls internally on small screens. */
export function Modal({
  title,
  onClose,
  children,
  footer,
  wide,
}: {
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  wide?: boolean
}) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/70 p-4 sm:p-8"
      onMouseDown={onClose}
    >
      <div
        className={`my-auto w-full border border-line-bright bg-panel shadow-2xl ${wide ? 'max-w-3xl' : 'max-w-xl'}`}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header className="flex items-center justify-between border-b border-line bg-panel-2 px-3 py-2">
          <h2 className="text-[12px] uppercase tracking-widest text-phosphor">{title}</h2>
          <button
            onClick={onClose}
            className="text-muted hover:text-danger"
            aria-label="Close"
          >
            [x]
          </button>
        </header>
        <div className="max-h-[70vh] overflow-y-auto p-4">{children}</div>
        {footer && (
          <footer className="flex justify-end gap-2 border-t border-line bg-panel-2 px-3 py-2">
            {footer}
          </footer>
        )}
      </div>
    </div>
  )
}
