# 03. Architecture decision og deterministic simulation

## Anbefalet projektlayout

Opret et særskilt Unity-projekt. Tilføj disse undermapper i den struktur, Quantum Hub selv opretter. SDK's `Quantum.Simulation`-assembly skal indeholde al gameplay. Behold SDK's egne asmdefs og `Simulation/Generated` samt `View/Generated`.

```text
Assets/
  Photon/                         # SDK, ingen egen gameplay
  QuantumUser/
    Simulation/
      Generated/                  # QTN CodeGen, ikke håndredigeret
      AshAndIron/
        DSL/                      # De seks medfølgende .qtn-filer
        Rules/                    # AaiRulesAsset, AaiMapAsset
        Commands/                 # Diskrete spillerkommandoer
        Match/                    # Player, match lifecycle, score
        Movement/                 # Formation, steering
        Navigation/               # Grid, spatial index, path budget
        Combat/                   # Targeting, damage, projectiles
        Economy/                  # Worker/resource, population
        Construction/             # Placement, repair, building levels
        Production/               # Queues, research, cancellation
        Heroes/                   # XP, abilities, corpses/summons
        ArmyGroups/               # Templates, assignment, replenish
        FogOfWar/                 # Grid + visibility tests
        Naval/                    # Water routes, pickup, unload
        AI/                       # Decision state, current rules
        Save/                     # Deterministic state/version contract
      RuntimeConfig.User.cs       # SDK-generated user extension files, use actual installed names
      RuntimePlayer.User.cs
      CommandSetup.User.cs
      SystemSetup.User.cs
    Resources/AshAndIron/          # Imported readonly Quantum .asset instances
    View/
      Generated/
      AshAndIron/
        Input/ Camera/ Entities/ UI/ Audio/ Effects/ Minimap/ FogOfWar/
  AshAndIron/
    Art/ Models/ Materials/ Animations/ Prefabs/ Scenes/
    Editor/                       # JSON -> Quantum asset importer, validation
    Tests/                        # Edit/Play and deterministic fixtures
Reference/                        # Not in Assets; export and benchmarks
Docs/
Packages/
ProjectSettings/
```

Quantum's ECS is distinct from Unity Entities/DOTS. For an outside View asmdef, explicitly reference the SDK's `Quantum.Unity`, `Quantum.Simulation` and relevant Unity packages. Simulation does not reference View, UnityEngine APIs, PhysX, Time, GameObjects or MonoBehaviours. AssetObject is a Quantum type which happens to be a ScriptableObject in Unity; no mutable Unity object in the simulation. [Quantum project structure](https://doc.photonengine.com/quantum/v3/manual/quantum-project).

## Components, static data and frame memory

| Definition | Ownership / use |
|---|---|
| `AaiPlayer` | One entity per seat, PlayerRef for actual human input; `IsComputer` and Seat determine AI ownership. Race/team separate. Research array in enum order; retained hero XP ordered Vanguard/Pathfinder. Counts/bank and active/queued hero checks validated centrally. |
| `AaiMatch` | One entity held by `AaiMatchEntity` global, two player refs sorted One/Two, stable config, source seed, time, result and winner. Draw = finished reason plus None winner. Simulation does not consult wall clock. |
| Identity/Health/Combat/Movement | Shared identity, current health and runtime modifiers. Attach movement only to mobile entities, combat only to damage-capable units/towers. Transform2D position is the one authority. Static HP/damage/armor/range/speed/cooldown/radius from immutable asset by race/kind; only derived overrides stored when justified. |
| `AaiPath` and `AaiQueuedOrders` | Owned collections attached when needed. Path is current route (legacy implementation); queued player waypoints are future functionality. No pointer cached outside Frame. |
| `AaiWorker` and construction | Worker job, cargo, tick progress, target refs, ordered building sites. Idle means no job/active order. Queued foundations are real entities; cancellation/destruction releases owners and resumes next. |
| Building / production / research | Construction status, queue and revision on training buildings, independent research/structure jobs, rally point. Atomic bank/pop changes with stored payment and refund. HQ/farm contribution only on completion. |
| Hero/ability/corpse/projectile | Cooldowns and expiration in tick integers; no Unity timer. Projectile entity stores target ref, source info and last source state. At impact resolve live source if available, update fallback upon destruction (old attacker object survived in pending missile) and calculate damage from current player research and aura just as source. Dead attacker XP handling must be covered by fixtures. |
| ArmyGroup/GroupMember | Member list is canonical for iteration; per-unit group ref is reverse index updated atomically. Member IDs uniquely assigned. Targets are separate from members, priority ordered by (priority, ID), death unlinks member only. `AutoReplenish`/ReservedResources disabled in parity mode. QString 128 bytes holds at least 32 UTF-16 code units; enforce original 1..32 trim rule and prevent truncation. |
| Map/fog | Immutable base mapping, mutable terrain chunks for configured variant/depletion. Per-player fog chunks of 256 cells, visibility/explored bitsets plus overlap counts. Identical sight stamps counted in owned dictionary; apply circle when first ref enters and remove when last leaves. Store last stamp and all counts in frame, invalidate at death/embark or rebuild from deterministic state. Iterate stable seat/chunk/id order. |
| Transport/Embarked | Ship owns passenger and pending lists. Transactional reserve, embark, unload or removal. Capacity 30 pop, land/water separate. Embarked units excluded from fog, targeting and movement. |
| Computer | Deterministic AI state including cadence, strategy, wave/recruit counter and attacking list; no secret random base. Decision inputs derive own entities and observation map and visible enemies. |

Data export provides source race/slot names, fully evaluated numeric stats, training/producer rules, research tiers/levels and price tables, cost and building rules, matches/settings/map fixtures, hero/race skills. Editor importer must normalize `null` (unlimited or time off) to flags + integer fields. Use enum order from `AaiTypes.qtn`, lookup by (race, kind) rather than order of JSON properties; derive combined costs and range modifiers from exact source functions. Build a read-only asset table with canonical ordered entries, version + SHA. Do not clone full rules into every unit. `AaiRulesAsset` is a schema, not generated populated assets yet. Additional constants below must be included in the rule importer and tested.

### Important private source constants

- Sight radius in cells: HQ 10; support 7; siege 8; scout 13 (elf ×1.1); transport 9; warship 10; worker 6; blade/polearm 7; ranged/marksman/heavy/vanguard 8; pathfinder 9; barracks/range/elite/smithy 5; farm 4; wall 2; tower 9; port 7; settlement 6.
- In `match.ts`: defense radius 8 tiles, leash 11 tiles, defense duration 4 s, 48 route resolutions per tick, boarding timeout 180 s, transport capacity 30 units of population. `isUnitKind` and `benefitsFromRange` define exclusions. `sight`, projectile timing, reveal expiry, collision/terrain tolerances, training legacy refund, hero recovery, building defenses and control timing are not data generated by the shared export. Port exactly from source rather than change them to constants from an old design.

### Collection lifetime

All dynamic lists/dictionaries in the DSL are uniquely owned by the declaring component. `AllocateOnComponentAdded` + `FreeOnComponentRemoved` are documented Quantum field attributes. If manual allocation needed, use Frame AllocateList/AllocateDictionary at OnAdded and ResolveList/ResolveDictionary within current Frame only. Free exactly once on removal, nullify ptr using default (or ref overload documented in SDK). Never copy ownership or keep a direct pointer across a list resize, destroy, rollback or frame. Avoid dictionary iteration order as a tie-break; sort by stable numeric cell/seat/EntityRef keys. Keep frame data authoritative so rollback/full snapshot restores it. Collection lifetime tests: 10.000 spawn/remove/recruit/cancel cycles, match teardown, save/load, rollback during mutation, heap leak check. [QTN collections](https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/dsl).

## Command design

Quantum 3.1 deterministic commands are regular C# types inheriting `Photon.Deterministic.DeterministicCommand`, with `Serialize(BitStream)`. Register ordered factories in SDK's `CommandSetup.User.cs` / `DeterministicCommandSetup.AddCommandFactoriesUser`, same order across platforms. Use 3.1's `QuantumGame.AddCommand`; process in global tick order by player and per-player command index (`Frame.GetPlayerCommandCount`, `GetPlayerCommand`) rather than grouping by command class if order matters. Keep continuous input empty/minimal according to installed SDK's existing input file. Unity view emits a command once after classified gesture, never one per tick or each drag sample. Simulation derives issuer from command owner, validates all fields including enum range and current state. Errors become small result events or readonly state. [Command API](https://doc.photonengine.com/quantum/v3/manual/commands).

| Command group | Payload (conceptual fields) | Rules to preserve |
|---|---|---|
| Move, AttackMove, Attack, Stop, Explore | Selector + destination FP or target ref + formation | Use stable own live IDs; no hidden-target commands; last target, halt and move behavior from source. |
| Gather, Build, WallLine, ResumeBuild, BuildQueue, Repair, CancelBuild | worker selector, resource/terrain position or site, kind; wall endpoints | Validate reachable/explored, cost, tier, placement, resources and assigned state; atomic accepted commands. Preserve queued construction. |
| Train, GlobalTrain, CancelTraining, CancelTrainingRun | building or global flag, kind, count, queue index/revision | Actual production permission, population, affordability, hero cap; reject stale revision. |
| Research, UpgradeBuilding, BulkStructureUpgrade | producer/building ref, kind | Level/tier/concurrency, exact cost and progress. |
| CreateArmyGroup, SaveTemplate, Rename, Delete, Assign, Release, Priority | group ID, bounded name, target counts, member refs, direction/expand | Max 8/500, unique membership, stable priorities, validated ownership. |
| Replenish, SetRallyPoint | group/production entity, expected counts or FP point | Recompute plan on simulation; stale expectation rejection, bank/pop reservation. |
| UseAbility | unit or hero ref | Cooldown, race, valid target and state. |
| Board, Transfer, Unload | selector, optional ship/destination | Owner, land/seaport, population, retry, unload capacity. |
| Demolish, Dismiss | owned entity ref(s) | Valid classes, existing confirmation is view responsibility. |
| Patrol, HoldPosition, Guard, Retreat, QueueOrder, CombatStance, AttackPriority | selector, destinations/guard target, options | Reserved future command variants; don't turn them on in baseline mode. Existing Retreat remains Move shortcut. |

Selector variants: stable `groupId` or built-in troop type/AllTroops; group resolution is by owned live members at processing tick, with no 500-element payload. Ad hoc selection sends bounded EntityRef list, sorted/deduplicated and checked; existing MAX_SELECTION=1000, group max 500. Start with chunks of at most 32 refs in development, measure actual serialization limit (handle `FailedTooBig`); avoid partial application by staging multipart selection within frame state and commit only a complete batch with matching owner/sequence, timeout on incomplete. Group command is preferable when large armies share an order. A 1000-unit action must map to same semantics atomically. Commands use no claimed owner IDs from view; scope comes from Quantum frame.

Signals are synchronous system communication inside simulation (e.g. unit destroyed, research completed). Events carry resulting visuals (hit, shot, warning, ability effects), deduplicate under rollback, use verified/synced presentation where necessary. The view reads state for HP, position, queue/pop and fog; commands and validated deterministic changes drive authoritative results. Example files `Examples/MoveGroupCommand.cs` + `ArmyInputBridge.cs` show one route with documented 3.1 API, to integrate next phase. Factory `factories.Add(new AaiMoveGroupCommand())` and processing belong in phase 2. Examples are not activated and not compiled. Avoid sending view-selected EntityRefs whose predicted spawn can be rolled back; rebuild selection on verified identity, discard vanished refs.

## Deterministic update order

Use a SystemsConfig and explicit ordered systems. The historical TS loop is sequential (including movement and combat per entity), so a naive batched ECS ordering changes combat simultaneity, timing and tie-breaks. Establish a parity mode first; separate systems by responsibility while preserving a stable processing order for legacy semantics. The following logical phases are a target, and every reordering must be validated against fixtures.

| Order | System(s) and dependencies |
|---:|---|
| 0 | PlayerSystem / Match init, deterministic map settings and seed, starting entities. |
| 1 | CommandSystem: validate/replay player inputs. AI decision uses prior `view` at cadence before `match.step` in source; AISystem submits the same order path before the gameplay step, with stable player ordering. |
| 2 | Advance tick; AbilitySystem passive state, expire summons/corpses, reset harvest count, SpatialPartitionSystem initial index. ProjectileSystem applies due impacts before other unit work; TransportSystem pickup updates and construction scheduling. |
| 3 | Construction/Repair/Production/Research/Economy: source interleaves these with per-entity actions, so deterministic stable iterator and shared functions initially. Queue/count/refund changes are atomic and update related group/pop state. HeroSystem healing/XP and AbilitySystem checks at corresponding source moments. |
| 4 | FormationSystem slots; TargetAcquisitionSystem + CombatSystem + MovementSystem within stable legacy order, respect pre- and post-hit visibility, attack turn order and defense response. NavigationSystem queues capped A* requests; land/water, obstacles, route resolution, local spacing. |
| 5 | TransportSystem unload/next-site queue fixup, path resolve and unit spacing exactly after unit loop. Run FogOfWarSystem incremental update, ArmyGroupSystem membership cleanup/assignment at spawn or death. |
| 6 | Economy income/stat samples and ControlPointSystem every 20 ticks, VictorySystem checks headquarters before control/time. Emit readonly events/report. |

Store all gameplay state in Frame; any scratch spatial index must be reproducibly rebuilt each tick from sorted frame entities, not persistent global mutable state. Fixed-point FP vectors for coordinates/range/speed, full integer ticks for timing; no floats or System.Random in runtime. `FP.FromRaw` from exported constants, fixed rational fractions with deterministic rounding; Unity View only quantizes coordinates once into raw ints for command. Legacy one game coord = one Unity world coordinate at first (32 units/tile), XZ maps FP X/Y. Avoid very large FP products; compare squared raw distances with checked range or scaled integer differences, validate map bounds. Source baseline is IEEE double and JS rounding. Do not promise exact byte-for-byte equality across old/new; define discrete event parity and tolerance (<=1 legacy world unit per checkpoint in movement; exact economy/health/orders/checkpoints, no diverging winner). Fix deterministic tie-breaks where old order is implicit and test source differences.

## Navigation and large armies

Quantum supports deterministic navmesh steering and avoidance; agents may use Transform2D, separate Pathfinder/Steering/Avoidance components and per-agent config. Baked navmesh regions toggle, but arbitrary wall/farm placement and finite trees change traversability. Use existing tile grid for reference prototype to preserve radius, shoreline and choke semantics. Benchmark stock navmesh as an isolated alternative. Photon has Flow Fields add-on with portal hierarchy and cached directions, but the sample leaves movement to game code. Verify license/compatibility with 3.1 before importing it. [Agent workflow](https://doc.photonengine.com/quantum/v3/manual/navigation/workflow-agents) · [Flow Fields](https://doc.photonengine.com/quantum/v3/addons/flow-fields/navigation).

Recommended order: stable 4/8-neighbor A* grid and segment tests → spatial hashing and per-radius obstacle mask keyed by deterministic terrain revision → bounded queue (48 legacy) → common route sharing with cache invalidation → flow fields/hierarchical portals after measurements. Maintain formation slots and local avoidance without Rigidbody per unit. Cache keyed by domain, radius, map revision, start/end cell (formation offsets on approach). Path/revision/queue must be rollback-safe. Choke tests with opposing 500 troops, workers/buildings and new walls, blocked entrances, land/ship routes, shore unload and dynamic forest removal. Distinguish failed path from no destination; bounded retry, never teleport. All resource and fog grids stable row-major; no sort by unbounded hash enumeration. Use deterministic tie-breaks (cell ID, seat, creation order) and check fixed-point differences against source.

## Unity View and mobile controls

One central input router (Unity Input System or existing SDK examples), view selection stores local entity refs and group ID. A pointer starting over UI stays UI until up/cancel. Outside-menu tap only closes/consumes; second tap performs gameplay. Transition touch count 1→2 cancels pending selection/order without canceling placement. Pinch-zoom and two-finger pan maintain selection/build mode. Last finger release consumed after multitouch; drag selection single finger, double tap visible same kind, workers/army/all filters. Long press group management only if stationary, horizontal troop bar drag cancels press. Hold context commands and camera shortcuts; repeated placement attaches server validated build order to queue. Walls have distinct drag mode. Native lifecycle pauses local runner explicitly but keeps manual pause separate; reconnect online. Audio resumes after activation, effects and HUD derived from verified/local visibility.

Orthographic angled camera, XZ world, stylised low-poly original models, atlas textures for each race/role, few material variants; instanced static meshes, pooled effects, culling/LOD, batched HP bars and fog shader, minimap from classified grid, no per-unit Update when possible. EntityViewUpdater/pooled EntityViews for selection/hits. Use portrait safe area with identical bar order and compact menus, quality profile low/medium/high/auto changes view only. Rendering interpolates predicted transforms and is independent of 20 Hz tick; never alters simulation. Add native back semantics and mobile accessibility tests. WebGL on modern Safari/Chrome is supported by Unity 6.3 but mobile memory and performance need physical validation. Keep browser TypeScript deployed while testing new Unity WebGL host separately.

## Multiplayer, fog confidentiality and security decision

Existing server owns full state and filters each seat snapshot, including entities/corpses/effects. Photon Quantum Public Cloud uses deterministic input synchronization, clients simulate complete state. It prevents local state modifications from changing other clients' accepted results, but **cannot provide the same confidentiality for hidden units/resources**. Implement visual fog filtering and validate target visibility in simulation for fair gameplay. A modified client still has access to full required state; standard Quantum cannot guarantee secret fog. Even adding server-side simulation alone does not hide the replicated information. No ranked/competitive release if confidential fog is a requirement until a separate topology/design is evaluated. Options: retain existing server-authoritative architecture for competitive mode (Unity view with compatible snapshot client is separate work), evaluate Photon Enterprise/custom plugin with Photon support and design filtered simulation/state, or explicitly accept perfect-information risk for casual Quantum play. Do not claim cheating-proof. [Photon cheat protection](https://doc.photonengine.com/quantum/v3/manual/cheat-protection).

Matchmaking uses AppId/AppVersion + region, public criteria and private room code. Store protocol/data fingerprint (schema, code build, immutable rules/maps hash, config/tickrate) in expected matchmaking/version checks. Reject incompatible game assets in pre-match authorization; no mixed TS/Quantum clients. Quantum configs and RuntimePlayer may be client-controlled in Public Cloud, validate via developer backend/webhooks when available, authenticate player identity and enforce all gameplay rules in simulation. Track session identity and TTL, test return within 45/90s against actual Quantum behavior, seat reclaim via authenticated user; room/member TTL and rejoin policy are explicit settings, not guaranteed persistence. Use verified replay/input stream plus deterministic resimulation for result validation rather than trusting client score; checksums for debug desync diagnosis, not anti-cheat. Recovery after cloud instance/room expiry requires durable snapshots + command journal, or documented match ended flow. Evaluate cost and service agreement with Photon before any server customization. No paid changes.

Pricing published 24.11.2026: 20 CCU free dev, 100 CCU free gaming launch on one app/customer, 500 CCU USD 125/mo and 1.000 CCU USD 250/mo on Public Cloud (subject to terms/traffic/region). One CCU = concurrent connected user, not units. No budget assumption for Enterprise; ask provider for quote if custom server needed. [Photon Quantum plans](https://www.photonengine.com/quantum/pricing).

## AI, offline and saves

Port current `ComputerOpponent` state/cadence and observed visibility exactly first, then overhaul economically adaptive worker composition, base expansion, diversified production, multi-base/race strategy, research to L5, reinforcement, rally, naval and control play. Test a 30-minute scripted match and recovery after asset/worker loss with actual economy. AI shares command validations and sees only own data + visible enemies (known terrain allowed as in source). No free resources, stats or hidden enemy info. All seed and decision state within simulated frame. Use Quantum Local Mode for solo and Practice with same SystemsConfig, run AI seat without cloud connection.

Save envelope: own schema version, SDK version, content/asset hash, tickrate, seed, map ID/config, player identities, verified snapshot bytes, AI state in frame, input/replay journal if needed, timestamp only metadata outside simulation, checksum. Write atomically to `Application.persistentDataPath` via temp + verified backup, retain prior valid save, load into a separate runner before replacing, validate size/version/data hash and display repair/export options on error. Do not delete original corrupted/unsupported save. Snapshot restores verified frame and corresponding configs (Quantum 3.1 uses `GetSnapshotFile` and `QuantumRunnerLocalDebug` rather than old CreateSavegame). Test interruption and rollback at every phase. Legacy TS saves cannot directly load into Quantum; add an explicit migration tool or keep playable in original app. [Quantum 3.1 release notes](https://doc.photonengine.com/quantum/v3/getting-started/release-notes/release-notes-3.1) · [Starting from snapshot](https://doc.photonengine.com/quantum/v3/manual/game-session/starting-from-snapshot).
