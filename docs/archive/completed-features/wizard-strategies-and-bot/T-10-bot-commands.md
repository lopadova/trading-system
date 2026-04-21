# T-BOT-02 — D1 Queries + Formatters + Semaphores 🔴🟡🟢

## Obiettivo
Implementare tutta la logica di business del bot: 6 query D1 per ogni tipo
di comando, logica semafori 🔴🟡🟢 con soglie precise, formatter messaggi
Markdown per ogni tipo di risposta, e integrazione nel dispatchCommand.

## Dipendenze
- T-BOT-01 (dispatcher e auth)

## Files da Creare
- `infra/cloudflare/worker/src/bot/semaphores.ts`
- `infra/cloudflare/worker/src/bot/queries/portfolio-query.ts`
- `infra/cloudflare/worker/src/bot/queries/status-query.ts`
- `infra/cloudflare/worker/src/bot/queries/risk-query.ts`
- `infra/cloudflare/worker/src/bot/queries/market-query.ts`
- `infra/cloudflare/worker/src/bot/queries/campaigns-query.ts`
- `infra/cloudflare/worker/src/bot/queries/alerts-query.ts`
- `infra/cloudflare/worker/src/bot/formatters/portfolio-formatter.ts`
- `infra/cloudflare/worker/src/bot/formatters/status-formatter.ts`
- `infra/cloudflare/worker/src/bot/formatters/risk-formatter.ts`
- `infra/cloudflare/worker/src/bot/formatters/market-formatter.ts`
- `infra/cloudflare/worker/src/bot/formatters/snapshot-formatter.ts`

## Files da Modificare
- `infra/cloudflare/worker/src/bot/dispatcher.ts` — implementare dispatchCommand

## Implementazione

### semaphores.ts — Logica colori

| Funzione | Verde 🟢 | Giallo 🟡 | Rosso 🔴 |
|---|---|---|---|
| `pnlSignal(pnl)` | >= 0 | >= -200 | < -200 |
| `pnlVsStopSignal(pnl, stop)` | ratio < 50% | 50-80% | >= 80% |
| `heartbeatSignal(ageMin)` | < 3 | 3-10 | >= 10 |
| `ivtsSignal(ivts, state)` | Active AND < 1.10 | 1.10-1.15 | Suspended OR > 1.15 |
| `deltaSignal(delta, limit)` | ratio < 60% | 60-85% | >= 85% |
| `thetaSignal(theta, limit)` | ratio < 60% | 60-85% | >= 85% |
| `spxVsWingSignal(spx, wing)` | dist > 150pt | 50-150pt | < 50pt |
| `daysRemainingSignal(d, max)` | pct remaining > 30% | 10-30% | < 10% |
| `processSignal(status)` | running | anything else | stopped |
| null input → ⚪ (sempre)

### Queries D1 principali

**queryPortfolio**: 
- `campaign_states WHERE status='monitoring'` per campagne + PnL unrealized
- `market_data_history ORDER BY captured_at DESC LIMIT 1` per IVTS/VIX
- `closed_campaigns` aggregato per PnL today/MTD/YTD e win rate

**queryStatus**:
- `heartbeats ORDER BY timestamp DESC LIMIT 1`
- `process_states` aggruppato per process_name (ultimi 15 min)

**queryRisk**:
- JOIN `campaign_states` + `portfolio_greek_snapshots` (ultimo snapshot)
- JSON_EXTRACT da `strategy_states.definition_json` per target/stop/max_days

**queryMarket**:
- `market_data_history ORDER BY captured_at DESC LIMIT 1`
- `market_data_history ORDER BY captured_at DESC LIMIT 30` per sparkline IVTS 30gg

**queryAlerts**:
- `alert_log ORDER BY sent_at DESC LIMIT 10`

### Formatters — Markdown per Telegram

Tutti usano *bold* (asterischi), nessun HTML.
Ogni formatter riceve i dati + `lang: 'it'|'en'`.
Testi via `t(key, lang)` da i18n.ts.

Esempio formato portfolio (italiano):
```
📊 *PORTFOLIO*
━━━━━━━━━━━━━━━━━━━━
💰 PnL Oggi: +$1.240 🟢
💰 PnL MTD: +$4.820 🟢
💰 PnL YTD: +$18.340 🟢
📈 Win Rate: 73% 🟢

📌 CAMPAGNE ATTIVE: 3
├─ opt-spx-250  D+12  PnL: +$820  🟢
├─ opt-spx-180  D+5   PnL: +$240  🟢
└─ opt-spx-250b D+2   PnL:  -$80  🟡

🌡️ IVTS: 0.94  🟢 ATTIVO
📉 SPX: 5.234,50
😱 VIX: 18,4  VIX3M: 19,6

🕐 04/04/2026 14:32:15 ET
```

### dispatchCommand — integrazione completa

```typescript
switch (command.type) {
  case 'menu':     → send menu text + makeMainKeyboard(lang)
  case 'query':
    portfolio: data = await queryPortfolio(db); text = formatPortfolio(data, lang); send(text, refreshKb)
    status:    data = await queryStatus(db);    text = formatStatus(data, lang);    send(text, refreshKb)
    risk:      data = await queryRisk(db);      text = formatRisk(data, lang);      send(text, refreshKb)
    snapshot:  [portfolio + status] in msg1, [risk + market] in msg2 (sequenziali)
    ...
}
```

## Test
- `TEST-BOT-02-01`: `pnlSignal(100)` → '🟢'
- `TEST-BOT-02-02`: `pnlSignal(-500)` → '🔴'
- `TEST-BOT-02-03`: `pnlSignal(null)` → '⚪'
- `TEST-BOT-02-04`: `heartbeatSignal(2)` → '🟢'
- `TEST-BOT-02-05`: `heartbeatSignal(8)` → '🟡'
- `TEST-BOT-02-06`: `spxVsWingSignal(5234, 5200)` → '🟡' (34pt < 50)
- `TEST-BOT-02-07`: `spxVsWingSignal(5234, 4900)` → '🟢' (334pt > 150)
- `TEST-BOT-02-08`: `pnlVsStopSignal(-4100, 5000)` → '🔴' (82%)
- `TEST-BOT-02-09`: formatPortfolio con 3 campagne mock → 3 righe campagna nel testo
- `TEST-BOT-02-10`: dispatchCommand 'menu' → telegramSend chiamato con keyboard

## Done Criteria
- [ ] Build pulito (Worker TypeScript)
- [ ] Tutti i test TEST-BOT-02-XX passano
- [ ] Tutte le query D1 usano prepared statements (no SQL injection)
- [ ] dispatchCommand gestisce tutti i 8 tipi di query senza eccezioni non gestite
- [ ] /snapshot genera 2 messaggi distinti in sequenza

## Stima
~2 giorni
