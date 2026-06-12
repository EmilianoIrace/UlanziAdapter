# Architecture

## Brainstorming

Il D100H puo essere trattato in due modi:

1. Remapper user-mode: intercetta gli input standard visti da Windows e invia nuove scorciatoie. E distribuibile come EXE semplice, ma la soppressione degli input originali non e perfettamente per-dispositivo.
2. Provider HID proprietario: parla direttamente con il dispositivo in online mode. E l'approccio migliore a lungo termine, ma richiede reverse engineering del protocollo Ulanzi.
3. Driver/filter kernel: puo bloccare input per device in modo corretto, ma richiede installazione, firma driver e privilegi/policy aziendali compatibili.

La versione iniziale sceglie il punto 1 per massimizzare usabilita su PC di lavoro bloccati.

## Cartelle

```text
src/UlanziAdapter.Core
  Configuration/    Modello JSON, loader e validazione
  Input/            Eventi normalizzati, sorgenti input, gesture
  Mapping/          BindingEngine, layer e debounce
  Actions/          Risultato esecuzione binding

src/UlanziAdapter.Windows
  Input/            Hook tastiera low-level e virtual-key names
  Output/           SendInput keyboard/text output
  Startup/          Registrazione HKCU Run
  Storage/          Settings utente in AppData
  Native/           P/Invoke Win32

src/UlanziAdapter.App
  UI WinForms minimale, tray icon, caricamento config

config/
  d100h.sample.json

docs/
  ARCHITECTURE.md
  MEMORY.md
```

## Flusso runtime

```text
KeyboardHookInputSource
  -> InputEvent normalizzato
  -> BindingEngine.Handle(...)
  -> BindingExecution
  -> SendInputKeyboardOutput
```

`BindingEngine` non dipende da Windows. Questo permette di aggiungere in futuro un input source HID senza riscrivere config, layer e azioni.

Quando una sorgente e una combinazione come `Ctrl+C`, l'app rilascia prima i modificatori sorgente via `SendInput` e poi invia l'azione configurata. Questo evita che il `Ctrl` fisico del D100H contamini l'output rimappato.

## JSON

`bindings` e organizzato per layer. Ogni binding ha:

- `source`: input visto dal D100H, per esempio `VolumeUp` o `Ctrl+C`.
- `send`: chord da inviare, per esempio `Ctrl+Shift+Z`.
- `text`: testo Unicode da scrivere.
- `layer`: azione opzionale `switch`, `toggle` o `momentary`.

Esempio:

```json
{
  "source": "VolumeMute",
  "layer": {
    "mode": "toggle",
    "target": "knobPressed",
    "fallback": "default"
  }
}
```

## Limite tecnico importante

Il low-level keyboard hook non espone l'handle del dispositivo fisico che ha generato il tasto. Quindi `suppressOriginalInput=true` sopprime l'evento in base al gesto, non in base al device. Questo e accettabile per tasti media/volume dedicati, ma puo interferire con combinazioni comuni come `Ctrl+C` se vengono premute da una tastiera normale mentre l'app e attiva.

Mitigazioni future:

- schermata capture per confermare quali sorgenti sono effettivamente usate;
- allowlist piu restrittiva;
- provider Raw Input diagnostico per identificare VID/PID;
- provider HID proprietario, se il protocollo Ulanzi viene documentato o reverse-engineerato.
