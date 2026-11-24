# 04. Migreringsplan, succeskriterier og tests

## Faser og afgrænsning

| Fase | Leverance | Exit gate |
|---|---|---|
| 1a (denne leverance) | Baseline-kortlægning, SDK/Unity-version, seks QTN-filer, immutable asset-skemaer, eksport af actual race/settings/map data, installationsguide og arkitektur. | Dataeksport er reproducerbar, kilden uændret, skemaer til CodeGen klar. Ingen rapport om gennemført Quantum-kompilering. |
| 1b teknisk prototype | Opret separat Unity projekt, importér SDK, kør CodeGen, compiler og tests. Importér rules/maps, spawn 2 sides med samme startopsætning, en lokal scene og to online klienter. | Alle gates nedenfor P1–P6. Ingen betalt hosting. |
| 2 RTS core | `CommandSystem`, `MovementSystem`, formationer, navigation, combat, worker/ressource, konstruktion, global production og pop. Først ren deterministic test, så view. | Reproducerbare inputfixtures for bevægelse, kamp, workers, build/cancel and train; exact resource/HP/duration parity. |
| 3 Gameplay parity | Fem racer, abilities, HQ, research, hero XP, army groups, fog, naval, Classic, Control Points, map settings, settlements, practice og frontend QoL. | Alle reference-feature rows og 15 racepar inkl. mirror matches, alle 6 kort, test af finite/unlimited ressources og caps. |
| 4 AI / multiplayer / save | Port nuværende AI først, derefter separat AI overhaul. Public/private matchmaking, rooms, reconnect, local pause/resume, robust save, version gate og resultatvalidering. | Langtidssim 30–60 min, diverse strategier og seeds, økonomi-/recovery-test, reconnect 45/90s, replay/verified checksum og korrupt save bevaret. |
| 5 Grafik og mobile | URP stil, raceidentitet, animationer, minimap, touch, safe area, adaptive kvalitet, native builds, WebGL. | Fysisk Android/iOS, realistiske touchprofiler, render/frame pacing og simprofil op til 500 pop/player; browserretning dokumenteret. |
| 6 Release readiness | Sammenlign scripts og gameplay, regressioner, replay, sikkerhed, netværks- og strømtest, platformssignering, hostingbudget, QA og optimeringer. | Kriterier accepteret af ejeren; klar go/no-go for erstatning af gammel version. Original deployment ændres kun efter særskilt godkendelse. |

## Målbare tekniske prototype-gates

P1. Quantum 3.1.0 CodeGen uden fejl på alle seks QTN-filer og begge C# assets; Unity compilation uden fejl. Commit eksakt Unity patch, SDK build, schema og genererede filer ifølge SDK-projektets anbefalede VCS-regler.

P2. Asset-import matcher 115 entityrækker, 65 training rows, 31 research steps, 8 wall/tower upgrade rows, 6 maps og 36 fixed-seed/mode fixtures. Alle priser og heltalsstats skal være identiske efter roundtrip. FP-raw roundtrip inden for 1/65536 legacy units af den fastlagte quantization. Indlæs alle base grids; check tile count, symmetry, startpositions og settings count. Validation af references og samme contenthash på begge klienter.

P3. Lokal start uden netværk: to seats, med 9 units/HQ hver, korrekt race, 300 G / 180 W / 120 S, 9 pop af 10 capacity. Spawn 100/250/500/1.000 entities, slet/destroy og load snapshot. 1.000 testticks med faste seeds og scripted move-kommando (minimal system i næste fase). Ingen exceptions, leak eller ugyldige refs. Command-accept, invalid owner og out-of-bounds test. Replay fra samme verified snapshot og input har samme checksums.

P4. To uafhængige clients (Desktop+Desktop først) 10-minutters session med 10 seeds. Ingen desync. Samme session/game/config hash og verified checksum hver 60 ticks. Simuler normale forsinkelser og kort forbindelsesfald, test 50/100/200 ms RTT og kort suspension, mål rollback længde og CPU. Test to modstridende kommandoer i samme tick i stabil orden. Fejl i én clients view må ikke ændre game state. iOS/Android build smoke når toolchain klar.

P5. Baseline 20 Hz med 50 ms budget pr. step. Prototype mål på target phone: for 1.000 levende land units (ikke nødvendigvis samme pop) p95 < 20 ms, p99 < 40 ms uden rollback, ved typisk 2–4 rollback steps samlet under 50 ms p95 eller demonstreret throttling/visuel degradering uden stateændring. Hvis ikke opfyldt, optimer before 30 Hz. 30 Hz har 33,3 ms budget og må kun vælges når duration conversions og tests viser uændret gameplay. På fysisk midrange phone 30 FPS minimum under store kampe, helst 60 i små kampe, touch feedback p95 < 100 ms, og ingen memory growth >10% efter warmup ved gentagne 30-min sessions. Dette er mål, ikke målte resultater.

P6. Resultater fra Android ARM64, iOS ARM64 og WebGL desktop med samme inputs/checksums og deterministiske data. WebGL mobile test separat. Sammenlign faktisk netværk bytes/s, CPU/GPU, resident memory, rollback, FPS, frame pacing, touch, strøm og thermal throttling for 10/30 min. Hardware, OS, build profile, map, race og seeds dokumenteres. Ikke erstat gammel version hvis native eller browser er ubrugelig. 2.000/4.000 er stresstest, ikke garantier eller krav om 4.000 på alle telefoner.

## Reproducerbar referencebenchmark (TypeScript)

Kørt på Linux arbejdsrunner, Node 24.22.3, mod `6456e9a`, 120 ticks pr. scenarie, Human/Orc, 100–4.000 units samlet (scenariet spawner 50% per side). Hære er sammensat af ranged/blade/heavy og overskrider bevidst normal population i stressscenarier. `move`, `combat`, `bridge`, samme seed/layout og input for samtlige future Quantum-runs. Baseline inkluderer simulation + to visibility views + JSON snapshots på hvert andet tick, er derfor ikke en ren sim-tick eller faktisk netværkstrafik. Højere 4000 tal illustrerer flaskehalsene. Resultatet kan ikke bruges til at love fysisk enheds-FPS.

| Units samlet | Move p95 ms | Combat p95 ms | Bridge p95 ms | Højeste single tick ms | Højeste målt JS heap MiB |
|---:|---:|---:|---:|---:|---:|
| 100 | 1,72 | 2,03 | 1,87 | 12,02 | 22 |
| 250 | 6,57 | 2,92 | 4,36 | 25,64 | 26 |
| 500 | 13,06 | 5,81 | 12,78 | 48,06 | 41 |
| 1.000 | 24,24 | 10,30 | 31,13 | 55,85 | 66 |
| 2.000 | 53,96 | 23,69 | 88,59 | 111,59 | 109 |
| 4.000 | 101,48 | 70,78 | 335,99 | 386,26 | 141 |

Kommando fra fantasy-rts ved referencecommit:

```sh
BENCH_COUNTS=100,250,500,1000,2000,4000 BENCH_CASES=move,combat,bridge BENCH_TICKS=120 node --expose-gc scripts/benchmark-armies.mjs
```

`Reference/baseline.jsonl` indeholder alle 18 rå målinger, p50/p95/max, CPU, JS heap, phase timings og fulde snapshot-JSON bytes. Baseline registrerer ikke p99; lav fremover rå tick timings og beregn p99. Benchmark med 3+ gentagelser og 10.000+ ticks, varme caches, identisk hardware og release mode. Skeln mellem engine ticks, rollback ticks og client rendering fra én samlet frame. Mål både 500 pop/player i en realistisk army og 500 faktiske units/player, eftersom heavy/siege/warship bruger 2 pop.

På reference TS er 1.000 units close to budget i enkelte spikes, 2.000 især bridge overskrider 50 ms ved p95, 4.000 bridge er uegnet i nuværende scenario. Quantum kan reducere network snapshot-arbejde, men flytter full simulation/rollback til telefonen og rendering til 3D. Det skal måles, før vi påstår en forbedring.

## Testmatrix

| Type | Nuværende reference | Quantum-krav |
|---|---|---|
| Data / rules | `npm run check`, `npm test` 160/160 bestået 24.11.2026, exporter asserts + SHA | QTN CodeGen, C# EditMode/FrameEmulator, fixed point & roundtrip tests |
| Economy | `economy`, `idea-bank`, `pop-cap-dismiss-deposits`, `upgrade-tiers`, `production`, `global-production` tests | Exact health/bank/pop/cost/tick counters ved hver operation, cancel/refund/retry, 0/max resource count, progression off/on |
| Combat | `match`, `army-regression`, `formations`, `balance`, `heroes`, `hero-abilities`, `siege-races` | Target lost/fog, defense leash, move+fire, halt, formation, hero death/spawn/retained XP, race-specific skills, missile after shooter death |
| Fog/maps | `scouting-placement`, `scout-exploration`, `battlefields`, `naval-islands`, `control-match` | Hidden/explored/visible masks, overlapping stamps, reveal, depletion, map seed, ship boarding/unload, draw/control/timeout |
| AI | `computer`, `balance-races.mjs`, `ai-league.mjs` | Current decision parity, then separate 30/60-minute new AI experiments, 15 pairings, all 6 maps, seeded races and skills, no hidden info used |
| Online | `socket`, `network-lifecycle`, `snapshot-delta` | Local SessionRunner deterministic replay, FrameEmulator, checksum across 2 clients, session restore, timeout/rejoin and protocol version gating |
| Mobile | `battle`, `battle-qol`, `custom-match`, `army-groups`, `offline-mobile`, `massive`, `races` browser suite, emulator workflows | UI gesture tests, native devices, audio+safe areas+background, selection, orders, panel dismissal, production, fog/range, group bar and 500 vs 500 |
| Saves | `resume`, `socket`, `offline-mobile` | Save envelope version validation, load runner snapshot with matching asset hash; crash/restore; unknown or corrupt save preserved; legacy save remains available in original app |

Current environment: no Unity, SDK, .NET, Xcode or usable Unity license. No Quantum CodeGen, C# compile, prototype session, native Unity build, online game, GPU/FPS/battery or physical-device check is claimed. Existing Capacitor workflow results are separate from Unity. The TypeScript browser/deploy was preserved and is still the gameplay reference.

## Decision points and risks

1. Fog confidentiality is the top architecture risk: for casual Quantum use visual fog plus deterministic validation; for competitive, decide whether complete state on clients is acceptable, or retain authoritative filtered server architecture. This decision may determine whether Quantum replaces the old engine at all.
2. Deterministic numerical conversion and update order can alter combat outcomes. Freeze asset values, 20 Hz, ties and fixtures before moving to 30 Hz or systems parallelism. Compare exact discrete events and movement tolerance.
3. Performance shift: Node runs authoritative state now, Unity/Quantum moves work to mobiles. Rendering and rollback could exceed frame budget. Establish device metrics before claiming advantage.
4. Match persistence: Public Cloud reconnect is not permanent room backup. Save schema and recovery are deliberate protocols. Keep backups.
5. SDK/license/hosting: 3.1 new API, verify codegen and supported packages. Use 20 CCU dev, evaluate free launch eligibility. No paid plan or production changes without approval.
6. Feature completeness: phase 1 code schema/fixtures only. Port all rows of parity inventory before new gameplay, AI overhaul or release. Existing roadmap's advanced features are tracked as planned.

## References (official)

- [Quantum 3.1 SDK and Unity support](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup), [3.1 release notes](https://doc.photonengine.com/quantum/v3/getting-started/release-notes/release-notes-3.1), [3.1 migration notes](https://doc.photonengine.com/quantum/v3/getting-started/migration-guide/migration-guide-3.1).
- [Unity LTS](https://unity.com/releases/unity-6/support), [Unity Web 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html).
- [Quantum project](https://doc.photonengine.com/quantum/v3/manual/quantum-project), [DSL](https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/dsl), [FP](https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/fixed-point), [Commands](https://doc.photonengine.com/quantum/v3/manual/commands).
- [Assets](https://doc.photonengine.com/quantum/v3/manual/assets/assets-simulation), [Entity View](https://doc.photonengine.com/quantum/v3/manual/entity-view-component), [Entity Prototypes](https://doc.photonengine.com/quantum/v3/manual/entity-prototypes), [Navigation](https://doc.photonengine.com/quantum/v3/manual/navigation/workflow-agents), [Flow Fields](https://doc.photonengine.com/quantum/v3/addons/flow-fields/navigation).
- [Testing and desync](https://doc.photonengine.com/quantum/v3/manual/testing), [Starting from snapshot](https://doc.photonengine.com/quantum/v3/manual/game-session/starting-from-snapshot), [WebGL](https://doc.photonengine.com/quantum/v3/manual/webgl), [Cheat protection](https://doc.photonengine.com/quantum/v3/manual/cheat-protection), [Pricing](https://www.photonengine.com/quantum/pricing).
