# Real-Money Review Remediation Plan

Data: 2026-04-28
Stato: draft operativo
Scope: `OptionsExecutionService`, `TradingSupervisorService`, integrazione IBKR

## Executive Summary

La review ha identificato blocker P1 che impediscono di considerare il sistema pronto per operativita real-money. I problemi principali sono:

- esecuzione campagne ancora stubbed;
- callback broker non persistite nei tracking table;
- account equity non alimentata in modo production;
- order id IBKR riutilizzabili nella stessa sessione;
- option contract costruiti in modo ambiguo o convertibili accidentalmente in stock order;
- circuit breaker conservato in un servizio scoped;
- runtime config non allineata alle chiavi validate.

Decisione operativa: non abilitare campagne real-money finche tutti i P1 non sono chiusi, testati e validati su IBKR paper/live-sim.

## Stop Gates

Questi gate devono restare bloccanti per il go-live:

- Nessuna campagna puo passare ad `Active` senza posizioni broker reali confermate o senza un failure esplicito.
- Nessun ordine puo essere inviato a IBKR se l'opzione non e identificata in modo completo tramite `conId` oppure `symbol`, `expiry`, `strike`, `right`, `exchange`, `currency`, `multiplier`.
- Nessun ordine puo essere inviato se non e disponibile una account equity/account balance recente.
- Il circuit breaker deve sopravvivere ai cicli worker e alle nuove scope DI.
- Le callback `orderStatus`, `openOrder`, `execDetails`, `commissionReport` ed errori rilevanti devono aggiornare lo stato persistente.
- I test paper/live-sim devono coprire multi-order session, rejection, partial fill, cancellation, fill completo, drawdown pause e riavvio servizio.

## Finding Matrix

| ID | Priorita | Area | File | Finding | Remediation sintetica | Acceptance criteria |
| --- | --- | --- | --- | --- | --- | --- |
| RM-01 | P1 | IBKR order ids | `src/OptionsExecutionService/Ibkr/IbkrClient.cs` | `nextValidId` viene riutilizzato per piu ordini. | Introdurre riserva atomica/lock degli order id e incrementare localmente ogni placement. | Due o piu ordini nella stessa sessione usano id unici e monotoni. |
| RM-02 | P1 | Option contracts | `src/OptionsExecutionService/Ibkr/IbkrClient.cs` | Ordini option incompleti possono diventare stock order o contract ambigui. | Validare contract option obbligatorio prima di `placeOrder`; rimuovere default silenzioso a `STK`. | Ordini incompleti falliscono prima di IBKR con errore esplicito. |
| RM-03 | P1 | Campaign execution | `src/OptionsExecutionService/Orders/OrderPlacer.cs` | Entry campagne ritorna fake position ids senza inviare ordini broker. | Sostituire stub con chiamate reali a `PlaceOrderAsync` per ogni leg e attivare la campagna solo dopo esito coerente. | Campagna `Active` solo se gli ordini richiesti sono creati e tracciati. |
| RM-04 | P1 | Broker callbacks | `src/OptionsExecutionService/Orders/OrderPlacer.cs` | Dopo `placeOrder`, gli ordini restano `Submitted`; callback non persistite. | Aggiungere handler production per status, errori, fills, executions, commissioni e posizioni. | DB riflette submitted, filled, partial, cancelled, rejected e realized PnL. |
| RM-05 | P1 | Circuit breaker | `src/OptionsExecutionService/Program.cs` | Stato breaker in `OrderPlacer` scoped, reset a ogni scope worker. | Estrarre stato in singleton/thread-safe store o servizio dedicato. | Breaker trippato blocca nuove scope fino a cooldown completato. |
| RM-06 | P1 | Account balance safety | `src/OptionsExecutionService/Orders/OrderPlacer.cs` | `_cachedAccountBalance` parte da 0 e non viene aggiornato in production. | Collegare account summary/equity feed o fetch sincrono prima dei safety check. | Ordini diretti usano equity recente o vengono rifiutati per dati stale. |
| RM-07 | P1 | Supervisor account summary | `src/TradingSupervisorService/Ibkr/IbkrClient.cs` | `RequestAccountSummaryAsync` logga soltanto e non chiama IBKR. | Implementare `reqAccountSummary`, callback parsing e cancellation. | `/api/performance/today` e `DailyPnLWatcher` ricevono equity aggiornata. |
| RM-08 | P2 | Live Greeks | `src/TradingSupervisorService/Workers/GreeksMonitorWorker.cs` | Subscription Greeks usa solo underlying symbol, non il contract option reale. | Sottoscrivere market data con `conId` o contract completo della posizione. | `tickOptionComputation` aggiorna le Greeks della posizione corretta. |
| RM-09 | P2 | Safety config | `src/OptionsExecutionService/Program.cs` | Runtime legge chiavi diverse da quelle validate/configurate. | Allineare binding runtime a `Safety:MaxRiskPercentOfAccount` e `Safety:CircuitBreakerResetMinutes`. | Cambi config validati producono effetti runtime verificabili. |

## Detailed Remediation

### RM-01 - Incrementare gli IBKR order id

Problema: il client salva `nextValidId`, ma non riserva/incrementa localmente l'id prima di inviare ordini successivi.

Remediation:

- Aggiungere uno stato thread-safe in `IbkrClient`, per esempio lock dedicato o `Interlocked`, per riservare l'id.
- Alla callback `nextValidId`, inizializzare o riallineare il contatore locale usando il massimo tra valore attuale e valore IBKR.
- Prima di ogni `placeOrder`, chiamare un metodo `ReserveOrderId()` che ritorna l'id corrente e incrementa il prossimo.
- Gestire reconnect e nuovi `nextValidId` senza tornare indietro.
- Loggare order id, perm id se disponibile, e correlation id della richiesta.

Test consigliati:

- Unit test con due placement consecutivi: id distinti e monotoni.
- Test reconnect: callback con id minore non abbassa il contatore.
- Paper test: invio di almeno due ordini simulati nella stessa sessione.

### RM-02 - Validare option contract completi

Problema: un ordine options con solo `ContractSymbol` puo essere inviato come `STK`; un ordine `OPT` con campi mancanti puo essere ambiguo.

Remediation:

- Definire una contract policy esplicita per `OptionsExecutionService`: option-only, salvo eccezioni motivate e validate.
- Rifiutare ordini option se mancano `expiry`, `strike`, `right`, `exchange`, `currency` e preferibilmente `multiplier`.
- Preferire `conId` quando gia disponibile dai dati posizione/chain.
- Se si supporta parsing da `ContractSymbol`, implementarlo in un parser testato, non dentro al builder IBKR.
- Non defaultare piu a `SecType = "STK"` nel path options.
- Rendere gli errori di validazione chiari e persistibili come order rejection interna.

Test consigliati:

- Contract option completo produce `Contract` IBKR completo.
- Contract con solo symbol fallisce prima di chiamare IBKR.
- `SecType` mancante nel servizio options non diventa `STK`.
- Parsing di simboli supportati, con casi limite su expiry/right/strike.

### RM-03 - Sostituire gli stub di campaign order

Problema: l'ingresso campagna ritorna fake position ids e la campagna passa ad `Active` senza broker positions reali.

Remediation:

- Identificare il metodo di entry usato da `CampaignManager` e sostituire lo stub con chiamate a `PlaceOrderAsync`.
- Creare una richiesta ordine per ogni leg della campagna, con contract validato e quantita corretta.
- Persistire una relazione tra campagna, leg, order id broker e posizione attesa.
- Rendere l'attivazione transazionale a livello applicativo:
  - `PendingEntry` quando gli ordini sono creati/inviati;
  - `Active` solo dopo fill coerenti o policy esplicita per partial fill;
  - `EntryFailed` o stato equivalente se un leg viene rifiutato.
- Gestire compensazione/manual review se alcuni leg vengono fillati e altri rifiutati.
- Rimuovere PnL hard-coded a zero nel path campaign.

Test consigliati:

- Campaign entry crea ordini reali/tracciati per tutte le leg.
- Rejection su una leg non marca la campagna `Active`.
- Partial fill resta in stato coerente e non produce PnL fittizio.
- Test paper su campagna multi-leg.

### RM-04 - Persistire callback broker nei tracking table

Problema: dopo `placeOrder`, lo stato viene marcato `Submitted`, ma nessun handler production aggiorna fills, rejection, cancellation, positions o PnL.

Remediation:

- Collegare `IIbkrClient.OrderStatusChanged` e `OrderError` a un servizio singleton/background oppure a un event dispatcher scoped-safe.
- Persistire transizioni ordine:
  - `Submitted`;
  - `PreSubmitted`;
  - `Filled`;
  - `PartiallyFilled`;
  - `Cancelled`;
  - `Rejected`;
  - `Inactive`;
  - errori IBKR rilevanti.
- Usare execution details e commission report per fill price, realized PnL, commissioni e posizione.
- Rendere idempotenti gli update: le callback IBKR possono arrivare duplicate o fuori ordine.
- Correlare callback tramite broker order id, perm id e internal order id.
- Aggiornare la posizione/campaign state machine dopo ogni transizione rilevante.

Test consigliati:

- Simulare callback duplicate: un solo update effettivo.
- Simulare fill parziale e poi fill completo.
- Simulare rejection e verificare che safety/campaign reagiscano.
- Test di riavvio servizio con ordini gia submitted.

### RM-05 - Spostare il circuit breaker fuori da `OrderPlacer` scoped

Problema: `OrderPlacer` e scoped, quindi ogni ciclo worker ottiene un breaker chiuso e perde lo stato trippato.

Remediation:

- Estrarre lo stato in un servizio dedicato, per esempio `IOrderCircuitBreaker`, registrato singleton.
- Conservare almeno:
  - stato aperto/chiuso;
  - motivo apertura;
  - timestamp apertura;
  - timestamp earliest reset;
  - contatori errori se richiesti.
- Rendere thread-safe le operazioni `Trip`, `CanPlaceOrder`, `ResetIfExpired`.
- Fare usare il servizio sia da `OrderPlacer` sia da worker/API che inviano ordini.
- Loggare ogni trip/reset con correlation id e motivo.

Test consigliati:

- Due scope DI diverse vedono lo stesso breaker aperto.
- Cooldown impedisce ordini fino alla scadenza.
- Reset dopo cooldown avviene una sola volta e viene loggato.

### RM-06 - Collegare account balance/equity al safety check

Problema: `_cachedAccountBalance` parte da 0 e non viene aggiornato da servizi production, quindi gli ordini diretti falliscono o dipendono da mutazioni test-only.

Remediation:

- Introdurre un provider production, per esempio `IAccountEquityProvider`, condiviso tra servizi che eseguono ordini.
- Alimentarlo con account summary IBKR o con fetch esplicito prima del safety check.
- Associare ogni valore a `asOfUtc` e rifiutare ordini se il dato e stale.
- Configurare una soglia di freshness, per esempio `Safety:AccountBalanceMaxAgeSeconds`.
- Rendere il failure esplicito: `AccountBalanceUnavailable` o `AccountBalanceStale`.
- Evitare cache locale non condivisa dentro `OrderPlacer`, oppure usarla solo come view di uno store singleton.

Test consigliati:

- Balance assente: ordine rifiutato con errore esplicito.
- Balance stale: ordine rifiutato.
- Balance fresco sopra soglia: safety passa.
- Balance fresco sotto `MinAccountBalanceUsd`: safety fallisce.

### RM-07 - Implementare account summary in `TradingSupervisorService`

Problema: `RequestAccountSummaryAsync` nel supervisor logga soltanto e non invia `reqAccountSummary`, quindi non arrivano callback equity.

Remediation:

- Implementare `reqAccountSummary` usando request id univoco.
- Richiedere tag necessari, almeno `NetLiquidation`, `TotalCashValue`, `AvailableFunds`, `ExcessLiquidity`, `RealizedPnL`, `UnrealizedPnL`, in base alla logica gia esistente.
- Implementare callback `accountSummary` e `accountSummaryEnd`.
- Implementare cancellation con `cancelAccountSummary`.
- Persistire o pubblicare i valori verso `MarketDataCollector`, `/api/performance/today` e `DailyPnLWatcher`.
- Gestire timeout e stato stale.

Test consigliati:

- Unit test callback parsing.
- Integration test con fake EWrapper: request, callback, end, cancellation.
- Paper test: equity aggiornata visibile in performance endpoint.
- Drawdown test: `DailyPnLWatcher` imposta `trading_paused`.

### RM-08 - Usare il contract option reale per Greeks

Problema: la subscription live Greeks usa `secType = "OPT"` ma solo underlying symbol e exchange; IBKR non puo identificare l'opzione specifica.

Remediation:

- Recuperare dalla posizione il `conId` IBKR se disponibile.
- In alternativa costruire contract completo da expiry, strike, right, exchange, currency e multiplier.
- Rifiutare/skipparne la subscription se la posizione non ha contract identificabile, marcando Greeks stale.
- Correlare `tickOptionComputation` alla posizione corretta tramite ticker id e mapping persistito/in memoria.
- Aggiungere metriche/log per subscription non create e dati stale.

Test consigliati:

- Posizione con `conId`: market data request contiene `conId`.
- Posizione senza `conId` ma con campi completi: request contract completo.
- Posizione incompleta: skip esplicito e alert/stale flag.

### RM-09 - Allineare le chiavi config validate e runtime

Problema: validator e config usano `Safety:MaxRiskPercentOfAccount` e `Safety:CircuitBreakerResetMinutes`, mentre runtime legge `MaxPositionPctOfAccount` e `CircuitBreakerCooldownMinutes`.

Remediation:

- Decidere i nomi canonici e usarli in config, validator, binding runtime e documentazione.
- Per compatibilita temporanea, supportare alias legacy solo con warning chiaro.
- Aggiungere test di binding config: valori impostati nel file producono i valori runtime attesi.
- Aggiornare `docs/CONFIGURATION.md`, checklist e sample config se necessario.

Test consigliati:

- Config con chiavi canoniche modifica risk percent e breaker reset.
- Config con chiavi legacy produce warning oppure fallisce, secondo policy scelta.
- Validator e runtime leggono la stessa source of truth.

## Implementation Plan

### Phase 0 - Safety freeze e baseline

Obiettivo: impedire go-live accidentale mentre i blocker sono aperti.

Task:

- Aggiungere un feature flag o guardrail esplicito, se non esiste, per bloccare real-money campaign activation.
- Aprire issue/task separati per RM-01..RM-09.
- Salvare baseline test attuale e identificare test suite rilevanti.
- Definire dati paper IBKR da usare per option contract e account summary.

Deliverable:

- Lista task tracciabile.
- Go-live bloccato dai P1.
- Baseline test nota.

### Phase 1 - Broker primitives P1

Obiettivo: rendere sicuro il path minimo di invio ordine.

Task:

- Implementare RM-01 order id reservation.
- Implementare RM-02 contract validation option-only.
- Allineare RM-09 config safety runtime/validator.

Deliverable:

- Unit test su order id.
- Unit test su contract validation.
- Config binding test.

Exit criteria:

- Nessun ordine puo uscire con id duplicato nella stessa sessione.
- Nessun option order incompleto viene inviato a IBKR.
- Le chiavi config documentate sono quelle usate runtime.

### Phase 2 - Shared safety state P1

Obiettivo: rendere persistenti nella vita del processo i segnali di safety.

Task:

- Implementare RM-05 circuit breaker singleton/thread-safe.
- Implementare RM-06 account equity provider production.
- Collegare provider al safety check di `OrderPlacer`.

Deliverable:

- Test multi-scope sul circuit breaker.
- Test balance missing/stale/fresh.
- Log strutturati per trip/reset e balance freshness.

Exit criteria:

- Il breaker non si resetta al ciclo successivo del worker.
- Gli ordini usano equity recente oppure falliscono esplicitamente.

### Phase 3 - Broker callback persistence P1

Obiettivo: rendere lo stato ordini/posizioni guidato dalle callback broker.

Task:

- Implementare RM-04 event handling production.
- Persistire order status, errori, execution details e commissioni.
- Rendere idempotenti gli update.
- Aggiornare state machine posizione/campaign in base agli eventi.

Deliverable:

- Test su submitted, partial fill, fill, cancellation, rejection.
- Test callback duplicate/fuori ordine.
- Mapping broker order id/perm id/internal order id documentato.

Exit criteria:

- Il DB non resta bloccato su `Submitted` dopo callback broker.
- Risk e campaign logic leggono stati aggiornati.

### Phase 4 - Campaign execution P1

Obiettivo: rimuovere gli stub che marcano campagne attive senza posizioni reali.

Task:

- Implementare RM-03 per entry campaign reali.
- Definire stati `PendingEntry`, `Active`, `EntryFailed` o equivalenti gia presenti.
- Gestire partial fill e failure multi-leg.
- Rimuovere PnL hard-coded.

Deliverable:

- Test campaign entry multi-leg.
- Test rejection/partial fill.
- Paper test con campagna simulata.

Exit criteria:

- Una campagna diventa `Active` solo dopo condizioni broker coerenti.
- Non esistono fake position ids nel path production.

### Phase 5 - Supervisor market/account data P1/P2

Obiettivo: alimentare equity, drawdown watcher e Greeks con dati IBKR corretti.

Task:

- Implementare RM-07 account summary request/callback/cancel.
- Implementare RM-08 Greeks subscription con contract reale.
- Collegare dati aggiornati a performance endpoint e watchers.

Deliverable:

- Test account summary callback.
- Test `DailyPnLWatcher` con drawdown intraday.
- Test Greeks request per `conId` e contract completo.

Exit criteria:

- `/api/performance/today` riceve equity recente.
- `trading_paused` viene aggiornato su drawdown.
- Greeks live sono associate alla posizione option corretta o marcate stale.

### Phase 6 - Paper/live-sim validation

Obiettivo: validare il comportamento end-to-end prima del real-money.

Scenari minimi:

- Avvio servizi con account paper collegato.
- Request account summary e aggiornamento equity.
- Invio due ordini nella stessa sessione, con order id unici.
- Option order completo accettato dal broker.
- Option order incompleto rifiutato localmente.
- Fill completo con stato DB aggiornato.
- Partial fill con stato DB coerente.
- Cancellation e rejection persistite.
- Campaign entry multi-leg in paper.
- Drawdown simulato o controllato che imposta `trading_paused`.
- Riavvio servizio con ordini pendenti e recupero stato.

Deliverable:

- Report paper validation con data, account, build/commit, scenari, esito.
- Log e screenshot/export DB per scenari critici.
- Lista residual risk prima del go-live.

## Suggested Work Order

Ordine consigliato per ridurre rischio e dipendenze:

1. RM-09 config keys, per evitare di testare con valori runtime sbagliati.
2. RM-01 order id reservation.
3. RM-02 option contract validation.
4. RM-05 circuit breaker singleton.
5. RM-06 account equity provider nel servizio execution.
6. RM-07 account summary nel supervisor.
7. RM-04 callback persistence.
8. RM-03 campaign execution reale.
9. RM-08 Greeks contract subscription.
10. Phase 6 paper/live-sim validation.

## Definition of Done

Un item P1 e chiuso solo se:

- il codice production e implementato;
- i test automatici coprono success e failure path;
- i log contengono informazioni sufficienti per ricostruire decisioni e broker events;
- non ci sono stub o valori hard-coded nel path real-money;
- esiste almeno una validazione paper/live-sim per i flussi broker coinvolti;
- documentazione config/runbook aggiornata se il comportamento operativo cambia.

## Open Questions

- Quale modello stato campagna deve rappresentare partial fill multi-leg: stato dedicato o `PendingEntry` con dettaglio leg-level?
- Esiste gia un table/model canonico per broker executions e commission reports o va aggiunto?
- L'option contract canonico deve essere `conId`-first oppure parser simbolo-first?
- Quale account value e la source of truth per i safety check: `NetLiquidation`, `AvailableFunds`, `ExcessLiquidity` o una combinazione?
- Il sistema deve supportare stock orders nello stesso `OptionsExecutionService` oppure devono essere esclusi dal path real-money?

