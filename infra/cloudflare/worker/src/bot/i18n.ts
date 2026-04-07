/**
 * Bot Internationalization (i18n)
 * Supports IT and EN languages
 */

export type Language = 'it' | 'en'

/**
 * All bot message strings in both languages
 */
export const messages: Record<Language, Record<string, string>> = {
  it: {
    // Menu and commands
    menu_title: '📊 Trading System — Menu Principale',
    menu_portfolio: '💼 Portfolio',
    menu_status: '⚙️ Stato Servizi',
    menu_campaigns: '🎯 Campagne Attive',
    menu_market: '📈 Market Data',
    menu_strategies: '🧠 Strategie',
    menu_alerts: '🔔 Alert',
    menu_risk: '⚠️ Risk Monitor',
    menu_snapshot: '📸 Snapshot Completo',
    menu_refresh: '🔄 Refresh',

    // Authorization
    unauthorized: '⛔ Non autorizzato. Contatta l\'amministratore.',

    // Portfolio
    portfolio_title: '💼 Portfolio',
    portfolio_no_positions: 'Nessuna posizione aperta',
    portfolio_position: 'Posizione: {symbol}\nTipo: {type}\nQuantità: {quantity}\nP&L: {pnl}',

    // Status
    status_title: '⚙️ Stato Servizi',
    status_ok: '✅ {service} — OK (ultimo heartbeat: {time})',
    status_stale: '⚠️ {service} — STALE (ultimo heartbeat: {time})',
    status_offline: '❌ {service} — OFFLINE',

    // Campaigns
    campaigns_title: '🎯 Campagne Attive',
    campaigns_no_active: 'Nessuna campagna attiva',
    campaigns_item: '🎯 {name}\nStato: {status}\nPosizioni: {positions}',

    // Market
    market_title: '📈 Market Data',
    market_no_data: 'Nessun dato disponibile',

    // Strategies
    strategies_title: '🧠 Strategie',
    strategies_no_active: 'Nessuna strategia attiva',
    strategies_item: '{name}\nStato: {status}\nUltimo segnale: {signal}',

    // Alerts
    alerts_title: '🔔 Alert',
    alerts_no_unresolved: 'Nessun alert irrisolto',
    alerts_item: '{severity} {message}\nData: {time}',

    // Risk
    risk_title: '⚠️ Risk Monitor',
    risk_exposure: 'Esposizione totale: {exposure}',
    risk_margin: 'Margine utilizzato: {margin}',
    risk_ok: '✅ Tutti i parametri di rischio sono OK',

    // Snapshot
    snapshot_title: '📸 Snapshot Completo',
    snapshot_generating: '⏳ Generazione snapshot in corso...',
    snapshot_ready: '✅ Snapshot pronto!',

    // Errors
    error_generic: '❌ Errore: {message}',
    error_timeout: '⏱️ Timeout durante l\'elaborazione',
    error_database: '💾 Errore database: {message}',

    // Command processing
    command_unknown: '❓ Comando non riconosciuto. Usa /menu per vedere i comandi disponibili.',
    command_processing: '⏳ Elaborazione in corso...',

    // Whitelist admin commands
    whitelist_add_success: '✅ Utente {userId} aggiunto alla whitelist',
    whitelist_add_already_exists: '⚠️ Utente {userId} già presente nella whitelist',
    whitelist_add_missing_userid: '❌ Uso: /whitelist add <user_id>',
    whitelist_remove_success: '✅ Utente {userId} rimosso dalla whitelist',
    whitelist_remove_not_found: '⚠️ Utente {userId} non trovato nella whitelist',
    whitelist_remove_missing_userid: '❌ Uso: /whitelist remove <user_id>',
    whitelist_list_title: '👥 *WHITELIST UTENTI*',
    whitelist_list_empty: 'Nessun utente nella whitelist'
  },

  en: {
    // Menu and commands
    menu_title: '📊 Trading System — Main Menu',
    menu_portfolio: '💼 Portfolio',
    menu_status: '⚙️ Services Status',
    menu_campaigns: '🎯 Active Campaigns',
    menu_market: '📈 Market Data',
    menu_strategies: '🧠 Strategies',
    menu_alerts: '🔔 Alerts',
    menu_risk: '⚠️ Risk Monitor',
    menu_snapshot: '📸 Full Snapshot',
    menu_refresh: '🔄 Refresh',

    // Authorization
    unauthorized: '⛔ Unauthorized. Contact administrator.',

    // Portfolio
    portfolio_title: '💼 Portfolio',
    portfolio_no_positions: 'No open positions',
    portfolio_position: 'Position: {symbol}\nType: {type}\nQuantity: {quantity}\nP&L: {pnl}',

    // Status
    status_title: '⚙️ Services Status',
    status_ok: '✅ {service} — OK (last heartbeat: {time})',
    status_stale: '⚠️ {service} — STALE (last heartbeat: {time})',
    status_offline: '❌ {service} — OFFLINE',

    // Campaigns
    campaigns_title: '🎯 Active Campaigns',
    campaigns_no_active: 'No active campaigns',
    campaigns_item: '🎯 {name}\nStatus: {status}\nPositions: {positions}',

    // Market
    market_title: '📈 Market Data',
    market_no_data: 'No data available',

    // Strategies
    strategies_title: '🧠 Strategies',
    strategies_no_active: 'No active strategies',
    strategies_item: '{name}\nStatus: {status}\nLast signal: {signal}',

    // Alerts
    alerts_title: '🔔 Alerts',
    alerts_no_unresolved: 'No unresolved alerts',
    alerts_item: '{severity} {message}\nDate: {time}',

    // Risk
    risk_title: '⚠️ Risk Monitor',
    risk_exposure: 'Total exposure: {exposure}',
    risk_margin: 'Margin used: {margin}',
    risk_ok: '✅ All risk parameters are OK',

    // Snapshot
    snapshot_title: '📸 Full Snapshot',
    snapshot_generating: '⏳ Generating snapshot...',
    snapshot_ready: '✅ Snapshot ready!',

    // Errors
    error_generic: '❌ Error: {message}',
    error_timeout: '⏱️ Timeout during processing',
    error_database: '💾 Database error: {message}',

    // Command processing
    command_unknown: '❓ Unknown command. Use /menu to see available commands.',
    command_processing: '⏳ Processing...',

    // Whitelist admin commands
    whitelist_add_success: '✅ User {userId} added to whitelist',
    whitelist_add_already_exists: '⚠️ User {userId} already in whitelist',
    whitelist_add_missing_userid: '❌ Usage: /whitelist add <user_id>',
    whitelist_remove_success: '✅ User {userId} removed from whitelist',
    whitelist_remove_not_found: '⚠️ User {userId} not found in whitelist',
    whitelist_remove_missing_userid: '❌ Usage: /whitelist remove <user_id>',
    whitelist_list_title: '👥 *WHITELISTED USERS*',
    whitelist_list_empty: 'No users in whitelist'
  }
}

/**
 * Translate a key to the specified language
 * @param key - Message key to translate
 * @param lang - Target language (defaults to 'en')
 * @param params - Optional parameters for string interpolation
 * @returns Translated string
 */
export function t(key: string, lang: Language = 'en', params?: Record<string, string>): string {
  let message = messages[lang]?.[key] || messages.en[key] || key

  // Replace parameters if provided
  if (params) {
    Object.entries(params).forEach(([paramKey, value]) => {
      message = message.replace(`{${paramKey}}`, value)
    })
  }

  return message
}

/**
 * Detect user language from Telegram language_code or Discord locale
 * @param languageCode - Language code from bot platform
 * @returns Normalized language ('it' or 'en')
 */
export function detectLanguage(languageCode?: string): Language {
  if (!languageCode) {
    return 'en'
  }

  const normalized = languageCode.toLowerCase()
  if (normalized.startsWith('it')) {
    return 'it'
  }

  return 'en'
}
