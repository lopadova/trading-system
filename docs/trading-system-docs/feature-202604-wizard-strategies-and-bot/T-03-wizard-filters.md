# T-SW-03 — UI Shell + Design System + Step Indicator + Componenti Base

## Obiettivo
Implementare il layout shell del wizard con tema dark amber, StepIndicator
animato, e tutti i componenti riutilizzabili (FieldWithTooltip, DeltaSlider,
ImportDropzone, ValidationBadge, NavigationButtons). Questi componenti vengono
usati da tutti gli step successivi.

## Dipendenze
- T-SW-01 (tipi)
- T-SW-02 (store — per NavigationButtons e ValidationBadge)

## Files da Creare
- `dashboard/src/components/strategy-wizard/WizardContainer.tsx`
- `dashboard/src/components/strategy-wizard/StepIndicator.tsx`
- `dashboard/src/components/strategy-wizard/shared/FieldWithTooltip.tsx`
- `dashboard/src/components/strategy-wizard/shared/DeltaSlider.tsx`
- `dashboard/src/components/strategy-wizard/shared/ValidationBadge.tsx`
- `dashboard/src/components/strategy-wizard/shared/ImportDropzone.tsx`
- `dashboard/src/components/strategy-wizard/shared/NavigationButtons.tsx`
- `dashboard/src/components/strategy-wizard/shared/JSONPreview.tsx`
- `dashboard/src/styles/wizard.css`
- `dashboard/src/pages/trading/strategies/StrategyWizardPage.tsx`

## Files da Modificare
- `dashboard/src/App.tsx` o router — registrare StrategyWizardPage nella route

## Implementazione

### wizard.css — Token di design

```css
.wizard-root {
  --wz-bg:           #0a0a0f;
  --wz-surface:      #12121a;
  --wz-elevated:     #1a1a26;
  --wz-border:       #1e2433;
  --wz-border-focus: rgba(245, 158, 11, 0.4);
  --wz-amber:        #f59e0b;
  --wz-amber-dim:    rgba(245, 158, 11, 0.12);
  --wz-success:      #10b981;
  --wz-warning:      #f97316;
  --wz-error:        #ef4444;
  --wz-info:         #6366f1;
  --wz-text:         #f1f5f9;
  --wz-muted:        #64748b;
  --font-display:    'Space Mono', monospace;
  --font-body:       'DM Sans', sans-serif;
  --font-mono:       'JetBrains Mono', monospace;
}
```

Font da caricare (Google Fonts o self-hosted):
- Space Mono (display, titoli step)
- DM Sans (body, label, descrizioni)
- JetBrains Mono (numeri, JSON preview)

### WizardContainer.tsx — Layout

Layout **3 colonne su desktop**:
- Sinistra (240px): `StepIndicator` (collassabile su mobile)
- Centro (flex-1): Area contenuto step con `AnimatePresence` (Motion)
- Destra (280px): `HelpPanel` contestuale (mostra tooltip del campo attivo)

Layout **mobile** (< 768px): stack verticale — StepIndicator (barra orizzontale) + contenuto + (HelpPanel come drawer)

Transizione step: `slide + fade` con `AnimatePresence` di framer-motion/motion.
- Uscita: traslazione -30px + fade out verso sinistra
- Entrata: traslazione +30px → 0 + fade in da destra

### StepIndicator.tsx

```
Step list verticale con 10 item. Ogni item:
- Cerchio con numero
- Nome step (Space Mono)
- Linea connettore tra step

Stato cerchio:
- pending:  bordo #1e2433, sfondo trasparente, numero grigio
- active:   bordo amber, sfondo amber-dim, numero bianco, box-shadow amber glow
            + animazione pulse (keyframe CSS)
- done:     sfondo verde, ✓ bianco, bordo verde
- error:    sfondo rosso, ✗ bianco, bordo rosso
- clickable se: stato === 'done' o step === currentStep
```

Definizione STEPS:
```typescript
const STEPS = [
  { n: 1,  label: 'Identità' },
  { n: 2,  label: 'Strumento' },
  { n: 3,  label: 'Filtri Ingresso' },
  { n: 4,  label: 'Regole Campagna' },
  { n: 5,  label: 'Struttura Legs' },
  { n: 6,  label: 'Filtri Selezione' },
  { n: 7,  label: 'Regole Uscita' },
  { n: 8,  label: 'Esecuzione' },
  { n: 9,  label: 'Monitoring' },
  { n: 10, label: 'Review & Publish' },
]
```

### FieldWithTooltip.tsx

```typescript
interface FieldWithTooltipProps {
  label: string
  required?: boolean
  tooltip: { description: string; example?: string; warning?: string }
  error?: string
  warning?: string
  children: React.ReactNode
}
// Rendering: label (+ asterisco se required) | ? icon → Popover | campo | errore rosso
```

### DeltaSlider.tsx

Slider custom con:
- Range 0.01–0.99 (step 0.001)
- Track colorato: verde (≤ 0.20) → giallo (0.20–0.50) → rosso (> 0.50)
- Marker fissi: 0.05, 0.16, 0.30, 0.50, 0.68, 0.85
- Label marker: "Deep OTM" / "OTM" / "Neutral" / "ATM" / "ITM"
- Thumb amber + input numerico sincronizzato a destra
- Clamp automatico a [0.01, 0.99]

### ImportDropzone.tsx

Drop zone con:
- Bordo tratteggiato amber, sfondo amber-dim
- Icona upload (bounce su hover)
- Testo: "Trascina il file JSON qui oppure clicca per scegliere"
- Accept: `.json` only
- On drop: legge testo → chiama `wizardStore.initFromJson()`
- Feedback: spinner → success (verde) / error (rosso con messaggio)

### NavigationButtons.tsx

Footer fisso con:
- Sinistra: `← Indietro` (grigio, if step > 1)
- Centro: `Step N di 10` (Space Mono)
- Destra: `Avanti →` (amber, if step < 10) | `🚀 Review & Pubblica` (step 10)
- Progress bar sottile amber (animata) sotto il footer
- Se `nextStep()` → false: animazione shake sul pulsante Avanti

### JSONPreview.tsx

- Syntax highlighting con `prism-react-renderer` (tema atomDark)
- JSON prettificato con 2 spazi
- Pulsante `📋 Copia` → clipboard + feedback "Copiato!"
- Pulsante `📥 Download` → Blob + URL.createObjectURL
- Altezza max 500px con overflow-y: scroll
- Linee numerate (CSS counter)

## Test

- `TEST-SW-03-01`: `StepIndicator` con currentStep=3 → step 1,2 classe 'done', 3 classe 'active'
- `TEST-SW-03-02`: click step done → goToStep chiamato con numero corretto
- `TEST-SW-03-03`: click step non visitato → goToStep NON chiamato
- `TEST-SW-03-04`: `DeltaSlider` value=0.30 → track verde fino a marker 0.30
- `TEST-SW-03-05`: `DeltaSlider` onChange chiamato con valore corretto al drag
- `TEST-SW-03-06`: `ImportDropzone` file .json valido → `initFromJson` chiamato
- `TEST-SW-03-07`: `ImportDropzone` file .txt → messaggio errore "Formato non supportato"
- `TEST-SW-03-08`: `FieldWithTooltip` con `error="Errore test"` → testo rosso visibile
- `TEST-SW-03-09`: `ValidationBadge` con 2 errori → mostra "✗ 2"
- `TEST-SW-03-10`: `NavigationButtons` — nextStep false → classe CSS 'shake' sul bottone

## Done Criteria
- [ ] Build pulito
- [ ] Tutti i test TEST-SW-03-XX passano
- [ ] Font Space Mono, DM Sans, JetBrains Mono caricati (verifica in browser)
- [ ] Tema dark amber coerente — nessun colore hardcoded fuori da CSS variables
- [ ] Layout 3 colonne su desktop (≥ 1024px)
- [ ] Layout stack su mobile (< 768px)
- [ ] Transizione step: nessun flash/jank visibile
- [ ] `JSONPreview` copia in clipboard funzionante

## Stima
~2 giorni
