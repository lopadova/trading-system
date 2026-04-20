// HomePage — kit Overview layout. Widgets enter with a staggered motion and
// are wired to the global AssetFilter store, so changing the asset chip
// re-queries every performance-sensitive widget below.

import { motion } from 'motion/react'
import { DollarSign, TrendingUp, FolderKanban, RefreshCw } from 'lucide-react'
import { AssetFilter } from '../components/dashboard/AssetFilter'
import { StatCard } from '../components/ui/StatCard'
import { SummaryCard } from '../components/dashboard/SummaryCard'
import { MultiSeriesChart } from '../components/dashboard/MultiSeriesChart'
import { DrawdownsSection } from '../components/dashboard/DrawdownsSection'
import { MonthlyPerfSection } from '../components/dashboard/MonthlyPerfSection'
import { RiskMetricsCard } from '../components/dashboard/RiskMetricsCard'
import { AlertsMiniCard } from '../components/dashboard/AlertsMiniCard'
import { SystemPerfMini } from '../components/dashboard/SystemPerfMini'
import { RecentActivity } from '../components/dashboard/RecentActivity'
import { ActivePositionsTable } from '../components/dashboard/ActivePositionsTable'
import { Card } from '../components/ui/Card'
import { useAssetFilterStore } from '../stores/assetFilterStore'
import { usePerformanceSummary } from '../hooks/usePerformanceSummary'
import { useCampaignsSummary } from '../hooks/useCampaignsSummary'
import { formatCurrency, formatDelta } from '../utils/format'

// Helper: staggered fade-in-up for each top-level row (index-driven delay)
function stagger(i: number) {
  return {
    initial: { opacity: 0, y: 8 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.28, delay: i * 0.04, ease: [0.4, 0, 0.2, 1] as const },
  }
}

export function HomePage() {
  const asset = useAssetFilterStore(s => s.asset)
  const { data: summary } = usePerformanceSummary(asset)
  const { data: camps } = useCampaignsSummary()

  // Headline account card reads from the same summary query the SummaryCard uses
  const accountValue = summary ? formatCurrency(summary.base, 'USD') : '—'
  const accountDelta = summary
    ? formatDelta((summary.base * summary.m) / 100, summary.m, 'USD')
    : ''

  return (
    <div className="p-8 flex flex-col gap-5">
      {/* Row 0 — Asset filter + auto-refresh hint */}
      <motion.div
        {...stagger(0)}
        className="flex justify-between items-center gap-3 flex-wrap"
      >
        <AssetFilter />
        <div className="flex items-center gap-2 text-[11.5px] text-muted font-mono">
          <RefreshCw size={12} />
          auto-refresh · 30s
        </div>
      </motion.div>

      {/* Row 1 — 4 KPI cards */}
      <motion.div
        {...stagger(1)}
        className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4"
      >
        <StatCard
          label="Account Value"
          value={accountValue}
          delta={accountDelta}
          deltaTone="green"
          icon={DollarSign}
        />
        <StatCard
          label="Open P&L"
          value="+$1,248.00"
          delta="3 positions · today"
          deltaTone="green"
          icon={TrendingUp}
        />
        <StatCard
          label="Active Campaigns"
          value={String(camps?.active ?? '—')}
          {...(camps?.detail ? { delta: camps.detail } : {})}
          icon={FolderKanban}
        />
        <AlertsMiniCard />
      </motion.div>

      {/* Row 2 — main chart (2/3) + right column stack (1/3) */}
      <motion.div
        {...stagger(2)}
        className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4"
      >
        <Card className="min-h-[330px]">
          <MultiSeriesChart asset={asset} />
        </Card>
        <div className="flex flex-col gap-4">
          <RiskMetricsCard />
          <SystemPerfMini />
        </div>
      </motion.div>

      {/* Rows 3..7 — full-width sections */}
      <motion.div {...stagger(3)}>
        <SummaryCard asset={asset} />
      </motion.div>
      <motion.div {...stagger(4)}>
        <DrawdownsSection asset={asset} />
      </motion.div>
      <motion.div {...stagger(5)}>
        <MonthlyPerfSection asset={asset} />
      </motion.div>
      <motion.div {...stagger(6)}>
        <ActivePositionsTable />
      </motion.div>
      <motion.div {...stagger(7)}>
        <RecentActivity />
      </motion.div>
    </div>
  )
}
