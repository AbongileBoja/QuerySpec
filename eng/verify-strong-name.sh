#!/usr/bin/env bash
# usage: verify-strong-name.sh <dir> [<dir> ...]
# Exit 0 if every QuerySpec.*.dll in the given dirs is fully strong-name signed.

set -euo pipefail

if [[ "$#" -lt 1 ]]; then
  echo "usage: $0 <dir> [<dir> ...]" >&2
  exit 2
fi

shopt -s globstar nullglob

failures=0
checked=0

verify_one () {
  local dll="$1"

  # Prefer `sn -vf` when available (Windows runners or where mono is installed).
  if command -v sn >/dev/null 2>&1; then
    if sn -vf "$dll" >/dev/null 2>&1; then
      return 0
    fi
    return 1
  fi

  # Fallback: python PE walker. Locates the CLR header → StrongNameSignature
  # directory → reads the referenced bytes; any all-zero region is a fail.
  python3 - "$dll" <<'PY' || return 1
import struct, sys

path = sys.argv[1]
with open(path, "rb") as f:
    data = f.read()

# DOS header → PE offset
pe_off = struct.unpack_from("<I", data, 0x3C)[0]
if data[pe_off:pe_off+4] != b"PE\x00\x00":
    sys.exit(2)

# COFF header is at pe_off+4, size 20. Optional header follows.
opt_off = pe_off + 24
magic = struct.unpack_from("<H", data, opt_off)[0]
is_pe32_plus = (magic == 0x20B)

# Data directories live after the standard optional header fields.
# CLR Runtime Header is data directory entry index 14.
dd_off = opt_off + (112 if not is_pe32_plus else 128) - 16 + (14 * 8)
clr_rva, clr_size = struct.unpack_from("<II", data, dd_off)
if clr_rva == 0:
    print(f"{path}: no CLR header (not a managed assembly)", file=sys.stderr)
    sys.exit(2)

# Build section table to translate RVAs to file offsets.
num_sections = struct.unpack_from("<H", data, pe_off + 6)[0]
sections_off = opt_off + struct.unpack_from("<H", data, pe_off + 20)[0]
sections = []
for i in range(num_sections):
    s = sections_off + (i * 40)
    vsize, vaddr, rsize, raddr = struct.unpack_from("<IIII", data, s + 8)
    sections.append((vaddr, vsize, raddr, rsize))

def rva_to_off(rva):
    for vaddr, vsize, raddr, rsize in sections:
        if vaddr <= rva < vaddr + max(vsize, rsize):
            return raddr + (rva - vaddr)
    return None

clr_off = rva_to_off(clr_rva)
if clr_off is None:
    sys.exit(2)

# CLR header layout: 8-byte header, then StrongNameSignature data dir at +32.
sn_rva, sn_size = struct.unpack_from("<II", data, clr_off + 32)
if sn_rva == 0 or sn_size == 0:
    print(f"{path}: not strong-name signed", file=sys.stderr)
    sys.exit(1)

sn_off = rva_to_off(sn_rva)
if sn_off is None:
    sys.exit(2)

sig = data[sn_off:sn_off + sn_size]
if all(b == 0 for b in sig):
    print(f"{path}: delay-signed (signature region is all zeroes)", file=sys.stderr)
    sys.exit(1)

sys.exit(0)
PY
}

for dir in "$@"; do
  for dll in "$dir"/**/QuerySpec.*.dll; do
    [[ -f "$dll" ]] || continue
    checked=$((checked + 1))
    if ! verify_one "$dll"; then
      echo "::error::$dll is not fully signed" >&2
      failures=$((failures + 1))
    fi
  done
done

if [[ "$checked" -eq 0 ]]; then
  echo "::error::no QuerySpec.*.dll found under: $*" >&2
  exit 1
fi

if [[ "$failures" -gt 0 ]]; then
  echo "::error::$failures of $checked assemblies failed strong-name verification" >&2
  exit 1
fi

echo "Strong-name verification passed: $checked assemblies fully signed."
