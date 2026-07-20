import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

/** How row-action buttons render: full words, colour emoji, or monochrome ascii glyphs. */
export type ButtonStyle = 'text' | 'icons' | 'ascii'

export const BUTTON_STYLES: { value: ButtonStyle; label: string; hint: string }[] = [
  { value: 'ascii', label: 'ascii', hint: 'monochrome glyphs (▲ ▶ ■) — compact, matches the terminal theme' },
  { value: 'icons', label: 'icons', hint: 'colour emoji (🚀 ▶ ⏹) — compact' },
  { value: 'text', label: 'text', hint: 'full words (deploy, start, stop) — clearest, but widest' },
]

const STORAGE_KEY = 'gluetunweb.buttonStyle'
const DEFAULT: ButtonStyle = 'ascii'

function read(): ButtonStyle {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    if (v === 'text' || v === 'icons' || v === 'ascii') return v
  } catch {
    /* localStorage unavailable (private mode) — fall back to the default */
  }
  return DEFAULT
}

interface Ctx {
  style: ButtonStyle
  setStyle: (s: ButtonStyle) => void
}

const ButtonStyleCtx = createContext<Ctx | null>(null)

/**
 * A per-browser display preference — it only affects how buttons look, so it lives in localStorage
 * rather than the server settings. Defaults to ascii.
 */
export function ButtonStyleProvider({ children }: { children: ReactNode }) {
  const [style, setStyleState] = useState<ButtonStyle>(read)

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, style)
    } catch {
      /* ignore */
    }
  }, [style])

  return (
    <ButtonStyleCtx.Provider value={{ style, setStyle: setStyleState }}>
      {children}
    </ButtonStyleCtx.Provider>
  )
}

export function useButtonStyle(): Ctx {
  const ctx = useContext(ButtonStyleCtx)
  if (!ctx) throw new Error('useButtonStyle must be used within ButtonStyleProvider')
  return ctx
}
