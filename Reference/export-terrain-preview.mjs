import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
import { gunzipSync } from 'node:zlib'

const palette = {
  grass: [0.32, 0.49, 0.27], road: [0.60, 0.48, 0.33], forest: [0.19, 0.34, 0.23],
  water: [0.18, 0.40, 0.51], rock: [0.38, 0.39, 0.41], bridge: [0.53, 0.36, 0.23],
  mine: [0.68, 0.49, 0.23], quarry: [0.57, 0.59, 0.63], pine: [0.12, 0.26, 0.20],
  seat_one: [0.20, 0.56, 0.81], seat_two: [0.73, 0.28, 0.24],
}
const tileNames = ['grass', 'road', 'forest', 'water', 'rock', 'bridge', 'mine', 'quarry']

export function loadPreviewMaps() {
  const path = new URL('../Assets/AshAndIron/Reference/Generated/maps.editor.json.gz', import.meta.url)
  return JSON.parse(gunzipSync(readFileSync(path))).Maps
}

export function buildTerrainPreview(map) {
  const { Width: width, Height: height, TileSize: tileSize, Tiles: tiles } = map
  if (!/^[a-z0-9-]+$/.test(map.MapId) || !Number.isSafeInteger(width) || width <= 0 ||
      !Number.isSafeInteger(height) || height <= 0 || !Number.isSafeInteger(tileSize) || tileSize <= 0 ||
      tiles.length !== width * height || tiles.some(tile => !Number.isInteger(tile) || !tileNames[tile])) {
    throw new Error('Invalid map geometry')
  }
  const name = map.HasSeed ? `${map.MapId}-${map.Seed}-${map.Mode === 1 ? 'control' : 'classic'}` : map.MapId
  const vertices = []
  const surfaces = new Map()
  function polygon(material, points, triangles) {
    const offset = vertices.length + 1
    for (const point of points) vertices.push(`v ${point.map(value => value.toFixed(5)).join(' ')}`)
    if (!surfaces.has(material)) surfaces.set(material, [])
    for (const triangle of triangles) surfaces.get(material).push(`f ${triangle.map(index => index + offset).join(' ')}`)
  }
  function quad(material, x, z, right, bottom, y = 0) {
    polygon(material, [[x, y, z], [x, y, bottom], [right, y, bottom], [right, y, z]], [[0, 1, 2], [0, 2, 3]])
  }
  function peak(material, x, z, radius, y, base = 0) {
    polygon(material, [[x - radius, base, z - radius], [x - radius, base, z + radius],
      [x + radius, base, z + radius], [x + radius, base, z - radius], [x, y, z]],
    [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]])
  }

  // Stamp one upward-facing surface per horizontal run of identical tiles.
  for (let z = 0; z < height; z++) {
    let x = 0
    while (x < width) {
      const tile = tiles[z * width + x]
      let end = x + 1
      while (end < width && tiles[z * width + end] === tile) end++
      quad(tileNames[tile], x, z, end, z + 1)
      x = end
    }
  }
  for (let z = 0; z < height; z++) {
    for (let x = 0; x < width; x++) {
      const terrain = tileNames[tiles[z * width + x]]
      if (terrain === 'forest' && (x + z * 2) % 3 === 0) peak('pine', x + 0.5, z + 0.5, 0.31, 1.05)
      if (terrain === 'rock' && (x + z) % 2 === 0) peak('rock', x + 0.5, z + 0.5, 0.46, 0.68)
      if (terrain === 'bridge') quad('bridge', x + 0.04, z + 0.04, x + 0.96, z + 0.96, 0.085)
    }
  }
  for (const [seat, material] of [['One', 'seat_one'], ['Two', 'seat_two']]) {
    const x = map[`Seat${seat}XRaw`] / 65536 / tileSize
    const z = map[`Seat${seat}YRaw`] / 65536 / tileSize
    if (!Number.isFinite(x) || !Number.isFinite(z) || x < 0 || x >= width || z < 0 || z >= height) {
      throw new Error(`Seat ${seat} outside the map`)
    }
    peak(material, x, z, 0.42, 0.85, 0.02)
  }

  const faceLines = [...surfaces].flatMap(([material, faces]) => [`usemtl ${material}`, ...faces])
  const mtl = Object.entries(palette).map(([material, rgb]) => `newmtl ${material}\nKd ${rgb.join(' ')}\nillum 1`).join('\n\n')
  return { name, obj: [`mtllib ${name}.mtl`, `o ${name}`, ...vertices, ...faceLines, ''].join('\n'), mtl: `${mtl}\n` }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [mapId, output, seed, mode = 'classic'] = process.argv.slice(2)
  if (!mapId || !output || !['classic', 'control'].includes(mode) ||
      (seed === undefined && mode !== 'classic') || (seed !== undefined && !/^\d+$/.test(seed))) {
    throw new Error('Usage: node Reference/export-terrain-preview.mjs map-id output-folder [seed] [classic|control]')
  }
  const map = loadPreviewMaps().find(entry => entry.MapId === mapId && (seed === undefined ? !entry.HasSeed :
    entry.HasSeed && entry.Seed === Number(seed) && entry.Mode === (mode === 'control' ? 1 : 0)))
  if (!map) throw new Error('Map or seed/mode variant unavailable in the reference catalog')
  const { name, obj, mtl } = buildTerrainPreview(map)
  mkdirSync(output, { recursive: true })
  writeFileSync(join(output, `${name}.obj`), obj)
  writeFileSync(join(output, `${name}.mtl`), mtl)
  console.log(`Exported ${name} (${map.Width} × ${map.Height} tiles) to ${output}`)
}
