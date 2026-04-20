import { motion } from 'motion/react'
import { useQuery } from '@tanstack/react-query'
import {
  Activity,
  TrendingUp,
  TrendingDown,
  AlertCircle,
  CheckCircle2,
  Clock,
  DollarSign,
} from 'lucide-react'
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts'
import { useSystemMetrics } from '../hooks/useSystemStatus'

// Generate performance data
const generatePerformanceData = () => {
  return Array.from({ length: 24 }, (_, i) => ({
    time: i,
    value: 50 + Math.sin(i / 3) * 15 + Math.random() * 10,
  }))
}

interface DashboardStats {
  activePositions: number
  openOrders: number
  totalAlerts: number
  criticalAlerts: number
  accountValue: number
  dailyPnL: number
  pnlPercent: number
}

export function HomePage() {
  const { data: metrics } = useSystemMetrics()
  const performanceData = generatePerformanceData()

  const { data: stats } = useQuery<DashboardStats>({
    queryKey: ['dashboard', 'stats'],
    queryFn: async () => {
      return {
        activePositions: 3,
        openOrders: 2,
        totalAlerts: 12,
        criticalAlerts: 2,
        accountValue: 125430.5,
        dailyPnL: 2340.8,
        pnlPercent: 1.9,
      }
    },
    refetchInterval: 5000,
  })

  const isPnlPositive = (stats?.dailyPnL ?? 0) >= 0

  return (
    <div className="min-h-screen p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.6, ease: [0.22, 1, 0.36, 1] }}
        className="mb-8"
      >
        <div className="flex items-center gap-3 mb-3">
          <div className="w-2.5 h-2.5 rounded-full bg-green-500 pulse-dot" />
          <span className="text-[10px] font-mono text-gray-500 font-semibold uppercase tracking-widest">Paper Trading Mode</span>
        </div>
        <h1 className="text-4xl font-bold text-white mb-2 tracking-tight">
          Trading System <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-400 to-cyan-400">Dashboard</span>
        </h1>
        <p className="text-sm text-gray-400 font-medium">Real-time monitoring and control center</p>
      </motion.div>

      {/* Main Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {/* Account Value */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.02, transition: { duration: 0.2 } }}
          className="card-clean p-6 group"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs text-gray-400 font-semibold uppercase tracking-wider">Account Value</span>
            <div className="p-2 rounded-lg bg-blue-500/10 border border-blue-500/20">
              <DollarSign className="w-4 h-4 text-blue-400" />
            </div>
          </div>
          <div className="text-3xl font-mono font-bold text-white mb-3 metric-value">
            ${stats?.accountValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <div className={`flex items-center gap-1.5 text-sm font-mono font-semibold ${isPnlPositive ? 'text-green-400 positive-glow' : 'text-red-400 negative-glow'}`}>
            {isPnlPositive ? <TrendingUp className="w-4 h-4" /> : <TrendingDown className="w-4 h-4" />}
            <span>{isPnlPositive ? '+' : ''}${Math.abs(stats?.dailyPnL ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2 })} ({isPnlPositive ? '+' : ''}{stats?.pnlPercent}%)</span>
          </div>
        </motion.div>

        {/* Active Positions */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.02, transition: { duration: 0.2 } }}
          className="card-clean p-6 group"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs text-gray-400 font-semibold uppercase tracking-wider">Active Positions</span>
            <div className="p-2 rounded-lg bg-cyan-500/10 border border-cyan-500/20">
              <Activity className="w-4 h-4 text-cyan-400" />
            </div>
          </div>
          <div className="text-3xl font-mono font-bold text-white mb-3 metric-value">
            {stats?.activePositions ?? 0}
          </div>
          <div className="text-sm text-gray-400 font-medium">
            {stats?.openOrders ?? 0} pending orders
          </div>
        </motion.div>

        {/* System Status */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.02, transition: { duration: 0.2 } }}
          className="card-clean p-6 group"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs text-gray-400 font-semibold uppercase tracking-wider">System Status</span>
            <div className="p-2 rounded-lg bg-green-500/10 border border-green-500/20">
              <CheckCircle2 className="w-4 h-4 text-green-400" />
            </div>
          </div>
          <div className="mb-3">
            <span className="badge badge-green">OPERATIONAL</span>
          </div>
          <div className="text-sm text-gray-400 font-medium">
            All systems online
          </div>
        </motion.div>

        {/* Alerts */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.4, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.02, transition: { duration: 0.2 } }}
          className="card-clean p-6 group"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-xs text-gray-400 font-semibold uppercase tracking-wider">Alerts</span>
            <div className="p-2 rounded-lg bg-red-500/10 border border-red-500/20">
              <AlertCircle className="w-4 h-4 text-red-400" />
            </div>
          </div>
          <div className="text-3xl font-mono font-bold text-white mb-3 metric-value">
            {stats?.totalAlerts ?? 0}
          </div>
          <div className="flex items-center gap-2">
            <span className="badge badge-red">{stats?.criticalAlerts ?? 0} Critical</span>
          </div>
        </motion.div>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-8">
        {/* Performance Chart */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.5, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.005, transition: { duration: 0.2 } }}
          className="lg:col-span-2 card-clean p-6 group"
        >
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-base font-bold text-white mb-1 tracking-tight">Account Performance</h3>
              <p className="text-xs text-gray-400 font-medium">Last 24 hours</p>
            </div>
            <motion.div
              animate={{ opacity: [1, 0.5, 1] }}
              transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
            >
              <span className="badge badge-blue">Live</span>
            </motion.div>
          </div>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={performanceData}>
                <defs>
                  <linearGradient id="perfGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#3b82f6" stopOpacity={0.4} />
                    <stop offset="50%" stopColor="#06b6d4" stopOpacity={0.2} />
                    <stop offset="100%" stopColor="#8b5cf6" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis
                  dataKey="time"
                  stroke="#5a6575"
                  fontSize={11}
                  fontFamily="JetBrains Mono"
                  tickLine={false}
                  axisLine={{ stroke: '#1a2332' }}
                />
                <YAxis
                  stroke="#5a6575"
                  fontSize={11}
                  fontFamily="JetBrains Mono"
                  tickLine={false}
                  axisLine={{ stroke: '#1a2332' }}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'rgba(15, 20, 27, 0.95)',
                    border: '1px solid #1a2332',
                    borderRadius: '8px',
                    fontSize: '12px',
                    backdropFilter: 'blur(10px)',
                    fontFamily: 'JetBrains Mono',
                  }}
                  labelStyle={{ color: '#8b95a8' }}
                  itemStyle={{ color: '#e8eef7' }}
                />
                <Area
                  type="monotone"
                  dataKey="value"
                  stroke="url(#perfGrad)"
                  fill="url(#perfGrad)"
                  strokeWidth={3}
                  dot={false}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </motion.div>

        {/* System Metrics */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.6, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
          whileHover={{ scale: 1.02, transition: { duration: 0.2 } }}
          className="card-clean p-6 group"
        >
          <h3 className="text-base font-bold text-white mb-6 tracking-tight">System Metrics</h3>
          <div className="space-y-6">
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400 font-medium">CPU Usage</span>
                <span className="text-sm font-mono font-bold text-white">{metrics?.cpu.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-900/50 rounded-full h-2.5 overflow-hidden border border-gray-800">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: `${metrics?.cpu ?? 0}%` }}
                  transition={{ duration: 1, ease: [0.22, 1, 0.36, 1] }}
                  className="bg-gradient-to-r from-blue-500 to-cyan-500 h-full rounded-full relative"
                  style={{ boxShadow: '0 0 12px rgba(59, 130, 246, 0.5)' }}
                >
                  <div className="absolute inset-0 bg-gradient-to-r from-white/20 to-transparent" />
                </motion.div>
              </div>
            </div>
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400 font-medium">Memory</span>
                <span className="text-sm font-mono font-bold text-white">{metrics?.ram.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-900/50 rounded-full h-2.5 overflow-hidden border border-gray-800">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: `${metrics?.ram ?? 0}%` }}
                  transition={{ duration: 1, delay: 0.1, ease: [0.22, 1, 0.36, 1] }}
                  className="bg-gradient-to-r from-green-500 to-emerald-500 h-full rounded-full relative"
                  style={{ boxShadow: '0 0 12px rgba(16, 185, 129, 0.5)' }}
                >
                  <div className="absolute inset-0 bg-gradient-to-r from-white/20 to-transparent" />
                </motion.div>
              </div>
            </div>
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400 font-medium">Network</span>
                <span className="text-sm font-mono font-bold text-white">{metrics?.network.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-900/50 rounded-full h-2.5 overflow-hidden border border-gray-800">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: `${metrics?.network ?? 0}%` }}
                  transition={{ duration: 1, delay: 0.2, ease: [0.22, 1, 0.36, 1] }}
                  className="bg-gradient-to-r from-yellow-500 to-orange-500 h-full rounded-full relative"
                  style={{ boxShadow: '0 0 12px rgba(245, 158, 11, 0.5)' }}
                >
                  <div className="absolute inset-0 bg-gradient-to-r from-white/20 to-transparent" />
                </motion.div>
              </div>
            </div>
          </div>
        </motion.div>
      </div>

      {/* Recent Activity */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.7, duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
        whileHover={{ scale: 1.005, transition: { duration: 0.2 } }}
        className="card-clean p-6 group"
      >
        <h3 className="text-base font-bold text-white mb-6 tracking-tight">Recent Activity</h3>
        <div className="space-y-4">
          {[
            { time: '2 min ago', event: 'Position opened: SPY Call $450', type: 'trade', status: 'success' },
            { time: '15 min ago', event: 'Alert: High IV detected on QQQ', type: 'alert', status: 'warning' },
            { time: '1 hour ago', event: 'Greeks rebalance completed', type: 'system', status: 'success' },
            { time: '2 hours ago', event: 'Campaign started: Theta Harvest', type: 'campaign', status: 'info' },
          ].map((activity, i) => (
            <motion.div
              key={i}
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.8 + i * 0.1, duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
              whileHover={{ x: 4, transition: { duration: 0.2 } }}
              className="flex items-start gap-4 pb-4 border-b border-gray-800/50 last:border-0 cursor-pointer group/item"
            >
              <div className="p-1.5 rounded-md bg-gray-800/50 group-hover/item:bg-gray-700/50 transition-colors">
                <Clock className="w-3.5 h-3.5 text-gray-500" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm text-white font-medium mb-1 group-hover/item:text-blue-400 transition-colors">{activity.event}</p>
                <p className="text-xs text-gray-500 font-mono">{activity.time}</p>
              </div>
              <span className={`badge ${
                activity.status === 'success' ? 'badge-green' :
                activity.status === 'warning' ? 'badge-yellow' :
                activity.status === 'info' ? 'badge-blue' : 'badge-red'
              }`}>
                {activity.type}
              </span>
            </motion.div>
          ))}
        </div>
      </motion.div>
    </div>
  )
}
