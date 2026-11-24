# Ash & Iron Unity / Photon Quantum 3

Dato: 24. november 2026. Reference: fantasy-rts `main` ved `6456e9a94f5966bcea0b266da9a1c4bcae0616fa` ([kilde](https://github.com/TmnLuckyDev/fantasy-rts/tree/6456e9a94f5966bcea0b266da9a1c4bcae0616fa)). Den eksisterende kode og Render er intakte. Ingen Photon-app, betaling eller release er oprettet. Målet er fuld funktionel paritet, ny stiliseret 3D-grafik og native mobilbuilds i et separat projekt.

## Sådan bruger du pakken

1. Læs `Docs/01-installation.md` og opret et separat Unity 6.3 LTS-projekt med Universal 3D og Quantum 3.1.0 (build 2313).
2. Importér SDK og Quantum User Files. Kopiér derefter pakkens `Assets/QuantumUser/Simulation/AshAndIron` til samme placering i det nye projekt. Behold SDK'ets genererede assembly-konfiguration.
3. Kør QTN CodeGen, ret eventuelle versionsspecifikke diagnostikfejl, og kontrollér kompilering. De seks QTN-filer er skrevet ud fra dokumenteret DSL, men **ikke valideret i CodeGen**. Unity, SDK og C#-toolchain er ikke tilgængelige i dette miljø.
4. Brug `Reference/data/rules.json` og `maps.json.gz` som autoritativt migreringsinput. De er maskinelt eksporteret fra det faktiske TypeScript-modul ved ovenstående commit. Kortdata er komprimeret tabsfrit. `AaiRulesAsset.cs` og `AaiMapAsset.cs` er de tilhørende asset-skemaer. `node Reference/compile-assets.mjs --check` verificerer de genererede editor-inputs. En importør og første simulationssystemer er skrevet, men endnu ikke kompileret med SDK. Se `Docs/05-cloud-development.md`.
5. Læs `Docs/02-parity.md`, `Docs/03-architecture.md` og `Docs/04-roadmap-validation.md` før systemimplementering. `Examples/` indeholder to afgrænsede C#-eksempler. De ligger uden for Assets, da de kræver et registreret kommando-system, og er ikke kompileret eller kørende.

## Leverance og status

| Område | Status |
|---|---|
| Kortlagt TypeScript-version | Nyeste main og 160/160 tests bestået; TypeScript-check bestået |
| Dataeksport | 115 race/entity-kombinationer, 65 træningsregler, 31 researchtrin, 14 abilities, seks kort og 36 seed/mode-fixtures |
| QTN | Seks filer med grundlæggende spillere, match, units, økonomi, heroes, grupper, fog, flåde og AI |
| C# asset-skemaer | Typed regler og kort samt Unity-editorimportør, ikke kørt i editoren |
| Simulationsprototype | Opening spawner, atomisk ressource/population-reservation, move/stop-kommando og terrænbegrænset direkte bevægelse skrevet, ikke kompileret |
| Tests | Node-referencedata bestået, Quantum EditMode-tests skrevet men ikke kørt |
| Kommando / view | Runtime-systemer kræver SDK-registrering, view og input er ikke aktive endnu |
| Benchmark | 18 TypeScript-scenarier i `Reference/baseline.jsonl` |
| Unity CodeGen, kompilation, scene, lokal Quantum-kamp | Afventer installation af Unity og Quantum 3.1.0 |
| Native Quantum builds, multiplayer, WebGL | Afventer prototype og tests |

Dette er et **Unity-kildeprojekt under udvikling**, med låst editorversion og package manifest, men uden SDK, fulde editor-genererede ProjectSettings, `.meta`-filer og scener. Det er ikke en spilbar build. Det eksisterende spil skal fortsat anvendes indtil migrationsgates er opfyldt. Commit aldrig licenser, Photon-indstillinger, licenserede SDK-filer eller signeringsnøgler i dette repository.

### Genskab dataeksporten

Fra den udpakkede pakke med Node 24 (eller nyere kompatibel Node 22 med type stripping):

```sh
node Reference/export-reference.mjs /sti/til/fantasy-rts Reference/data
node Reference/compile-assets.mjs
node Reference/validate-package.mjs
node Reference/compile-assets.mjs --check
```

Eksportøren læser kilden uden at skrive til repositoryet. Den verificerer beregnede træningsregler og research, enhedskombinationer, terrænstørrelse og spejling. `manifest.json` indeholder commit og SHA-256 til de to datafiler. Ved re-eksport fra en anden revision skal alle forskelle reviewes; matchversionshash skal afspejle faktisk Unity asset database og systems.
