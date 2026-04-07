# Add Windows Defender Exclusions Manually

Se lo script PowerShell non funziona (policy aziendali), aggiungi manualmente:

## Passo 1: Apri Windows Security

1. Premi `Win + I` per aprire Impostazioni
2. Vai a **Privacy e sicurezza** → **Sicurezza di Windows**
3. Clicca **Protezione da virus e minacce**
4. Scorri in basso e clicca **Gestisci impostazioni** sotto "Impostazioni di Protezione da virus e minacce"
5. Scorri fino a **Esclusioni** e clicca **Aggiungi o rimuovi esclusioni**

## Passo 2: Aggiungi queste cartelle

Clicca **Aggiungi un'esclusione** → **Cartella** e aggiungi:

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\tests\SharedKernel.Tests\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\tests\TradingSupervisorService.Tests\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\tests\OptionsExecutionService.Tests\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\src\SharedKernel\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\src\TradingSupervisorService\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\src\OptionsExecutionService\bin
```

```
C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\src\IBApi.Stub\bin
```

## Passo 3: Ricompila e testa

Dopo aver aggiunto le esclusioni:

```powershell
cd "C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system"
dotnet clean
dotnet build
dotnet test --no-build
```

## Se Windows Security è disabilitato da policy aziendale

Se non riesci ad accedere alle esclusioni, **contatta l'IT aziendale** e richiedi l'aggiunta delle cartelle sopra alle esclusioni Windows Defender.

Alternativa: chiedi all'IT di disabilitare completamente Windows Defender Application Control per la cartella di sviluppo.
