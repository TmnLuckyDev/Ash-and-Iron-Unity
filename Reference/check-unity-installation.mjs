import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const root = 'Assets/Photon'
function hasEngine(directory) {
  return existsSync(directory) && readdirSync(directory, { withFileTypes: true }).some(entry =>
    entry.isDirectory() ? hasEngine(join(directory, entry.name)) : entry.name === 'Quantum.Engine.dll')
}

if (!hasEngine(root) || !existsSync('Assets/QuantumUser/Simulation/Quantum.Simulation.asmdef')) {
  console.error('Quantum SDK and Quantum User Files must be imported into the private Unity project under the official license before running Unity tests. See Docs/05-cloud-development.md.')
  process.exitCode = 1
} else {
  console.log('Quantum SDK and Simulation assembly found. Unity will verify CodeGen and compilation.')
}
