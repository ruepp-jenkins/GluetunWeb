import { useState } from 'react'
import { ApiError } from '../api/client'
import type { ProxyTestResult } from '../api/types'
import { Modal } from './Modal'
import { Field, Input } from './Field'
import { Banner, Button, Spinner } from './ui'

const DEFAULT_URL = 'https://ipwho.is/'

/**
 * Fetches a URL through the target's SOCKS5 proxy and reports whether it worked. The raw response is
 * kept behind a disclosure: the verdict answers "is it working?", the raw view answers "why not?".
 */
export function TestModal({
  title,
  hint,
  onRun,
  onClose,
}: {
  title: string
  hint?: string
  onRun: (url: string) => Promise<ProxyTestResult>
  onClose: () => void
}) {
  const [url, setUrl] = useState(DEFAULT_URL)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<ProxyTestResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function run() {
    setBusy(true)
    setError(null)
    setResult(null)
    try {
      setResult(await onRun(url))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Test failed to run.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      title={title}
      onClose={onClose}
      wide
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            close
          </Button>
          <Button variant="primary" onClick={() => void run()} disabled={busy || !url.trim()}>
            {busy ? 'testing…' : 'run test'}
          </Button>
        </>
      }
    >
      <div className="grid gap-3">
        {hint && <Banner kind="info">{hint}</Banner>}

        <Field label="Test URL" doc="testUrl">
          <Input
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder={DEFAULT_URL}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                if (!busy && url.trim()) void run()
              }
            }}
          />
        </Field>

        {busy && <Spinner label="fetching through the proxy…" />}
        {error && <Banner kind="error">{error}</Banner>}
        {result && <TestResult result={result} />}
      </div>
    </Modal>
  )
}

function TestResult({ result }: { result: ProxyTestResult }) {
  const [showRaw, setShowRaw] = useState(false)

  return (
    <div className="grid gap-2">
      <Banner kind={result.ok ? 'ok' : 'error'}>
        {result.ok ? (
          <>
            <span className="text-ink">Works.</span> {result.url} returned {result.statusCode}{' '}
            {result.reasonPhrase} in {result.elapsedMs} ms.
          </>
        ) : (
          <>
            <span className="text-ink">Failed.</span> {result.error}
          </>
        )}
      </Banner>

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 border border-line bg-panel-2 px-3 py-2 text-[12px]">
        <Row label="via" value={result.via || '—'} />
        <Row label="url" value={result.url} />
        <Row
          label="status"
          value={result.statusCode ? `${result.statusCode} ${result.reasonPhrase ?? ''}`.trim() : 'no response'}
        />
        <Row label="elapsed" value={`${result.elapsedMs} ms`} />
      </dl>

      {(result.headers || result.body) && (
        <div className="border border-line bg-panel-2">
          <button
            type="button"
            onClick={() => setShowRaw((v) => !v)}
            className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-[11px] uppercase tracking-widest text-muted hover:text-phosphor"
          >
            <span className="text-phosphor">{showRaw ? '[-]' : '[+]'}</span> raw response
          </button>
          {showRaw && (
            <div className="border-t border-line">
              {result.headers && (
                <pre className="max-h-56 overflow-auto whitespace-pre-wrap break-words px-3 py-2 text-[11px] leading-relaxed text-cyan">
                  {result.headers}
                </pre>
              )}
              {result.body && (
                <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words border-t border-line px-3 py-2 text-[11px] leading-relaxed text-ink">
                  {result.body}
                </pre>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted">{label}</dt>
      <dd className="break-all text-ink">{value}</dd>
    </>
  )
}
