import { useState } from 'react'
import { Card } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { Select } from '../components/ui/Select'
import { Toggle } from '../components/ui/Toggle'
import { Button } from '../components/ui/Button'
import { useSettingsStore } from '../stores/settingsStore'
import { useUiStore } from '../stores/uiStore'
import { useToastStore } from '../stores/toastStore'
import { applyTheme, type ThemeMode } from '../utils/theme'

export function SettingsPage() {
  const settings = useSettingsStore()
  const { theme, setTheme } = useUiStore()
  const addToast = useToastStore((state) => state.addToast)
  const [validationErrors, setValidationErrors] = useState<string[]>([])

  const handleSave = () => {
    const validation = settings.validateSettings()
    if (!validation.valid) {
      setValidationErrors(validation.errors)
      addToast('error', `Validation failed: ${validation.errors[0]}`)
      return
    }

    setValidationErrors([])
    addToast('success', 'Settings saved successfully')
  }

  const handleReset = () => {
    if (confirm('Are you sure you want to reset all settings to defaults?')) {
      settings.resetSettings()
      addToast('info', 'Settings reset to defaults')
      setValidationErrors([])
    }
  }

  const handleThemeChange = (newTheme: string) => {
    const themeMode = newTheme as ThemeMode
    setTheme(themeMode)
    applyTheme(themeMode)
    addToast('success', `Theme changed to ${themeMode}`)
  }

  return (
    <div className="p-8 flex flex-col gap-5">
      <div>
        <h1 className="text-3xl font-bold">Settings</h1>
        <p className="text-muted mt-1">Configure your dashboard preferences and API settings</p>
      </div>

      {/* Theme Settings */}
      <Card>
        <div className="p-6 border-b border-border">
          <h2 className="text-lg font-semibold">Appearance</h2>
          <p className="text-sm text-muted mt-1">Customize the look and feel of your dashboard</p>
        </div>
        <div className="p-6 space-y-4">
          <Select
            id="theme"
            label="Theme"
            description="Choose your preferred color scheme"
            value={theme}
            onChange={handleThemeChange}
            options={[
              { value: 'light', label: 'Light' },
              { value: 'dark', label: 'Dark' },
              { value: 'system', label: 'System (Auto)' },
            ]}
          />
        </div>
      </Card>

      {/* API Configuration */}
      <Card>
        <div className="p-6 border-b border-border">
          <h2 className="text-lg font-semibold">API Configuration</h2>
          <p className="text-sm text-muted mt-1">Configure connection to the Cloudflare Worker API</p>
        </div>
        <div className="p-6 space-y-4">
          <Input
            id="apiEndpoint"
            label="API Endpoint"
            description="Base URL of the Cloudflare Worker (e.g., http://localhost:8787)"
            type="url"
            placeholder="http://localhost:8787"
            value={settings.apiEndpoint}
            onChange={(e) => settings.updateSettings({ apiEndpoint: e.target.value })}
            {...(validationErrors.find((err) => err.includes('endpoint')) && {
              error: validationErrors.find((err) => err.includes('endpoint')),
            })}
          />
          <Input
            id="apiKey"
            label="API Key"
            description="Optional API key for authenticated requests"
            type="password"
            placeholder="Enter API key"
            value={settings.apiKey}
            onChange={(e) => settings.updateSettings({ apiKey: e.target.value })}
          />
        </div>
      </Card>

      {/* Auto-Refresh Settings */}
      <Card>
        <div className="p-6 border-b border-border">
          <h2 className="text-lg font-semibold">Auto-Refresh Intervals</h2>
          <p className="text-sm text-muted mt-1">
            Set how often each widget should refresh data (in seconds)
          </p>
        </div>
        <div className="p-6 space-y-4">
          <Input
            id="refreshPositions"
            label="Positions Refresh"
            description="How often to refresh positions data"
            type="number"
            min="5"
            step="5"
            value={settings.refreshIntervalPositions / 1000}
            onChange={(e) =>
              settings.updateSettings({
                refreshIntervalPositions: Number(e.target.value) * 1000,
              })
            }
            {...(validationErrors.find((err) => err.includes('Positions refresh')) && {
              error: validationErrors.find((err) => err.includes('Positions refresh')),
            })}
          />
          <Input
            id="refreshHealth"
            label="Health Refresh"
            description="How often to refresh system health data"
            type="number"
            min="5"
            step="5"
            value={settings.refreshIntervalHealth / 1000}
            onChange={(e) =>
              settings.updateSettings({ refreshIntervalHealth: Number(e.target.value) * 1000 })
            }
            {...(validationErrors.find((err) => err.includes('Health refresh')) && {
              error: validationErrors.find((err) => err.includes('Health refresh')),
            })}
          />
          <Input
            id="refreshAlerts"
            label="Alerts Refresh"
            description="How often to refresh alerts"
            type="number"
            min="5"
            step="5"
            value={settings.refreshIntervalAlerts / 1000}
            onChange={(e) =>
              settings.updateSettings({ refreshIntervalAlerts: Number(e.target.value) * 1000 })
            }
            {...(validationErrors.find((err) => err.includes('Alerts refresh')) && {
              error: validationErrors.find((err) => err.includes('Alerts refresh')),
            })}
          />
          <Input
            id="refreshIvts"
            label="IVTS Refresh"
            description="How often to refresh IVTS data"
            type="number"
            min="5"
            step="5"
            value={settings.refreshIntervalIvts / 1000}
            onChange={(e) =>
              settings.updateSettings({ refreshIntervalIvts: Number(e.target.value) * 1000 })
            }
            {...(validationErrors.find((err) => err.includes('IVTS refresh')) && {
              error: validationErrors.find((err) => err.includes('IVTS refresh')),
            })}
          />
        </div>
      </Card>

      {/* Alert Preferences */}
      <Card>
        <div className="p-6 border-b border-border">
          <h2 className="text-lg font-semibold">Alert Preferences</h2>
          <p className="text-sm text-muted mt-1">Configure how alerts are displayed and notified</p>
        </div>
        <div className="p-6 space-y-4">
          <Toggle
            checked={settings.enableDesktopNotifications}
            onChange={(checked) => settings.updateSettings({ enableDesktopNotifications: checked })}
            label="Desktop Notifications"
            description="Show browser notifications for critical alerts"
          />
          <Toggle
            checked={settings.enableSoundAlerts}
            onChange={(checked) => settings.updateSettings({ enableSoundAlerts: checked })}
            label="Sound Alerts"
            description="Play sound when new alerts arrive"
          />
          <Select
            id="alertThreshold"
            label="Alert Threshold"
            description="Minimum severity level to display"
            value={settings.alertThreshold}
            onChange={(value) =>
              settings.updateSettings({
                alertThreshold: value as 'all' | 'warning' | 'critical',
              })
            }
            options={[
              { value: 'all', label: 'All Alerts' },
              { value: 'warning', label: 'Warning and Above' },
              { value: 'critical', label: 'Critical Only' },
            ]}
          />
        </div>
      </Card>

      {/* Data Export Options */}
      <Card>
        <div className="p-6 border-b border-border">
          <h2 className="text-lg font-semibold">Data Export</h2>
          <p className="text-sm text-muted mt-1">Configure default export settings</p>
        </div>
        <div className="p-6 space-y-4">
          <Select
            id="exportFormat"
            label="Export Format"
            description="Default format for data exports"
            value={settings.exportFormat}
            onChange={(value) =>
              settings.updateSettings({ exportFormat: value as 'json' | 'csv' })
            }
            options={[
              { value: 'json', label: 'JSON' },
              { value: 'csv', label: 'CSV' },
            ]}
          />
          <Toggle
            checked={settings.includeTimestamps}
            onChange={(checked) => settings.updateSettings({ includeTimestamps: checked })}
            label="Include Timestamps"
            description="Add timestamp column to exported data"
          />
        </div>
      </Card>

      {/* Actions */}
      <div className="flex items-center justify-end gap-3">
        <Button variant="ghost" onClick={handleReset}>
          Reset to Defaults
        </Button>
        <Button variant="primary" onClick={handleSave}>
          Save Settings
        </Button>
      </div>

      {/* Validation Errors Summary — kit tinted danger surface */}
      {validationErrors.length > 0 && (
        <div className="p-4 bg-[var(--tint-red)] border border-[color:var(--red)]/25 rounded-md">
          <h3 className="text-sm font-semibold text-down mb-2">Validation Errors</h3>
          <ul className="list-disc list-inside flex flex-col gap-1">
            {validationErrors.map((error, index) => (
              <li key={index} className="text-xs text-down">
                {error}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
