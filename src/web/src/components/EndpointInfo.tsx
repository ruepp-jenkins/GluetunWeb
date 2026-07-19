import { useState } from 'react'
import type { ReactNode } from 'react'
import { Modal } from './Modal'
import { Banner, Button } from './ui'

/** One ready-to-use endpoint line with a copy button. */
export function EndpointRow({
  label,
  value,
  note,
  href,
}: {
  label: string
  value: string
  note?: ReactNode
  /** When set, the value opens in a new tab. Only for things a browser can actually render. */
  href?: string
}) {
  return (
    <div className="border border-line bg-panel-2 px-3 py-2">
      <div className="mb-1 flex items-center justify-between gap-3">
        <span className="text-[11px] uppercase tracking-widest text-muted">{label}</span>
        <CopyButton value={value} />
      </div>
      {href ? (
        <a
          href={href}
          target="_blank"
          rel="noreferrer"
          className="block break-all text-[12px] text-cyan underline hover:text-phosphor"
        >
          {value} ↗
        </a>
      ) : (
        <code className="block break-all text-[12px] text-phosphor">{value}</code>
      )}
      {note && <p className="mt-1 text-[11px] text-faint">{note}</p>}
    </div>
  )
}

/**
 * Copies to the clipboard, falling back to a hidden textarea + execCommand: the async Clipboard API
 * is unavailable on pages served over plain HTTP from anything but localhost, which is exactly how
 * this dashboard is usually reached.
 */
export function CopyButton({ value, label = 'copy' }: { value: string; label?: string }) {
  const [done, setDone] = useState(false)

  async function copy() {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(value)
      } else {
        const ta = document.createElement('textarea')
        ta.value = value
        ta.style.position = 'fixed'
        ta.style.opacity = '0'
        document.body.appendChild(ta)
        ta.select()
        document.execCommand('copy')
        document.body.removeChild(ta)
      }
      setDone(true)
      setTimeout(() => setDone(false), 1200)
    } catch {
      /* clipboard blocked — the value is selectable on screen anyway */
    }
  }

  return (
    <Button variant="ghost" onClick={copy} className="!py-0.5 !text-[10px]">
      {done ? 'copied' : label}
    </Button>
  )
}

/** A copyable multi-line block (compose snippets). */
export function CodeBlock({ title, code }: { title: string; code: string }) {
  return (
    <div className="border border-line bg-panel-2">
      <div className="flex items-center justify-between gap-3 border-b border-line px-3 py-1.5">
        <span className="text-[11px] uppercase tracking-widest text-muted">{title}</span>
        <CopyButton value={code} />
      </div>
      <pre className="overflow-x-auto px-3 py-2 text-[11px] leading-relaxed text-ink">{code}</pre>
    </div>
  )
}

export function InfoModal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <Modal
      title={title}
      onClose={onClose}
      wide
      footer={
        <Button variant="ghost" onClick={onClose}>
          close
        </Button>
      }
    >
      <div className="grid gap-3">{children}</div>
    </Modal>
  )
}

/** Section heading inside an info modal. */
export function InfoSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="grid gap-2">
      <h3 className="text-[11px] uppercase tracking-widest text-phosphor-dim">{title}</h3>
      {children}
    </section>
  )
}

export function NotDeployedNotice() {
  return (
    <Banner kind="warn">
      Not deployed yet — these ports are <span className="text-ink">reserved</span> but nothing is
      listening on them until you deploy.
    </Banner>
  )
}

/**
 * The tun2socks sidecar snippet. Shares the target container's network namespace, so it creates the
 * tun device and the policy routes in place — the app image needs no changes and no capabilities of
 * its own. tun2socks marks its own upstream packets (fwmark) so its connection to the proxy bypasses
 * the tunnel instead of looping through it.
 */
export function tun2socksSnippet(opts: {
  name: string
  proxyUrl: string
  dns?: string
  mtu?: number
}): string {
  const { name, proxyUrl, dns = '9.9.9.9', mtu = 1500 } = opts
  return `services:
  ${name}:
    image: your/image
    # Docker's embedded resolver answers via the host — outside the tunnel.
    # Pin a public resolver so lookups go through the proxy too.
    dns: [${dns}]

  ${name}-vpn:
    image: ghcr.io/xjasonlyu/tun2socks
    # Shares ${name}'s network namespace: creates tun0 there and points the
    # default route at it. If this stops, ${name}'s traffic is black-holed
    # rather than falling back to the host's connection.
    network_mode: "container:${name}"
    cap_add: [NET_ADMIN]
    restart: unless-stopped
    depends_on: [${name}]
    environment:
      LOGLEVEL: info
      PROXY: ${proxyUrl}
      # Lower than the image default (9000): VPN encapsulation eats headroom,
      # and too-large values show up as "connects fine, big transfers hang".
      MTU: ${mtu}`
}
