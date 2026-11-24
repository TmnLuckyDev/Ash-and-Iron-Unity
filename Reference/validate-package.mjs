// Structural checks only. This does not substitute for the Quantum CodeGen.
import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { gunzipSync } from 'node:zlib'

const root = dirname(dirname(fileURLToPath(import.meta.url)))
const read = file => readFileSync(join(root, file), 'utf8')
const manifest = JSON.parse(read('Reference/data/manifest.json'))
for (const [file, hash] of Object.entries(manifest.sha256)) {
  assert.equal(createHash('sha256').update(readFileSync(join(root, 'Reference/data', file))).digest('hex'), hash)
}
const rules = JSON.parse(read('Reference/data/rules.json'))
const maps = JSON.parse(gunzipSync(readFileSync(join(root, 'Reference/data/maps.json.gz'))).toString('utf8'))
assert.equal(rules.revision, manifest.revision)
assert.equal(rules.entities.length, manifest.entities)
assert.equal(new Set(rules.entities.map(row => `${row.race}/${row.kind}`)).size, 115)
assert.equal(rules.training.length, 65)
assert.equal(rules.research.length, 31)
assert.equal(maps.length, 6)
assert.equal(maps.flatMap(x => x.fixtures).length, 36)

const base = 'Assets/QuantumUser/Simulation/AshAndIron'
const dsl = readdirSync(join(root, base, 'DSL')).filter(file => file.endsWith('.qtn'))
assert.equal(dsl.length, 6)
const definitions = new Set()
const content = dsl.map(file => {
  const source = read(`${base}/DSL/${file}`).replace(/\/\/[^\n]*/g, '')
  let depth = 0
  for (const char of source) {
    if (char === '{') depth++
    if (char === '}') depth--
    assert.ok(depth >= 0, `${file}: unexpected close brace`)
  }
  assert.equal(depth, 0, `${file}: unclosed brace`)
  for (const [, name] of source.matchAll(/\b(?:enum|component|struct|asset)\s+(Aai\w+)/g)) {
    assert.ok(!definitions.has(name), `Duplicate declaration: ${name}`)
    definitions.add(name)
  }
  return source
}).join('\n')
for (const name of content.matchAll(/\bAai\w+/g)) {
  assert.ok(definitions.has(name[0]) || name[0] === 'AaiMatchEntity', `Unknown type: ${name[0]}`)
}

function checkEnum(name, expected) {
  const body = content.match(new RegExp(`enum ${name} : Byte \\{([^}]+)\\}`))[1]
  const actual = body.trim().split(',').map((entry, index) => {
    const [key, value] = entry.trim().split(/\s*=\s*/)
    assert.equal(Number(value), index, `${name}: ${key} must use stable index ${index}`)
    return key.toLowerCase()
  })
  assert.deepEqual(actual, expected)
}
checkEnum('AaiRace', rules.races)
checkEnum('AaiKind', rules.entityKinds)
checkEnum('AaiResource', rules.resources)
checkEnum('AaiResearchKind', rules.researchKinds)
checkEnum('AaiFormation', rules.formations)
for (const name of ['AaiRulesAsset', 'AaiMapAsset']) {
  assert.ok(read(`${base}/Rules/${name}.cs`).includes(`class ${name} : AssetObject`))
}
const baseline = read('Reference/baseline.jsonl').trim().split('\n').map(JSON.parse)
assert.equal(baseline.length, 18)
assert.equal(new Set(baseline.map(r => `${r.units}/${r.scenario}`)).size, 18)
console.log(`Reference integrity OK: ${definitions.size} QTN declarations, ${dsl.length} files, 115 entities, 36 map fixtures and 18 benchmark runs. Quantum CodeGen still required.`)
