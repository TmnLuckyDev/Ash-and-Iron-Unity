// Run with Node 22.12+ and TypeScript stripping enabled (Node 24 defaults to it).
// node Reference/export-reference.mjs /path/to/fantasy-rts Reference/data
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve, join } from 'node:path'
import { pathToFileURL } from 'node:url'
import { createHash } from 'node:crypto'
import { gzipSync } from 'node:zlib'

const [repoPath, outputPath] = process.argv.slice(2)
if (!repoPath || !outputPath) throw new Error('Specify a fantasy-rts checkout and a separate output directory.')
const repo = resolve(repoPath)
const output = resolve(outputPath)
assert.notEqual(repo, output)
const revision = execFileSync('git', ['-C', repo, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim()
assert.equal(execFileSync('git', ['-C', repo, 'status', '--porcelain', '--untracked-files=no'], { encoding: 'utf8' }).trim(), '', 'Export requires a clean tracked source checkout')
const load = file => import(pathToFileURL(join(repo, 'src/shared', file)).href)
const [p, e, u, r, h, m, s] = await Promise.all(['protocol.ts','economy.ts','units.ts','race-rules.ts','heroes.ts','map.ts','match-settings.ts'].map(load))

const cost = value => ({ gold: value.gold ?? 0, wood: value.wood ?? 0, stone: value.stone ?? 0 })
const entities = p.races.flatMap(race => p.entityKinds.map(kind => ({
  race, kind, name: p.entityNames[race][kind], ...u.unitStats[race][kind],
  trains: e.trainedAt(kind, race)
})))
const training = p.races.flatMap(race => p.unitKinds.map(kind => ({ race, kind,
  ...e.trainingRule(kind, race), cost: cost(e.trainingCost(kind, race)), hall: e.unitTier(kind) })))
const buildings = p.buildingKinds.map(kind => ({ kind, ...e.buildings[kind], cost: cost(e.buildings[kind].cost),
  hall: e.buildingTier(kind), footprint: e.buildingFootprint(kind), repairCost: cost(e.repairCost(kind)) }))
const research = p.upgradeKinds.flatMap(kind => Array.from({ length: e.upgradeLevelLimit(kind) }, (_, i) => ({
  kind, level: i + 1, name: e.upgrades[kind].name, bonus: e.upgrades[kind].bonus,
  cost: cost(e.researchCost(kind, i + 1)), ticks: e.researchTicks(kind, i + 1), legacyTicks: e.upgrades[kind].ticks,
  hall: e.researchTier(i + 1), producer: e.researchSite(kind)
})))
const structures = ['tower', 'wall'].flatMap(kind => Array.from({ length: e.MAX_UPGRADE_LEVEL - 1 }, (_, i) => ({
  kind, targetLevel: i + 2, ...e.structureUpgrade(kind, i + 2), cost: cost(e.structureUpgrade(kind, i + 2).cost),
  bulkCost: cost(e.bulkStructureCost(kind, i + 2))
})))
const hall = Object.entries(e.hallUpgrades).map(([level, rule]) => ({ level: Number(level), ...rule, cost: cost(rule.cost) }))
const rules = { schemaVersion: 1, revision, ticksPerSecond: s.TICKS_PER_SECOND, tile: m.TILE,
  races: p.races, entityKinds: p.entityKinds, unitKinds: p.unitKinds, buildingKinds: p.buildingKinds,
  researchKinds: p.upgradeKinds, resources: p.resourceKinds, formations: p.formations,
  entityNames: p.entityNames, factionNames: p.factionNames, entities, training, buildings, research, structures, hall,
  raceRules: r.raceRules, raceSkills: r.raceSkills, heroAbilities: h.heroAbilities,
  heroThresholds: h.heroThresholds, heroHealthPerLevel: h.heroHealthPerLevel, heroDamagePerLevel: h.heroDamagePerLevel,
  gathering: e.GATHERING, repair: e.REPAIR, siege: u.siegeBalance, defaults: s.defaultMatchSettings(),
  basePopulation: e.BASE_POPULATION, farmPopulation: e.FARM_POPULATION, cap: s.MAX_POPULATION_CAP,
  maxSelection: p.MAX_SELECTION, maxGroups: p.MAX_ARMY_GROUPS, maxGroupSize: p.MAX_ARMY_GROUP_SIZE,
  control: { radius: s.CONTROL_RADIUS, score: s.CONTROL_VICTORY_SCORE }, limits: {
    deposits: s.MAX_DEPOSITS, starting: s.STARTING_RESOURCE_MAX, stock: s.DEPOSIT_STOCK_MAX,
    workers: s.WORKER_LIMIT_MAX, minutes: s.TIME_LIMIT_MAX_MINUTES
  } }
assert.equal(entities.length, 115)
assert.equal(training.length, 65)
assert.equal(research.length, 31)
assert.equal(structures.length, 8)
assert.equal(rules.defaults.populationCap, 500)
for (const race of p.races) {
  assert.equal(entities.find(row => row.race === race && row.kind === 'tower').range, 205)
  assert.equal(e.attackRange(race, 'tower', 5), 265)
  assert.equal(training.find(row => row.race === race && row.kind === 'warship').population, 2)
  assert.equal(training.find(row => row.race === race && row.kind === 'transport').population, 1)
}
const maps = m.mapOptions.map(option => ({ option, defaults: s.defaultMatchSettings(option.id),
  terrain: m.ashfordMap(option.id), fixtures: [1, 42, 2026].flatMap(seed => ['classic', 'control'].map(objective => ({
    seed, objective, map: m.mapForSettings({ ...s.defaultMatchSettings(option.id), objective }, seed)
  }))) }))
for (const entry of maps) for (const map of [entry.terrain, ...entry.fixtures.map(x => x.map)]) {
  assert.equal(map.tiles.length, map.width * map.height)
  assert.deepEqual(map.tiles, [...map.tiles].reverse())
}

mkdirSync(output, { recursive: true })
const manifest = {}
for (const [name, value] of Object.entries({ rules, maps })) {
  const bytes = JSON.stringify(value, null, 2) + '\n'
  const file = name === 'maps' ? `${name}.json.gz` : `${name}.json`
  const stored = name === 'maps' ? gzipSync(bytes, { level: 9 }) : bytes
  writeFileSync(join(output, file), stored)
  manifest[file] = createHash('sha256').update(stored).digest('hex')
}
writeFileSync(join(output, 'manifest.json'), JSON.stringify({ revision, entities: entities.length, training: training.length,
  research: research.length, maps: maps.length, mapFixtures: maps.reduce((n, entry) => n + entry.fixtures.length, 0), sha256: manifest }, null, 2) + '\n')
console.log(JSON.stringify({ revision, entities: entities.length, training: training.length, research: research.length, mapFixtures: 36 }))
