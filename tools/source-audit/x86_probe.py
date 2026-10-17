#!/usr/bin/env python3
"""Read-only i386 PE probe: string cross-references and annotated function disassembly.

Fallback for when no decompiler backend (Ghidra/IDA/Hopper) is available. Linear-sweep
disassembly with capstone; function boundaries are HEURISTIC (direct call targets plus
``push ebp; mov ebp, esp`` prologues after padding), so treat function starts as approximate.

Usage:
  python -I x86_probe.py <pe> info
  python -I x86_probe.py <pe> str  "<literal>" [...]          # find string VAs
  python -I x86_probe.py <pe> xref "<literal>" [...]          # code sites referencing them
  python -I x86_probe.py <pe> xrefva 0xVA [...]               # code sites referencing a VA
  python -I x86_probe.py <pe> func 0xVA [...]                 # annotated disassembly of function containing VA
  python -I x86_probe.py <pe> callers 0xFUNC_VA               # direct callers
  python -I x86_probe.py <pe> imports                         # IAT slot -> name
  python -I x86_probe.py <pe> bytes 0xVA <len>
"""
from __future__ import annotations

import bisect
import re
import struct
import sys

import pefile
from capstone import CS_ARCH_X86, CS_MODE_32, CS_OP_IMM, CS_OP_MEM, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM


class Probe:
    def __init__(self, path: str) -> None:
        self.pe = pefile.PE(path)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.data = open(path, "rb").read()
        self.sections = []
        for s in self.pe.sections:
            name = s.Name.rstrip(b"\0").decode(errors="replace")
            va = self.base + s.VirtualAddress
            self.sections.append((name, va, va + max(s.Misc_VirtualSize, s.SizeOfRawData),
                                  s.PointerToRawData, s.SizeOfRawData, s.Characteristics))
        self.iat = {}
        for dll in getattr(self.pe, "DIRECTORY_ENTRY_IMPORT", []):
            for imp in dll.imports:
                n = imp.name.decode() if imp.name else f"#{imp.ordinal}"
                self.iat[imp.address] = f"{dll.dll.decode()}!{n}"
        self.md = Cs(CS_ARCH_X86, CS_MODE_32)
        self.md.detail = True
        self._insns = None
        self._func_starts = None

    # ---- address helpers
    def va2off(self, va: int) -> int | None:
        for _, lo, hi, raw, rawsz, _ in self.sections:
            if lo <= va < lo + rawsz:
                return raw + (va - lo)
        return None

    def off2va(self, off: int) -> int | None:
        for _, lo, hi, raw, rawsz, _ in self.sections:
            if raw <= off < raw + rawsz:
                return lo + (off - raw)
        return None

    def read(self, va: int, n: int) -> bytes:
        off = self.va2off(va)
        if off is None:
            return b""
        return self.data[off:off + n]

    def cstr(self, va: int, maxlen: int = 120) -> str | None:
        b = self.read(va, maxlen)
        if not b:
            return None
        end = b.find(b"\0")
        if end < 0:
            return None
        s = b[:end]
        if len(s) >= 3 and all(0x20 <= c < 0x7F or c in (9, 10, 13) for c in s):
            return s.decode()
        return None

    # ---- text section
    def text(self):
        for name, lo, hi, raw, rawsz, ch in self.sections:
            if ch & 0x20000000:  # IMAGE_SCN_MEM_EXECUTE
                return lo, self.data[raw:raw + rawsz]
        raise RuntimeError("no executable section")

    def insns(self):
        if self._insns is None:
            lo, code = self.text()
            lst = list(self.md.disasm(code, lo))
            # capstone stops at an undecodable byte; resume after it
            pos = (lst[-1].address + lst[-1].size - lo) if lst else 0
            while pos < len(code):
                more = list(self.md.disasm(code[pos + 1:], lo + pos + 1))
                if not more:
                    break
                lst.extend(more)
                pos = more[-1].address + more[-1].size - lo
            self._insns = lst
            self._addr_index = [i.address for i in lst]
        return self._insns

    def func_starts(self):
        if self._func_starts is None:
            starts = set()
            ins = self.insns()
            for i in ins:
                if i.mnemonic == "call" and i.operands and i.operands[0].type == X86_OP_IMM:
                    starts.add(i.operands[0].imm)
            # prologue after padding (int3 / ret / nop)
            prev = None
            for i in ins:
                if i.mnemonic == "push" and i.op_str == "ebp" and prev is not None and \
                        prev.mnemonic in ("int3", "ret", "nop", "jmp"):
                    starts.add(i.address)
                prev = i
            self._func_starts = sorted(starts)
        return self._func_starts

    def func_of(self, va: int) -> int | None:
        fs = self.func_starts()
        k = bisect.bisect_right(fs, va) - 1
        return fs[k] if k >= 0 else None

    def func_end(self, start: int) -> int:
        fs = self.func_starts()
        k = bisect.bisect_right(fs, start)
        return fs[k] if k < len(fs) else self.text()[0] + len(self.text()[1])

    # ---- queries
    def find_strings(self, lit: str):
        pat = lit.encode()
        out = []
        for m in re.finditer(re.escape(pat), self.data):
            off = m.start()
            # require NUL-terminated and preceded by NUL (full string, not substring)
            if self.data[off + len(pat):off + len(pat) + 1] == b"\0" and self.data[off - 1:off] == b"\0":
                va = self.off2va(off)
                if va:
                    out.append((off, va))
        return out

    def xrefs_to(self, va: int):
        sites = []
        for i in self.insns():
            hit = False
            for op in i.operands:
                if op.type == X86_OP_IMM and op.imm == va:
                    hit = True
                elif op.type == X86_OP_MEM and op.mem.disp == va and op.mem.base == 0 and op.mem.index == 0:
                    hit = True
                elif op.type == X86_OP_MEM and op.mem.disp == va:
                    hit = True
            if hit:
                sites.append(i)
        return sites

    def annotate(self, i) -> str:
        notes = []
        for op in i.operands:
            if op.type == X86_OP_IMM:
                s = self.cstr(op.imm)
                if s is not None:
                    notes.append(f'"{s}"')
            elif op.type == X86_OP_MEM:
                if op.mem.disp in self.iat and op.mem.base == 0:
                    notes.append(self.iat[op.mem.disp])
                else:
                    s = self.cstr(op.mem.disp) if op.mem.base == 0 and op.mem.index == 0 else None
                    if s is not None:
                        notes.append(f'"{s}"')
        if i.mnemonic == "call" and i.operands and i.operands[0].type == X86_OP_IMM:
            notes.append(f"sub_{i.operands[0].imm:08X}")
        return ("    ; " + " | ".join(notes)) if notes else ""

    def dump_func(self, start: int, end: int | None = None) -> None:
        end = end or self.func_end(start)
        ins = self.insns()
        k = bisect.bisect_left(self._addr_index, start)
        print(f"==== sub_{start:08X} (file off 0x{self.va2off(start):X}) .. 0x{end:08X}")
        while k < len(ins) and ins[k].address < end:
            i = ins[k]
            print(f"{i.address:08X}  {i.bytes.hex():<20} {i.mnemonic:<7} {i.op_str}{self.annotate(i)}")
            k += 1


def main(argv):
    p = Probe(argv[1])
    cmd = argv[2]
    args = argv[3:]
    if cmd == "info":
        print(f"ImageBase=0x{p.base:X}")
        for name, lo, hi, raw, rawsz, ch in p.sections:
            print(f"  {name:<8} VA 0x{lo:08X}-0x{hi:08X} raw 0x{raw:X}+0x{rawsz:X} ch=0x{ch:08X}")
        print(f"  text insns={len(p.insns())} func_starts={len(p.func_starts())}")
    elif cmd == "str":
        for lit in args:
            for off, va in p.find_strings(lit):
                print(f"{lit!r}: file 0x{off:X} VA 0x{va:08X}")
    elif cmd in ("xref", "xrefva"):
        targets = []
        for a in args:
            if cmd == "xrefva":
                targets.append((a, int(a, 16)))
            else:
                targets += [(a, va) for _, va in p.find_strings(a)]
        for label, va in targets:
            sites = p.xrefs_to(va)
            print(f"== {label!r} VA 0x{va:08X}: {len(sites)} site(s)")
            for i in sites:
                f = p.func_of(i.address)
                print(f"   {i.address:08X} {i.mnemonic} {i.op_str}   in sub_{f:08X}" if f else
                      f"   {i.address:08X} {i.mnemonic} {i.op_str}   (no function)")
    elif cmd == "func":
        for a in args:
            va = int(a, 16)
            f = p.func_of(va)
            p.dump_func(f)
    elif cmd == "callers":
        va = int(args[0], 16)
        for i in p.insns():
            if i.mnemonic == "call" and i.operands and i.operands[0].type == X86_OP_IMM and i.operands[0].imm == va:
                print(f"   {i.address:08X} in sub_{p.func_of(i.address):08X}")
    elif cmd == "imports":
        for slot, name in sorted(p.iat.items()):
            print(f"  [0x{slot:08X}] {name}")
    elif cmd == "ptrtab":
        va, n = int(args[0], 16), int(args[1])
        for k in range(n):
            ptr = struct.unpack_from("<I", p.read(va + 4 * k, 4))[0]
            print(f"  [{k:2d}] 0x{va + 4 * k:08X} -> 0x{ptr:08X} {p.cstr(ptr)!r}")
    elif cmd == "imm":
        vals = {int(a, 16) for a in args}
        for i in p.insns():
            for op in i.operands:
                if op.type == X86_OP_IMM and (op.imm & 0xFFFFFFFF) in vals:
                    f = p.func_of(i.address)
                    print(f"   {i.address:08X} {i.mnemonic} {i.op_str}   in sub_{f:08X}")
    elif cmd == "rawref":
        # search the whole file for little-endian pointers to the given VAs
        for a in args:
            va = int(a, 16)
            needle = struct.pack("<I", va)
            print(f"== 0x{va:08X} ({p.cstr(va)!r})")
            for m in re.finditer(re.escape(needle), p.data):
                off = m.start()
                at = p.off2va(off)
                f = p.func_of(at) if at else None
                sec = next((s[0] for s in p.sections if s[3] <= off < s[3] + s[4]), "?")
                print(f"   file 0x{off:X} VA 0x{at:08X} [{sec}]" + (f" in sub_{f:08X}" if sec == ".text" and f else ""))
    elif cmd == "bytes":
        va, n = int(args[0], 16), int(args[1])
        b = p.read(va, n)
        for k in range(0, len(b), 16):
            chunk = b[k:k + 16]
            print(f"{va + k:08X}  {chunk.hex(' '):<48} {''.join(chr(c) if 32 <= c < 127 else '.' for c in chunk)}")
    else:
        print(__doc__)


if __name__ == "__main__":
    main(sys.argv)
