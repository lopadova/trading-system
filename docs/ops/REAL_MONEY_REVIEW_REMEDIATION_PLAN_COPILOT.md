---
title: "Real-Money Review Remediation Plan - Copilot"
tags: ["ops", "safety", "trading", "review", "remediation"]
aliases: ["REAL_MONEY_REVIEW_REMEDIATION_PLAN_COPILOT", "Copilot Real Money Review"]
status: draft
audience: ["operator", "developer"]
last-reviewed: "2026-04-28"
related:
  - "[[GO_LIVE]]"
  - "[[PAPER_VALIDATION]]"
  - "[[REAL_MONEY_REVIEW_REMEDIATION_PLAN]]"
---

# Real-Money Review Remediation Plan - Copilot

> Deep review focalizzata su robustezza, safety, correttezza operativa,
> auditability e rischio finanziario per:
>
> - `src/TradingSupervisorService`
> - `src/OptionsExecutionService`
>
> Questo documento traduce le scoperte della review in un piano
> implementativo con guardrail espliciti. Finche' i blocker P0/P1 restano
> aperti, il sistema NON e' idoneo a gestire soldi reali.

---

## 1. Executive summary

La review ha evidenziato problemi reali e ad alto impatto soprattutto in
`OptionsExecutionService`, nel punto piu' sensibile dell'intero sistema:
sequenziamento ordini, persistenza stato, callback broker, safety check e
idempotenza tra chiamate esterne e stato interno.

I rischi principali sono:

- invio ordini con `orderId` duplicato verso IBKR;
- circuit breaker che non sopravvive ai cicli del worker;
- pipeline di entry/exit ancora stubbed nei path piu' importanti;
- attivazione campagna non atomica rispetto all'invio ordini al broker;
- account balance non alimentato in modo affidabile;
- exit time-based che puo' ritentare close multipli;
- audit trail non affidabile in caso di payload outbox corrotti;
- alert storm sulle Greeks per mancanza di deduplica;
- soglie di stop-loss/profit-target calcolate su capitale allocato e non
  sul premio reale della posizione.

**Decisione operativa:** non abilitare il live trading finche' tutti i
blocker P0 e P1 non sono chiusi, testati, paper-validati e verificati con
riavvio servizio e failure injection.

---

## 2. No-go guardrails immediati

Questi guardrail devono essere considerati bloccanti da subito:

1. Nessuna campagna puo' passare a `Active` senza ordini broker reali
   tracciati e riconciliabili.
2. Nessun ordine puo' essere inviato se non esiste un `orderId` locale
   univoco, monotono e riservato atomicamente.
3. Nessun ordine puo' essere inviato se account balance/equity non sono
   disponibili oppure sono stale oltre la soglia configurata.
4. Nessun path live puo' usare stub, valori fittizi o PnL hard-coded.
5. Nessun close puo' essere ritentato in automatico senza idempotency key o
   stato persistito che impedisca duplicate close.
6. Nessun evento outbox corrotto puo' essere marcato come `sent`.
7. Nessuna feature che modifica order flow puo' essere promossa a live
   senza test su:
   - order rejection;
   - partial fill;
   - reconnect;
   - riavvio servizio;
   - callback duplicate/out-of-order;
   - stale market/account data.

---

## 3. Finding matrix

| ID | Severity | Area | File | Finding | Impatto real-money | Remediation sintetica |
| --- | --- | --- | --- | --- | --- | --- |
| COP-01 | P0 | IBKR order sequencing | `src/OptionsExecutionService/Ibkr/IbkrClient.cs` | `GetNextOrderId()` non incrementa localmente l'ID. | Rischio ordine sostituito, reject, stato incoerente. | Riserva atomica monotona locale con seed da `nextValidId`. |
| COP-02 | P0 | Circuit breaker | `src/OptionsExecutionService/Orders/OrderPlacer.cs`, `src/OptionsExecutionService/Program.cs` | Breaker in servizio scoped, di fatto si resetta a ogni ciclo. | Protezione inefficace durante failure intermittenti. | Spostare stato in singleton o in store persistente/thread-safe. |
| COP-03 | P0 | Campaign execution | `src/OptionsExecutionService/Orders/OrderPlacer.cs` | Entry/exit/PnL ancora stubbed. | Stop-loss e profit-target non affidabili; rischio capitale non protetto. | Implementare pipeline reale broker-backed e rimuovere stub dai path live. |
| COP-04 | P1 | Account balance safety | `src/OptionsExecutionService/Orders/OrderPlacer.cs`, `src/OptionsExecutionService/Ibkr/TwsCallbackHandler.cs` | Balance parte da 0 e non viene alimentato correttamente. | Safety check falsati, ordini rigettati o dati non affidabili. | Provider condiviso con freshness esplicita e callback/fetch reali. |
| COP-05 | P1 | Atomicita broker/DB | `src/OptionsExecutionService/Campaign/CampaignManager.cs` | Ordini inviati prima del salvataggio stato campagna. | Crash tra i due step puo' produrre doppio ingresso o posizioni orfane. | Introdurre stato `PendingEntry` + idempotenza + recovery al riavvio. |
| COP-06 | P1 | Exit idempotency | `src/OptionsExecutionService/Campaign/CampaignManager.cs` | Dopo `ExitTimeOfDay`, il close puo' essere ritentato ogni ciclo. | Rischio close ripetuti dopo errori transienti. | Persistire trigger close e impedire duplicate attempts. |
| COP-07 | P2 | Outbox integrity | `src/TradingSupervisorService/Workers/OutboxSyncWorker.cs` | JSON corrotto marcato come `sent`. | Buco audit silenzioso e perdita tracciabilita'. | Stato `permanent_failure` o dead-letter, mai `sent`. |
| COP-08 | P2 | Greeks alert storm | `src/TradingSupervisorService/Workers/GreeksMonitorWorker.cs` | Nessuna deduplica temporale reale per breach persistenti. | Rumore operativo, crescita outbox e alert fatigue. | Cooldown per `positionId + alertType` e metriche dedicate. |
| COP-09 | P2 | Exit economics | `src/OptionsExecutionService/Campaign/CampaignManager.cs` | Stop-loss/profit-target usano `CapitalPerPosition` al posto del premio reale. | Soglie economicamente sbagliate, uscite non affidabili. | Salvare `EntryCredit`/premium reale e basare su quello i threshold. |

---

## 4. Dettaglio findings e remediation

### COP-01 - Order ID IBKR monotoni e atomici

**Problema**

Il seed `nextValidId` viene letto ma non consumato come contatore locale.
In una sessione con ordini multipli due placement ravvicinati possono
riusare lo stesso `orderId`.

**Remediation**

- Introdurre un campo locale thread-safe per il prossimo order id.
- Seed iniziale da `nextValidId`.
- Esportare un metodo `ReserveOrderId()` che:
  - riserva l'id corrente;
  - incrementa quello successivo;
  - non permette regressioni dopo reconnect.
- Se arriva un nuovo `nextValidId`, usare `max(local, incoming)`.
- Loggare sempre: `internalOrderId`, `ibkrOrderId`, `permId`, `campaignId`,
  `correlationId`.

**Acceptance criteria**

- Due ordini consecutivi nella stessa sessione hanno ID distinti e
  monotoni.
- Un reconnect non fa mai retrocedere il contatore.
- In load test single-session non esistono collisioni.

**Guardrail**

- Se l'order ID allocator non e' inizializzato, `PlaceOrderAsync` deve
  fallire in modo esplicito e bloccante.

### COP-02 - Circuit breaker condiviso e persistente

**Problema**

Il circuit breaker e' contenuto in uno scoped service e perde stato a ogni
nuovo scope creato dal worker.

**Remediation**

- Estrarre il breaker in `IOrderCircuitBreaker` singleton.
- Conservare almeno:
  - stato aperto/chiuso;
  - timestamp apertura;
  - motivo;
  - cooldown until;
  - contatori rolling window.
- Se il conteggio fallimenti e' in DB, il gate deve poter leggere dallo
  store persistente, non solo dalla memoria della scope corrente.
- Separare chiaramente:
  - `Trip(...)`
  - `CanPlaceOrder(...)`
  - `ResetIfExpired(...)`

**Acceptance criteria**

- Due scope diverse vedono lo stesso stato breaker.
- Dopo N failure nel rolling window il breaker blocca le scope successive.
- Il reset avviene una sola volta ed e' loggato.

**Guardrail**

- Se lo stato breaker non e' leggibile oppure lo store e' degradato, il
  sistema deve andare in fail-closed sui nuovi ordini, non in fail-open.

### COP-03 - Rimozione stub dai path live di entry/exit/PnL

**Problema**

Le API di entry, close e PnL ritornano valori fittizi o costanti. Questo
invalida sia la protezione finanziaria sia la riconciliazione operativa.

**Remediation**

- Sostituire `PlaceEntryOrdersAsync` con una pipeline reale che crei gli
  ordini broker per tutte le leg.
- Implementare `ClosePositionsAsync` con close idempotente broker-backed.
- Implementare `GetUnrealizedPnLAsync` da posizioni/fill reali oppure da
  snapshot broker affidabili.
- Proibire la compilazione/attivazione live se nel path sono presenti stub
  marcati `[STUB]`.
- Collegare campaign state machine ai fill reali:
  - `PendingEntry`
  - `PartiallyFilled`
  - `Active`
  - `PendingExit`
  - `Closed`
  - `EntryFailed` / `ExitFailed`

**Acceptance criteria**

- Una campagna non diventa `Active` senza ordini e fill tracciabili.
- Stop-loss e profit-target possono scattare su dati economici reali.
- Close e re-close sono idempotenti.

**Guardrail**

- Build/live gate che vieta `TradingMode=live` se esistono warning `[STUB]`
  nei path di order execution.

### COP-04 - Account balance/equity come prerequisito hard

**Problema**

Il balance locale parte da 0 e non viene aggiornato in modo affidabile nei
path produttivi.

**Remediation**

- Introdurre `IAccountEquityProvider` singleton.
- Alimentarlo con:
  - `accountSummary` IBKR;
  - oppure fetch esplicito prima del safety check.
- Associare a ogni valore:
  - `value`;
  - `currency`;
  - `asOfUtc`;
  - `source`.
- Configurare una freshness massima.
- Definire errori applicativi espliciti:
  - `AccountBalanceUnavailable`
  - `AccountBalanceStale`
  - `AccountBalanceCurrencyMismatch`

**Acceptance criteria**

- Ordine con balance assente o stale viene rifiutato.
- Ordine con balance fresco sopra soglia passa il gate.
- Tutti i path di order placement leggono dallo stesso provider.

**Guardrail**

- Nessun fallback silenzioso a `0`.
- Nessuna cache locale non condivisa dentro servizi scoped.

### COP-05 - Entry broker/DB resa idempotente e crash-safe

**Problema**

L'ordine viene mandato prima del salvataggio dello stato campagna. Se il
processo cade in mezzo si crea divergenza tra broker e DB.

**Remediation**

- Aggiungere uno stato persistito `PendingEntry`.
- Prima del submit, scrivere nel DB:
  - `campaignId`
  - `attemptId`
  - `requested legs`
  - `idempotency key`
  - stato `PendingEntry`
- Dopo il submit, salvare mapping tra leg e order id broker.
- Al restart, un recovery worker deve:
  - rilevare campagne in `PendingEntry`;
  - riconciliare con broker;
  - promuovere a `Active`, `EntryFailed` o `ManualReview`.
- Se una stessa entry viene rilanciata, deve riusare l'idempotency key e
  non generare un secondo ingresso.

**Acceptance criteria**

- Crash dopo submit ma prima del save non produce un doppio ingresso.
- Riavvio servizio riconcilia gli ordini gia' creati.
- Una campagna non viene reinviata due volte.

**Guardrail**

- Nessuna transizione `Open -> Active` senza aver registrato prima
  l'intenzione di entry in store persistente.

### COP-06 - Close idempotente dopo `ExitTimeOfDay`

**Problema**

Una volta superata `ExitTimeOfDay`, il worker puo' ritentare close a ogni
tick se il close precedente e' fallito in modo transiente.

**Remediation**

- Introdurre stato `PendingExit`.
- Persistire:
  - `exitReason`
  - `exitTriggeredAtUtc`
  - `exitAttemptId`
  - `closeOrderIds`
- Se esiste gia' una close in corso, i cicli successivi devono osservare lo
  stato e non rilanciare un nuovo close.
- Distinguere:
  - close non ancora inviato;
  - close inviato e pending broker;
  - close rejected;
  - close completed.

**Acceptance criteria**

- Dopo l'ora di uscita viene generato un solo close attempt logico.
- Errori transienti non producono close duplicati.
- Il retry usa lo stesso contesto/idempotency key o richiede reset
  esplicito.

**Guardrail**

- Nessun retry automatico di close senza evidenza persistita dello stato del
  tentativo precedente.

### COP-07 - Integrita' dell'outbox e audit trail

**Problema**

Payload JSON corrotti vengono marcati come inviati con successo.

**Remediation**

- Introdurre stato `permanent_failure` oppure DLQ.
- Registrare:
  - `failed_at_utc`
  - `failure_kind`
  - hash o estratto sicuro del payload corrotto
  - contatore retry
- Esporre metrica/alert dedicata per outbox corruption.
- Separare chiaramente:
  - transient failure
  - permanent failure
  - sent

**Acceptance criteria**

- Nessun evento corrotto viene marcato `sent`.
- L'operatore puo' vedere, contare e investigare gli eventi corrotti.

**Guardrail**

- Per l'audit, meglio bloccare e segnalare che fingere una consegna.

### COP-08 - Deduplica alert Greeks

**Problema**

Per un threshold breach persistente si creano alert e outbox nuovi a ogni
ciclo.

**Remediation**

- Introdurre dedupe key logica basata su:
  - `positionId`
  - `alertType`
  - finestra temporale
- Memorizzare `lastAlertedAtUtc`.
- Consentire riapertura alert solo dopo cooldown o cambio di stato
  significativo.
- Misurare il rate di emissione per tipo alert.

**Acceptance criteria**

- Un breach persistente non genera spam a ogni tick.
- Un nuovo alert viene emesso solo dopo cooldown o state transition.

**Guardrail**

- Nessun `Guid.NewGuid()` nella dedupe key di un alert che dovrebbe essere
  deduplicato.

### COP-09 - Exit thresholds basati sul premio reale

**Problema**

Le soglie di stop-loss e profit-target usano il capitale allocato invece del
credito/premio di ingresso reale.

**Remediation**

- Persistire `EntryCredit` o equivalent net premium al momento dei fill.
- Basare il calcolo di:
  - profit target;
  - stop loss;
  - max adverse excursion;
  sul valore economico reale della posizione.
- Distinguere chiaramente:
  - capitale allocato / margin impact;
  - premium ricevuto/pagato;
  - PnL corrente.

**Acceptance criteria**

- I threshold sono coerenti con la strategia e con i fill reali.
- Profit target e stop loss sono raggiungibili e verificabili.

**Guardrail**

- Nessuna exit economica deve dipendere da valori "proxy" se il valore
  economico reale e' disponibile o ricostruibile.

---

## 5. Piano implementativo

### Phase 0 - Safety freeze

**Obiettivo:** impedire live accidentalmente mentre i blocker sono aperti.

Task:

1. Mettere un hard gate su `TradingMode=live` se esistono finding P0/P1
   aperti.
2. Introdurre un flag esplicito `LiveTradingEligibility = false` fino a
   chiusura remediation.
3. Censire tutti i path live che dipendono da stub, callback mancanti o
   state machine incomplete.
4. Tracciare le attivita' in issue/task separati per `COP-01` ... `COP-09`.

Deliverable:

- live disabilitato in modo esplicito;
- lista remediation tracciabile;
- baseline attuale dei test e dei gap.

### Phase 1 - Broker primitives e invarianti di ordine

**Obiettivo:** rendere sicuro il minimo comune denominatore per l'invio
ordine.

Task:

1. Implementare `COP-01` order id allocator.
2. Introdurre correlation/idempotency model per entry e close.
3. Collegare `COP-04` account balance provider.
4. Agganciare logging strutturato per order lifecycle.

Deliverable:

- ordine inviabile solo con prerequisiti minimi affidabili;
- nessuna collisione order id;
- safety gates fail-closed.

### Phase 2 - Campaign state machine reale

**Obiettivo:** eliminare gli stub e rendere coerente il flusso
broker <-> DB <-> campaign state.

Task:

1. Implementare `COP-03` per entry/close/PnL.
2. Implementare `COP-05` `PendingEntry` + recovery.
3. Implementare `COP-06` `PendingExit` + close idempotente.
4. Definire in modo esplicito le transizioni consentite della campaign
   state machine.

Deliverable:

- campagne broker-backed;
- restart-safe recovery;
- no duplicate entries/closes.

### Phase 3 - Callback persistence e reconciliation

**Obiettivo:** rendere persistente lo stato reale proveniente da IBKR.

Task:

1. Persistire `orderStatus`, `openOrder`, `execDetails`,
   `commissionReport`, errori e cancellation.
2. Rendere idempotenti gli update verso DB.
3. Implementare recovery di ordini in-flight al riavvio.
4. Aggiornare campaign e position state in base agli eventi reali.

Deliverable:

- tracking ordini affidabile;
- fill e rejection visibili e auditabili;
- recovery coerente dopo restart.

### Phase 4 - Supervisor hardening

**Obiettivo:** chiudere i gap di audit e alerting in
`TradingSupervisorService`.

Task:

1. Implementare `COP-07` outbox permanent failure / DLQ.
2. Implementare `COP-08` deduplica alert Greeks.
3. Aggiungere metriche e alert su:
   - outbox corruption;
   - alert storm;
   - stale data;
   - reconciliation mismatch.

Deliverable:

- audit trail integro;
- minore rumore operativo;
- maggiore osservabilita' delle anomalie.

### Phase 5 - Economic correctness e go-live revalidation

**Obiettivo:** validare che le regole economiche siano corrette e il sistema
sia davvero pronto al live.

Task:

1. Implementare `COP-09` con `EntryCredit` reale.
2. Eseguire paper validation focalizzata su:
   - stop loss;
   - profit target;
   - partial fill;
   - rejection;
   - reconnect;
   - restart.
3. Aggiornare runbook, checklists e documentazione operativa.

Deliverable:

- exit economics corrette;
- evidenza paper completa;
- checklist go-live aggiornata.

---

## 6. Guardrails implementativi non negoziabili

Durante la remediation valgono questi vincoli:

1. **Fail-closed sui path ordine.**  
   Se mancano account balance, contract data, mapping order state o
   inizializzazione broker, l'ordine va rifiutato.

2. **Idempotenza prima delle chiamate esterne.**  
   L'intenzione di entry/exit va registrata prima di chiamare il broker.

3. **Nessun default silenzioso.**  
   Nessun fallback implicito a `0`, `[]`, `Guid.NewGuid()`, `STK`,
   `Submitted` o stati "success-shaped".

4. **Event sourcing minimo del lifecycle ordini.**  
   Ogni transizione importante deve lasciare traccia persistente.

5. **Recovery al riavvio obbligatoria.**  
   Qualunque stato intermedio (`PendingEntry`, `PendingExit`,
   `PartiallyFilled`) deve essere riconciliabile dopo restart.

6. **Timeout e freshness espliciti.**  
   Dati account, market data e callback broker devono avere
   timestamp/versione verificabili.

7. **Riconciliazione broker-first sui dubbi.**  
   In caso di mismatch tra memoria locale e broker, il sistema deve
   riconciliare e passare eventualmente in `ManualReview`, non proseguire
   alla cieca.

8. **Logging strutturato e correlabile.**  
   Ogni attempt deve includere ID sufficienti a seguire la storia end-to-end.

9. **No live enablement by configuration drift.**  
   Una config incompleta, legacy o ambigua non deve mai riuscire ad attivare
   il live.

---

## 7. Test e validation matrix

La remediation non puo' considerarsi chiusa senza questa copertura minima.

| Area | Test richiesti |
| --- | --- |
| Order ID | multi-order same session, reconnect, concurrent reservations |
| Circuit breaker | rolling window cross-scope, cooldown, restart visibility |
| Entry flow | all legs created, one leg rejected, duplicate entry prevention |
| Exit flow | time exit single-trigger, transient close failure, no double close |
| PnL / thresholds | premium reale, target raggiungibile, stop loss corretto |
| Balance safety | unavailable, stale, fresh-above-threshold, fresh-below-threshold |
| Broker callbacks | duplicate callback, out-of-order callback, partial fill, full fill |
| Restart recovery | crash after submit, crash before persist final state, reconcile on boot |
| Outbox | malformed payload, permanent failure visibility, no false `sent` |
| Greeks alerts | persistent breach cooldown, state-change re-alert, outbox volume bounded |

**Paper validation minima obbligatoria prima del live:**

1. almeno una entry multi-leg completa;
2. almeno un partial fill gestito correttamente;
3. almeno una rejection gestita senza doppio ordine;
4. almeno un riavvio servizio con ordini in-flight;
5. almeno un test di stop-loss/profit-target basato su dati reali;
6. almeno un test di circuit breaker che blocca scope successive;
7. almeno un test di stale balance che rifiuta l'ordine.

---

## 8. Go-live release gates aggiornati

Il live puo' essere preso in considerazione solo se tutti i seguenti punti
sono verdi:

- tutti i finding `COP-01` ... `COP-09` sono chiusi o formalmente accettati
  con risk sign-off scritto;
- nessun path live contiene stub o TODO bloccanti;
- esiste evidenza paper per entry, close, rejection, partial fill, restart,
  reconnect e stale-data handling;
- esiste reconciliation report broker-vs-DB senza mismatch aperti;
- il circuit breaker e' verificato cross-scope e cross-restart;
- outbox corruption e alert storm hanno metriche/alert dedicati;
- i threshold economici usano premium/fill reali;
- l'operatore puo' ricostruire end-to-end un ordine dal log e dal DB.

Se anche uno solo di questi punti e' rosso, il sistema resta **paper-only**.

---

## 9. Recommended implementation order

Ordine suggerito per minimizzare il rischio:

1. `COP-01` order id
2. `COP-04` account balance freshness
3. `COP-02` circuit breaker condiviso
4. `COP-05` pending entry + recovery
5. `COP-06` pending exit + idempotent close
6. `COP-03` rimozione stub entry/exit/PnL
7. callback persistence e reconciliation completa
8. `COP-07` outbox integrity
9. `COP-08` Greeks dedupe
10. `COP-09` economic thresholds

Questo ordine riduce prima il rischio di ordini sbagliati o duplicati, poi
chiude i gap di controllo e audit.

---

## 10. Conclusion

La review Copilot conferma che il sistema ha gia' una buona impostazione
architetturale, ma il tratto piu' delicato - cioe' la catena
`decisione -> ordine -> callback broker -> stato persistito -> exit/risk` -
non ha ancora tutte le garanzie richieste da un software enterprise che
muove capitale reale.

La priorita' non e' aggiungere nuove feature, ma chiudere gli invarianti
operativi: unicita' ordine, idempotenza, persistenza degli eventi reali,
fail-closed, recovery e correttezza economica.

Finche' questi punti non sono dimostrati con test e paper validation, il
sistema deve restare **strictly paper-only**.
