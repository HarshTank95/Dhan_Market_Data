import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api } from '../lib/api'
import { parseUtc } from '../lib/datetime'

function expiryLabel(iso: string | null): string {
  if (!iso) return 'unknown'
  const exp = parseUtc(iso)
  const hours = (exp.getTime() - Date.now()) / 3_600_000
  const rel = hours <= 0 ? 'EXPIRED' : hours < 1
    ? `in ${Math.round(hours * 60)} min`
    : `in ${hours.toFixed(1)} h`
  return `${exp.toLocaleString()} (${rel})`
}

export function CredentialsPage() {
  const qc = useQueryClient()
  const status = useQuery({ queryKey: ['credentials'], queryFn: api.getCredentials })

  const [clientId, setClientId] = useState('')
  const [pin, setPin] = useState('')
  const [totp, setTotp] = useState('')
  const [accessToken, setAccessToken] = useState('')

  // Prefill Client ID from the stored value once it loads.
  useEffect(() => {
    if (status.data?.clientId && !clientId) setClientId(status.data.clientId)
  }, [status.data?.clientId]) // eslint-disable-line react-hooks/exhaustive-deps

  const generate = useMutation({
    mutationFn: () => api.generateToken(
      true,
      totp.trim() || undefined,
      pin.trim() || undefined,
      clientId.trim() || undefined,
    ),
    onSuccess: () => {
      setTotp(''); setPin('')
      qc.invalidateQueries({ queryKey: ['credentials'] })
    },
  })

  const savePaste = useMutation({
    mutationFn: () => api.setCredentials(clientId.trim(), accessToken.trim()),
    onSuccess: () => {
      setAccessToken('')
      qc.invalidateQueries({ queryKey: ['credentials'] })
    },
  })

  const s = status.data

  return (
    <div className="max-w-5xl">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-zinc-400">Dhan API credentials</h2>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 lg:items-start">
        {/* ── Left column: status + paste ───────────────────────── */}
        <div className="space-y-4">
          {s && (
            <div className="rounded-md border border-zinc-800 bg-zinc-900 p-4 text-sm">
              <div className="text-zinc-400">Current status</div>
              <div className="mt-2 space-y-1">
                <div>Client ID: <span className="font-mono">{s.clientId || <em className="text-zinc-500">not set</em>}</span></div>
                <div>Token: {s.hasToken
                  ? <span className="text-emerald-400">stored (encrypted)</span>
                  : <span className="text-zinc-500">not set</span>}
                </div>
                {s.hasToken && (
                  <div className="text-xs text-zinc-500">Expires: {expiryLabel(s.tokenExpiresAt)}</div>
                )}
                {s.updatedAt && (
                  <div className="text-xs text-zinc-500">Last updated: {parseUtc(s.updatedAt).toLocaleString()}</div>
                )}
              </div>
            </div>
          )}

          {/* Paste a token (generated on the Dhan platform) */}
          <div className="space-y-4 rounded-md border border-zinc-800 bg-zinc-900 p-4">
            <div className="text-sm font-medium text-zinc-200">Paste a token <span className="text-xs font-normal text-zinc-500">(from the Dhan platform)</span></div>
            <p className="text-xs text-zinc-500">
              Prefer to generate the token yourself on Dhan web? Paste it here to use it directly — an
              alternative to Generate. Uses the Client ID entered on the right.
            </p>
            <div>
              <label className="block text-xs font-medium text-zinc-400">Access token (JWT)</label>
              <textarea
                value={accessToken}
                onChange={e => setAccessToken(e.target.value)}
                rows={5}
                className="mt-1 w-full rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono"
                placeholder="eyJ..."
              />
              <p className="mt-1 text-xs text-zinc-500">
                Encrypted at rest using Windows DPAPI (CurrentUser scope). Decryptable only by this Windows user.
              </p>
            </div>
            <button
              onClick={() => savePaste.mutate()}
              disabled={!clientId.trim() || !accessToken.trim() || savePaste.isPending}
              className="rounded-md bg-zinc-700 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-600 disabled:opacity-50"
            >
              {savePaste.isPending ? 'Saving…' : 'Save token'}
            </button>
            {savePaste.error && <p className="text-sm text-red-400">{String(savePaste.error)}</p>}
          </div>
        </div>

        {/* ── Right column: generate ────────────────────────────── */}
        <div className="space-y-4 rounded-md border border-zinc-800 bg-zinc-900 p-4">
          <div className="text-sm font-medium text-zinc-200">Generate access token</div>
          <p className="text-xs text-zinc-500">
            Enter your Client ID, Dhan Pin and the current 6-digit code from your authenticator app.
            These go to Dhan's <span className="font-mono">generateAccessToken</span>; the new token
            (valid ~24h) is encrypted and saved as active automatically.
          </p>

          <div>
            <label className="block text-xs font-medium text-zinc-400">Client ID</label>
            <input
              value={clientId}
              onChange={e => setClientId(e.target.value)}
              className="mt-1 w-full rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono"
              placeholder="1107753191"
            />
          </div>

          <div className="flex gap-3">
            <div>
              <label className="block text-xs font-medium text-zinc-400">Dhan Pin</label>
              <input
                type="password"
                value={pin}
                onChange={e => setPin(e.target.value)}
                className="mt-1 w-32 rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono"
                placeholder="••••"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-zinc-400">TOTP code <span className="font-normal text-zinc-500">(6 digits)</span></label>
              <input
                value={totp}
                onChange={e => setTotp(e.target.value.replace(/\D/g, '').slice(0, 6))}
                inputMode="numeric"
                className="mt-1 w-40 rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono tracking-widest"
                placeholder="123456"
              />
            </div>
          </div>

          <button
            onClick={() => generate.mutate()}
            disabled={!clientId.trim() || !pin.trim() || totp.trim().length < 6 || generate.isPending}
            className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {generate.isPending ? 'Generating…' : 'Generate token'}
          </button>

          <p className="text-xs text-zinc-500">
            Pin and Client ID are stored encrypted (Windows DPAPI). The 6-digit code rotates every 30s —
            submit promptly, and if it's rejected just try the next code.
          </p>

          {generate.data && (
            <p className="text-sm text-emerald-400">
              Done — token saved
              {generate.data.tokenExpiresAt ? `, expires ${expiryLabel(generate.data.tokenExpiresAt)}` : ''}.
            </p>
          )}
          {generate.error && <p className="text-sm text-red-400 break-words">{String(generate.error)}</p>}
        </div>
      </div>
    </div>
  )
}
