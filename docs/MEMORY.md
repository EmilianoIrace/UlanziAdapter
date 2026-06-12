# Project Memory

## Obiettivo

Creare un EXE Windows minimale per Ulanzi Studio D100H che carichi un JSON e rimappi i 7 tasti piu la ghiera. Deve poter partire allo startup senza installare il software Ulanzi ufficiale.

## Decisioni prese

- Tecnologia: C#/.NET 8 WinForms.
- Distribuzione: publish self-contained single-file per `win-x64`.
- Nessun privilegio admin richiesto.
- Startup: registro utente `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Input iniziale: hook tastiera low-level.
- Output: Win32 `SendInput`.
- Config: JSON versionato, copiato in `%AppData%\UlanziAdapter\d100h.json` al primo avvio.
- Architettura aperta: `IInputSource` permette un futuro provider HID.

## Mapping D100H assunto

Fonte pratica trovata pubblicamente su GitHub/input-remapper:

```text
Dial Clockwise       -> VolumeUp
Dial Anti-clockwise  -> VolumeDown
Dial Press           -> VolumeMute
Top Left             -> MediaPreviousTrack
Top Middle           -> MediaPlayPause
Top Right            -> MediaNextTrack
Side Left Top        -> Ctrl+V
Side Left Bottom     -> Ctrl+C
Side Right Top       -> Ctrl+Y
Side Right Bottom    -> Ctrl+Z
```

## Rischi aperti

- Non verificato su Windows con D100H fisico.
- macOS locale non ha .NET SDK installato, quindi la build non e stata eseguita qui.
- La soppressione input e per gesto, non per dispositivo. Questo puo confliggere con tastiera normale per sorgenti tipo `Ctrl+C`.
- Se il D100H su Windows usa HID consumer reports non convertiti in virtual keys, servira aggiungere Raw Input/HID.

## Prossimi passi consigliati

1. Compilare su Windows con .NET 8.
2. Avviare app e premere ogni tasto D100H verificando il log.
3. Se i tasti laterali interferiscono con tastiera normale, impostare `suppressOriginalInput=false` oppure cambiare sorgenti/output.
4. Aggiungere capture wizard:
   - modalita ascolto prossimo input;
   - scrittura automatica del campo `source`;
   - esportazione config.
5. Aggiungere provider HID:
   - enumerazione HID devices;
   - filtro VID/PID;
   - parsing report;
   - opzione di soppressione precisa solo se il device non emette gia input tastiera standard.
