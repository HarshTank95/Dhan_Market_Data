import { useState } from 'react'

import { CredentialsPage } from './pages/CredentialsPage'
import { ResultsPage } from './pages/ResultsPage'
import { RunPage } from './pages/RunPage'
import { StrategiesPage } from './pages/StrategiesPage'

type Tab = 'strategies' | 'run' | 'results' | 'credentials'

const TABS: { key: Tab; label: string }[] = [
  { key: 'strategies', label: 'Strategies' },
  { key: 'run', label: 'Run' },
  { key: 'results', label: 'Results' },
  { key: 'credentials', label: 'Credentials' },
]

export default function App() {
  const [tab, setTab] = useState<Tab>('strategies')

  return (
    <div className="flex h-full flex-col">
      <header className="border-b border-zinc-800 bg-zinc-900/60 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <h1 className="text-lg font-semibold tracking-tight">
            <span className="text-emerald-400">Dhan</span> Backtest Console
          </h1>
          <nav className="flex gap-1">
            {TABS.map(t => (
              <button
                key={t.key}
                onClick={() => setTab(t.key)}
                className={
                  'rounded-md px-3 py-1.5 text-sm transition ' +
                  (tab === t.key
                    ? 'bg-emerald-500/15 text-emerald-300'
                    : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-100')
                }
              >
                {t.label}
              </button>
            ))}
          </nav>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-6">
        {tab === 'strategies' && <StrategiesPage />}
        {tab === 'run' && <RunPage />}
        {tab === 'results' && <ResultsPage />}
        {tab === 'credentials' && <CredentialsPage />}
      </main>
    </div>
  )
}
