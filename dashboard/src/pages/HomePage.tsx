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
      <div className="mb-8">
        <div className="flex items-center gap-2 mb-2">
          <div className="w-2 h-2 rounded-full bg-green-500 pulse-dot" />
          <span className="text-xs text-gray-500 font-medium uppercase tracking-wide">Paper Trading Mode</span>
        </div>
        <h1 className="text-3xl font-semibold text-white mb-1">Trading System Dashboard</h1>
        <p className="text-sm text-gray-400">Real-time monitoring and control center</p>
      </div>

      {/* Main Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {/* Account Value */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="card-clean p-6"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-sm text-gray-400">Account Value</span>
            <DollarSign className="w-4 h-4 text-gray-500" />
          </div>
          <div className="text-3xl font-semibold text-white mb-2">
            ${stats?.accountValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <div className={`flex items-center gap-1 text-sm font-medium ${isPnlPositive ? 'text-green-500' : 'text-red-500'}`}>
            {isPnlPositive ? <TrendingUp className="w-4 h-4" /> : <TrendingDown className="w-4 h-4" />}
            <span>{isPnlPositive ? '+' : ''}${Math.abs(stats?.dailyPnL ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2 })} ({isPnlPositive ? '+' : ''}{stats?.pnlPercent}%)</span>
          </div>
        </motion.div>

        {/* Active Positions */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="card-clean p-6"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-sm text-gray-400">Active Positions</span>
            <Activity className="w-4 h-4 text-gray-500" />
          </div>
          <div className="text-3xl font-semibold text-white mb-2">
            {stats?.activePositions ?? 0}
          </div>
          <div className="text-sm text-gray-400">
            {stats?.openOrders ?? 0} pending orders
          </div>
        </motion.div>

        {/* System Status */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
          className="card-clean p-6"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-sm text-gray-400">System Status</span>
            <CheckCircle2 className="w-4 h-4 text-green-500" />
          </div>
          <div className="mb-2">
            <span className="badge badge-green">OPERATIONAL</span>
          </div>
          <div className="text-sm text-gray-400">
            All systems online
          </div>
        </motion.div>

        {/* Alerts */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.4 }}
          className="card-clean p-6"
        >
          <div className="flex items-center justify-between mb-4">
            <span className="text-sm text-gray-400">Alerts</span>
            <AlertCircle className="w-4 h-4 text-gray-500" />
          </div>
          <div className="text-3xl font-semibold text-white mb-2">
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
          transition={{ delay: 0.5 }}
          className="lg:col-span-2 card-clean p-6"
        >
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-base font-semibold text-white mb-1">Account Performance</h3>
              <p className="text-xs text-gray-500">Last 24 hours</p>
            </div>
            <span className="badge badge-blue">Live</span>
          </div>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={performanceData}>
                <defs>
                  <linearGradient id="perfGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#2f81f7" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#2f81f7" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis
                  dataKey="time"
                  stroke="#484f58"
                  fontSize={11}
                  tickLine={false}
                  axisLine={{ stroke: '#30363d' }}
                />
                <YAxis
                  stroke="#484f58"
                  fontSize={11}
                  tickLine={false}
                  axisLine={{ stroke: '#30363d' }}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: '#161b22',
                    border: '1px solid #30363d',
                    borderRadius: '6px',
                    fontSize: '12px',
                  }}
                  labelStyle={{ color: '#7d8590' }}
                  itemStyle={{ color: '#e6edf3' }}
                />
                <Area
                  type="monotone"
                  dataKey="value"
                  stroke="#2f81f7"
                  fill="url(#perfGrad)"
                  strokeWidth={2}
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
          transition={{ delay: 0.6 }}
          className="card-clean p-6"
        >
          <h3 className="text-base font-semibold text-white mb-6">System Metrics</h3>
          <div className="space-y-6">
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400">CPU Usage</span>
                <span className="text-sm font-semibold text-white">{metrics?.cpu.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-800 rounded-full h-2">
                <div
                  className="bg-blue-500 h-2 rounded-full transition-all"
                  style={{ width: `${metrics?.cpu ?? 0}%` }}
                />
              </div>
            </div>
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400">Memory</span>
                <span className="text-sm font-semibold text-white">{metrics?.ram.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-800 rounded-full h-2">
                <div
                  className="bg-green-500 h-2 rounded-full transition-all"
                  style={{ width: `${metrics?.ram ?? 0}%` }}
                />
              </div>
            </div>
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-gray-400">Network</span>
                <span className="text-sm font-semibold text-white">{metrics?.network.toFixed(1)}%</span>
              </div>
              <div className="w-full bg-gray-800 rounded-full h-2">
                <div
                  className="bg-yellow-500 h-2 rounded-full transition-all"
                  style={{ width: `${metrics?.network ?? 0}%` }}
                />
              </div>
            </div>
          </div>
        </motion.div>
      </div>

      {/* Recent Activity */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.7 }}
        className="card-clean p-6"
      >
        <h3 className="text-base font-semibold text-white mb-6">Recent Activity</h3>
        <div className="space-y-4">
          {[
            { time: '2 min ago', event: 'Position opened: SPY Call $450', type: 'trade', status: 'success' },
            { time: '15 min ago', event: 'Alert: High IV detected on QQQ', type: 'alert', status: 'warning' },
            { time: '1 hour ago', event: 'Greeks rebalance completed', type: 'system', status: 'success' },
            { time: '2 hours ago', event: 'Campaign started: Theta Harvest', type: 'campaign', status: 'info' },
          ].map((activity, i) => (
            <div
              key={i}
              className="flex items-start gap-4 pb-4 border-b border-gray-800 last:border-0"
            >
              <Clock className="w-4 h-4 text-gray-500 mt-1" />
              <div className="flex-1 min-w-0">
                <p className="text-sm text-white mb-1">{activity.event}</p>
                <p className="text-xs text-gray-500">{activity.time}</p>
              </div>
              <span className={`badge ${
                activity.status === 'success' ? 'badge-green' :
                activity.status === 'warning' ? 'badge-yellow' :
                activity.status === 'info' ? 'badge-blue' : 'badge-red'
              }`}>
                {activity.type}
              </span>
            </div>
          ))}
        </div>
      </motion.div>
    </div>
  )
}
