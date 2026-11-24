// Editor input is deterministic, ordered and free of JS maps/nullable values.
// Run from the Unity project root. --check verifies the committed output.
import assert from 'node:assert/strict'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'
import { gzipSync, gunzipSync } from 'node:zlib'

const read = name => JSON.parse(readFileSync(`Reference/data/${name}.json`, 'utf8'))
const source = read('rules')
const mapSource = JSON.parse(gunzipSync(readFileSync('Reference/data/maps.json.gz')).toString('utf8'))
const toIndex = (values, value) => {
  const index = values.indexOf(value)
  assert.ok(index >= 0, `Unknown value ${value}`)
  return index
}
const race = value => toIndex(source.races, value)
const kind = value => toIndex(source.entityKinds, value)
const research = value => toIndex(source.researchKinds, value)
const resource = value => toIndex(source.resources, value)
// Quantum FP Q48.16, canonical conversion of nonnegative reference values.
const raw = value => {
  const result = Math.round(value * 65536)
  assert.ok(Number.isSafeInteger(result))
  return result
}
const cost = value => ({ Gold: value.gold ?? 0, Wood: value.wood ?? 0, Stone: value.stone ?? 0 })
const sight = { headquarters: 10, support: 7, siege: 8, scout: 13, transport: 9, warship: 10, worker: 6, blade: 7, polearm: 7, ranged: 8, marksman: 8, heavy: 8, vanguard: 8, pathfinder: 9, barracks: 5, range: 5, elite: 5, farm: 4, wall: 2, tower: 9, smithy: 5, port: 7, settlement: 6 }
const abilities = source.races.flatMap(raceName => {
  const custom = source.raceSkills[raceName] ?? {}
  const entries = raceName === 'human' || raceName === 'orc' ? source.heroAbilities : custom
  return Object.entries(entries).map(([kindName, value]) => ({ Race: race(raceName), Kind: kind(kindName),
    CooldownTicks: value.cooldown, DurationTicks: value.duration ?? 0,
    MaxTargets: value.maxTargets ?? (raceName === 'elf' && kindName === 'support' || raceName === 'undead' && kindName === 'pathfinder' ? 3 : 0),
    RadiusRaw: raw(value.radius), BonusRaw: raw(value.bonus) }))
})
const catalog = {
  SchemaVersion: source.schemaVersion, SourceRevision: source.revision,
  SourceTicksPerSecond: source.ticksPerSecond, TileSize: source.tile,
  DefaultStarting: cost(source.defaults.starting), DefaultPopulationCap: source.defaults.populationCap,
  BasePopulation: source.basePopulation, FarmPopulation: source.farmPopulation,
  MaxPopulationCap: source.cap, MaxArmyGroups: source.maxGroups, MaxArmyGroupSize: source.maxGroupSize, MaxSelection: source.maxSelection,
  TransportCapacity: 30, RepairHealth: source.repair.health, RepairTicks: source.repair.ticks,
  ControlRadiusRaw: raw(source.control.radius), ControlVictoryScore: source.control.score,
  SiegeStructureRangeRaw: raw(source.siege.structureRange), SiegeStructureBonus: source.siege.structureBonus, SiegeUnitPenalty: source.siege.unitPenalty,
  HeroThresholds: source.heroThresholds, HeroHealthPerLevel: source.heroHealthPerLevel, HeroDamagePerLevel: source.heroDamagePerLevel,
  Entities: source.entities.map(row => ({ Race: race(row.race), Kind: kind(row.kind), Health: row.health, Damage: row.damage,
    Armor: row.armor, CooldownTicks: row.cooldown, SightTenths: Math.round(sight[row.kind] * 10 * (row.kind === 'scout' ? source.raceRules[row.race].scoutSight ?? 1 : 1)),
    RangeRaw: raw(row.range), SpeedRaw: raw(row.speed), RadiusRaw: raw(row.radius), Trainable: row.trains.map(kind) })),
  Training: source.training.map(row => ({ Race: race(row.race), Kind: kind(row.kind), Population: row.population,
    Ticks: row.ticks, RequiredHall: row.hall, Cost: cost(row.cost) })),
  Buildings: source.buildings.map(row => ({ Kind: kind(row.kind), Ticks: row.ticks, RequiredHall: row.hall,
    FootprintRaw: raw(row.footprint), Cost: cost(row.cost), RepairCost: cost(row.repairCost) })),
  Research: source.research.map(row => ({ Kind: research(row.kind), Level: row.level, Ticks: row.ticks, LegacyTicks: row.legacyTicks,
    RequiredHall: row.hall, Bonus: row.bonus, Producer: kind(row.producer), Cost: cost(row.cost) })),
  Hall: source.hall.map(row => ({ Level: row.level, Ticks: row.ticks, Cost: cost(row.cost), Requirements: row.required.map(kind) })),
  Structures: source.structures.map(row => ({ Kind: kind(row.kind), Level: row.targetLevel, Ticks: row.ticks, Cost: cost(row.cost), BulkCost: cost(row.bulkCost) })),
  Gathering: source.resources.map(name => ({ Resource: resource(name), Amount: source.gathering[name].amount, Ticks: source.gathering[name].ticks })),
  Races: source.races.map(name => ({ Race: race(name), GatheringMultiplierRaw: raw(source.raceRules[name].gathering ?? 1),
    StoneMultiplierRaw: raw(source.raceRules[name].stoneYield ?? 1), ScoutSightMultiplierRaw: raw(source.raceRules[name].scoutSight ?? 1),
    TowerMultiplierRaw: raw(source.raceRules[name].defense?.tower ?? 1), WallMultiplierRaw: raw(source.raceRules[name].defense?.wall ?? 1),
    Ambush: (source.raceRules[name].ambush ?? []).map(kind) })),
  Abilities: abilities
}

const tiles = ['grass','road','forest','water','rock','bridge','mine','quarry']
const convert = (map, defaults, seed, objective) => ({
  MapId: map.id, DisplayName: map.name, Width: map.width, Height: map.height, TileSize: source.tile,
  HasSeed: seed !== undefined, Seed: seed ?? 0, Mode: objective === 'control' ? 1 : 0,
  SeatOneXRaw: raw(map.bases.human.x), SeatOneYRaw: raw(map.bases.human.y),
  SeatTwoXRaw: raw(map.bases.orc.x), SeatTwoYRaw: raw(map.bases.orc.y),
  Tiles: map.tiles.map(tile => toIndex(tiles, tile)),
  Sources: source.resources.map(name => ({ CountPerSide: defaults.sources[name].count,
    UnlimitedStock: defaults.sources[name].stock === null, Stock: defaults.sources[name].stock ?? 0,
    UnlimitedWorkers: defaults.sources[name].workers === null, MaxWorkers: defaults.sources[name].workers ?? 0 }))
})
const maps = { Maps: mapSource.flatMap(entry => [convert(entry.terrain, entry.defaults), ...entry.fixtures.map(f => convert(f.map, entry.defaults, f.seed, f.objective))]) }
assert.equal(catalog.Entities.length, 115)
assert.equal(catalog.Training.length, 65)
assert.equal(catalog.Research.length, 31)
assert.equal(maps.Maps.length, 42)
assert.equal(catalog.Abilities.length, 14)
for (const map of maps.Maps) assert.equal(map.Tiles.length, map.Width * map.Height)
for (const row of catalog.Entities) assert.equal(row.Race * source.entityKinds.length + row.Kind, catalog.Entities.indexOf(row))

const outputs = {
  'Assets/AshAndIron/Reference/Generated/rules.editor.json': catalog,
  'Assets/AshAndIron/Reference/Generated/maps.editor.json.gz': maps
}
for (const [path, object] of Object.entries(outputs)) {
  const bytes = JSON.stringify(object) + '\n'
  const compressed = path.endsWith('.gz')
  if (process.argv.includes('--check')) {
    const stored = readFileSync(path)
    assert.equal((compressed ? gunzipSync(stored) : stored).toString('utf8'), bytes, `${path} is stale`)
  } else { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, compressed ? gzipSync(bytes, { level: 9 }) : bytes) }
}
console.log('Compiled asset input: 115 entities, 65 training rows, 31 research rows, 42 maps and 14 abilities; canonical FP raw values.')
