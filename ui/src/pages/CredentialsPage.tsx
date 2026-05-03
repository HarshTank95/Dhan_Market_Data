import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api } from '../lib/api'

export function CredentialsPage() {
  const qc = useQueryClient()
  const status = useQuery({ queryKey: ['credentials'], queryFn: api.getCredentials })
  const [clientId, setClientId] = useState('')
  const [accessToken, setAccessToken] = useState('')

  const save = useMutation({
    mutationFn: () => api.setCredentials(clientId, accessToken),
    onSuccess: () => {
      setAccessToken('')
      qc.invalidateQueries({ queryKey: ['credentials'] })
    },
  })

  return (
    <div className="max-w-xl">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-zinc-400">Dhan API credentials</h2>

      {status.data && (
        <div className="mb-4 rounded-md border border-zinc-800 bg-zinc-900 p-4 text-sm">
          <div className="text-zinc-400">Current status</div>
          <div className="mt-2 space-y-1">
            <div>Client ID: <span className="font-mono">{status.data.clientId || <em className="text-zinc-500">not set</em>}</span></div>
            <div>Token: {status.data.hasToken
              ? <span className="text-emerald-400">stored (encrypted)</span>
              : <span className="text-zinc-500">not set</span>}
            </div>
            {status.data.updatedAt && (
              <div className="text-xs text-zinc-500">Last updated: {new Date(status.data.updatedAt).toLocaleString()}</div>
            )}
          </div>
        </div>
      )}

      <div className="space-y-4 rounded-md border border-zinc-800 bg-zinc-900 p-4">
        <div>
          <label className="block text-xs font-medium text-zinc-400">Client ID</label>
          <input
            value={clientId}
            onChange={e => setClientId(e.target.value)}
            className="mt-1 w-full rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono"
            placeholder="1107753191"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-zinc-400">Access token (JWT)</label>
          <textarea
            value={accessToken}
            onChange={e => setAccessToken(e.target.value)}
            rows={4}
            className="mt-1 w-full rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm font-mono"
            placeholder="eyJ..."
          />
          <p className="mt-1 text-xs text-zinc-500">
            Encrypted at rest using Windows DPAPI (CurrentUser scope). Decryptable only by this Windows user.
          </p>
        </div>
        <button
          onClick={() => save.mutate()}
          disabled={!clientId || !accessToken || save.isPending}
          className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
        >
          {save.isPending ? 'Saving…' : 'Save credentials'}
        </button>
        {save.error && <p className="text-sm text-red-400">{String(save.error)}</p>}
      </div>
    </div>
  )
}
