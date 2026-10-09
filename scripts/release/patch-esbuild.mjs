// patch-esbuild.mjs — environment workaround for the sandbox safe-delete shim.
//
// Root cause: the host sandbox intercepts DeleteFileW. For esbuild's native Go
// binary it returns failure, so when esbuild tries to remove the temp file it
// created for inputs >1MB (and outputs >ESBUILD_MAX_BUFFER), the Go service
// aborts the build with "remove <path>: Access is denied".
//
// Fix: keep esbuild I/O inline so it never creates a temp file to delete.
//   - input spill threshold raised from 1MB to ~1TB (inputs stay on stdin)
//   - ESBUILD_MAX_BUFFER must be raised by the caller (build script) so large
//     outputs also stay inline.
// This is valid in every environment, so applying it unconditionally is safe.
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const webDir = process.argv[2] || process.cwd();

function findMainJs() {
  const pnpmDir = join(webDir, 'node_modules', '.pnpm');
  if (!existsSync(pnpmDir)) return null;
  for (const d of readdirSync(pnpmDir)) {
    if (/^esbuild@/.test(d)) {
      const p = join(pnpmDir, d, 'node_modules', 'esbuild', 'lib', 'main.js');
      if (existsSync(p)) return p;
    }
  }
  return null;
}

const mainJs = findMainJs();
if (!mainJs) {
  console.log(`[patch-esbuild] esbuild not found under ${webDir} — skip`);
  process.exit(0);
}

const orig = readFileSync(mainJs, 'utf8');
const needle = '&& input.length > 1024 * 1024) {';
const replacement = '&& input.length > 1024 * 1024 * 1024 * 1024) {';

if (orig.includes(replacement)) {
  console.log(`[patch-esbuild] already patched: ${mainJs}`);
  process.exit(0);
}
if (!orig.includes(needle)) {
  console.log(`[patch-esbuild] spill guard not found in ${mainJs} — skip (esbuild version may differ)`);
  process.exit(0);
}

writeFileSync(mainJs, orig.replace(needle, replacement));
console.log(`[patch-esbuild] patched input-spill threshold: ${mainJs}`);
