import assert from 'node:assert/strict'
import { test } from 'node:test'
import { buildTerrainPreview, loadPreviewMaps } from './export-terrain-preview.mjs'

test('every reference map exports a complete, correctly oriented ground mesh', () => {
  const maps = loadPreviewMaps()
  assert.equal(maps.length, 42)
  const names = new Set()
  for (const map of maps) {
    const { name, obj, mtl } = buildTerrainPreview(map)
    assert.ok(!names.has(name), `Duplicate output name: ${name}`)
    names.add(name)
    assert.ok(obj.startsWith(`mtllib ${name}.mtl\n`))
    const positions = []
    const materials = new Set(mtl.matchAll(/^newmtl (\w+)$/gm).map(match => match[1]))
    const facesByMaterial = new Map()
    let groundArea = 0
    let activeMaterial
    for (const line of obj.split('\n')) {
      if (line.startsWith('v ')) positions.push(line.slice(2).split(' ').map(Number))
      if (line.startsWith('usemtl ')) {
        activeMaterial = line.slice(7)
        assert.ok(materials.has(activeMaterial))
      }
      if (!line.startsWith('f ')) continue
      const vertices = line.slice(2).split(' ').map(index => positions[Number(index) - 1])
      assert.ok(vertices.every(point => point?.every(Number.isFinite)))
      facesByMaterial.set(activeMaterial, (facesByMaterial.get(activeMaterial) ?? 0) + 1)
      const [[ax, ay, az], [bx, by, bz], [cx, cy, cz]] = vertices
      if (ay === 0 && by === 0 && cy === 0) {
        const twiceArea = (bz - az) * (cx - ax) - (bx - ax) * (cz - az)
        assert.ok(twiceArea > 0, `${name} has invalid ground winding`)
        groundArea += twiceArea / 2
      }
    }
    assert.equal(groundArea, map.Width * map.Height, name)
    assert.equal(facesByMaterial.get('seat_one'), 4)
    assert.equal(facesByMaterial.get('seat_two'), 4)
  }
})

test('the same variant produces identical output and invalid geometry is rejected', () => {
  const map = loadPreviewMaps().find(m => m.MapId === 'shattered-isles' && m.HasSeed && m.Seed === 42 && m.Mode === 1)
  const mesh = buildTerrainPreview(map)
  assert.equal(mesh.name, 'shattered-isles-42-control')
  assert.deepEqual(mesh, buildTerrainPreview(map))
  assert.throws(() => buildTerrainPreview({ ...map, Tiles: [99, ...map.Tiles.slice(1)] }), /Invalid map geometry/)
  assert.throws(() => buildTerrainPreview({ ...map, SeatTwoXRaw: -1 }), /Seat Two outside the map/)
})
