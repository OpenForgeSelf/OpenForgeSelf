// patch-esbuild.mjs — environment workarounds applied to every web dir
// (host ForgeSelf.Web + each Plugins/<X>/web) by scripts/release/build-frontend.ps1.
//
// Workaround 1 — sandbox safe-delete shim.
// Root cause: the host sandbox intercepts DeleteFileW. For esbuild's native Go
// binary it returns failure, so when esbuild tries to remove the temp file it
// created for inputs >1MB (and outputs >ESBUILD_MAX_BUFFER), the Go service
// aborts the build with "remove <path>: Access is denied".
// Fix: keep esbuild I/O inline so it never creates a temp file to delete.
//   - input spill threshold raised from 1MB to ~1TB (inputs stay on stdin)
//   - ESBUILD_MAX_BUFFER must be raised by the caller (build script) so large
//     outputs also stay inline.
// This is valid in every environment, so applying it unconditionally is safe.
//
// Workaround 2 — machine-level ESBUILD_BINARY_PATH pointing at a mismatched
// binary (2026-10-28).
// Root cause: esbuild's install.js honours the ESBUILD_BINARY_PATH env var by
//   (a) rewriting lib/main.js with a hardcoded `var ESBUILD_BINARY_PATH = "<path>"`,
//   (b) replacing bin/esbuild with a shim that execs that path,
//   and the native binary then refuses to serve a different version:
//     "Cannot start service: Host version "0.25.0" does not match binary version "0.25.12"".
// A single machine-wide env var therefore breaks every web dir whose esbuild
// version differs from the global binary — here: 12 plugin frontends pin
// esbuild 0.25.0 while the global binary is 0.25.12 (the host pins 0.25.12 and
// happened to match by luck).
// Fix: prefer the local platform package binary, whose version always matches
// the esbuild JS package by construction. Neutralise the global override in
// lib/main.js and regenerate bin/esbuild to resolve the local binary at runtime.
// Only applied when the local platform package is actually present, so
// installs without optional deps keep using the env var override.
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';

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
} else if (!orig.includes(needle)) {
  console.log(`[patch-esbuild] spill guard not found in ${mainJs} — skip (esbuild version may differ)`);
} else {
  writeFileSync(mainJs, orig.replace(needle, replacement));
  console.log(`[patch-esbuild] patched input-spill threshold: ${mainJs}`);
}

// ---------------------------------------------------------------------------
// Workaround 2 — neutralise a machine-level ESBUILD_BINARY_PATH override that
// points at a binary whose version differs from this esbuild package version.
// ---------------------------------------------------------------------------

const PLATFORM_PKG = {
  'win32-x64': '@esbuild/win32-x64',
  'win32-arm64': '@esbuild/win32-arm64',
  'win32-ia32': '@esbuild/win32-ia32',
  'darwin-x64': '@esbuild/darwin-x64',
  'darwin-arm64': '@esbuild/darwin-arm64',
  'linux-x64': '@esbuild/linux-x64',
  'linux-arm64': '@esbuild/linux-arm64',
  'linux-arm': '@esbuild/linux-arm',
  'linux-ia32': '@esbuild/linux-ia32',
  'linux-ppc64': '@esbuild/linux-ppc64',
  'linux-riscv64': '@esbuild/linux-riscv64',
  'linux-s390x': '@esbuild/linux-s390x',
  'linux-loong64': '@esbuild/linux-loong64',
  'freebsd-x64': '@esbuild/freebsd-x64',
  'freebsd-arm64': '@esbuild/freebsd-arm64',
  'openbsd-x64': '@esbuild/openbsd-x64',
  'openbsd-arm64': '@esbuild/openbsd-arm64',
  'sunos-x64': '@esbuild/sunos-x64',
  'aix-ppc64': '@esbuild/aix-ppc64',
};

const platformKey = `${process.platform}-${process.arch}`;
const platformPkg = PLATFORM_PKG[platformKey];
if (!platformPkg) {
  console.log(`[patch-esbuild] unknown platform ${platformKey} — skip binary-path fix`);
  process.exit(0);
}

const esbuildPkgDir = dirname(dirname(mainJs));
const binaryFile = process.platform === 'win32' ? 'esbuild.exe' : 'esbuild';
// pnpm isolated layout: the platform package is a sibling of the esbuild package
// inside <.pnpm>/<pkg>/node_modules/, not inside the package dir itself.
const candidateBinaries = [
  join(dirname(esbuildPkgDir), platformPkg, binaryFile),
  join(esbuildPkgDir, 'node_modules', platformPkg, binaryFile),
];
const localBinary = candidateBinaries.find((p) => existsSync(p));

if (!existsSync(localBinary)) {
  console.log(
    `[patch-esbuild] local platform binary absent (${localBinary}) — keep ESBUILD_BINARY_PATH override as-is`,
  );
  process.exit(0);
}

const pkgJsonPath = join(esbuildPkgDir, 'package.json');
const esbuildVersion = existsSync(pkgJsonPath)
  ? JSON.parse(readFileSync(pkgJsonPath, 'utf8')).version
  : 'unknown';

// 1) main.js — drop the hardcoded install.js path and stop preferring the env var.
let mainSrc = readFileSync(mainJs, 'utf8');
const hardcoded = /^var ESBUILD_BINARY_PATH = ".*";$/m;
const envPreferred =
  'var ESBUILD_BINARY_PATH = process.env.ESBUILD_BINARY_PATH || ESBUILD_BINARY_PATH;';

let mainChanged = false;
if (hardcoded.test(mainSrc)) {
  mainSrc = mainSrc.replace(hardcoded, 'var ESBUILD_BINARY_PATH = "";');
  mainChanged = true;
}
if (mainSrc.includes(envPreferred)) {
  mainSrc = mainSrc.replace(envPreferred, 'var ESBUILD_BINARY_PATH = ESBUILD_BINARY_PATH;');
  mainChanged = true;
}
if (mainChanged) {
  writeFileSync(mainJs, mainSrc);
}

// 2) bin/esbuild — regenerate the shim so it resolves the local platform binary.
const binShim = join(esbuildPkgDir, 'bin', 'esbuild');
const shimBody = `#!/usr/bin/env node
"use strict";
// Generated by scripts/release/patch-esbuild.mjs.
// Resolves the esbuild binary from the local platform package so its version
// always matches this esbuild package (avoids a machine-level
// ESBUILD_BINARY_PATH pointing at a mismatched global binary).
const { createRequire } = require("node:module");
const { execFileSync } = require("node:child_process");
const { dirname, join } = require("node:path");
const req = createRequire(__filename);
const bin = join(dirname(req.resolve("${platformPkg}/package.json")), "${binaryFile}");
execFileSync(bin, process.argv.slice(2), { stdio: "inherit" });
`;
const currentShim = existsSync(binShim) ? readFileSync(binShim, 'utf8') : '';
if (!currentShim.includes('patch-esbuild.mjs')) {
  writeFileSync(binShim, shimBody);
}

console.log(
  `[patch-esbuild] local binary pinned: esbuild@${esbuildVersion} -> ${platformPkg}/${binaryFile}` +
    ` (machine ESBUILD_BINARY_PATH=${process.env.ESBUILD_BINARY_PATH || '<unset>'})`,
);
