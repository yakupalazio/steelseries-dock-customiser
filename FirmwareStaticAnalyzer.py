"""Read-only Intel HEX inspection for the local Nova Pro Wireless transmitter firmware.

Never transmits HID packets, modifies firmware, or produces a flashable image.
Results are candidate clues, not verified firmware behavior.
"""
import argparse
import hashlib
import re
import struct
from pathlib import Path
from collections import Counter


def load_intel_hex(path):
    memory = {}
    base = 0
    counts = Counter()
    for line_number, raw in enumerate(path.read_text(encoding="ascii").splitlines(), 1):
        if not raw.startswith(":"):
            raise ValueError(f"Missing HEX prefix at line {line_number}")
        row = bytes.fromhex(raw[1:])
        if len(row) < 5 or sum(row) % 256:
            raise ValueError(f"Invalid HEX checksum at line {line_number}")
        length, addr_hi, addr_lo, kind = row[:4]
        if length + 5 != len(row):
            raise ValueError(f"Invalid HEX record length at line {line_number}")
        payload = row[4:-1]
        counts[kind] += 1
        address = addr_hi * 256 + addr_lo
        if kind == 0:
            for offset, value in enumerate(payload):
                where = base + address + offset
                if where in memory and memory[where] != value:
                    raise ValueError(f"Conflicting memory byte at 0x{where:08X}")
                memory[where] = value
        elif kind == 2:
            base = int.from_bytes(payload, "big") << 4
        elif kind == 4:
            base = int.from_bytes(payload, "big") << 16
        elif kind not in (1, 3, 5):
            raise ValueError(f"Unsupported HEX record type {kind}")
    return memory, counts


def contiguous_ranges(memory):
    ranges = []
    if not memory:
        return ranges
    start = last = min(memory)
    for address in sorted(memory):
        if address == start:
            continue
        if address != last + 1:
            ranges.append((start, last + 1))
            start = address
        last = address
    ranges.append((start, last + 1))
    return ranges


def candidates(memory, blob, start):
    needle_values = (5000, 1000, 500, 250, 100, 50)
    for number in needle_values:
        pattern = struct.pack("<I", number)
        offsets = []
        from_index = 0
        while len(offsets) < 50:
            offset = blob.find(pattern, from_index)
            if offset < 0:
                break
            addr = start + offset
            if all((addr+i) in memory for i in range(4)):
                offsets.append(addr)
            from_index = offset + 1
        shown = ", ".join(f"0x{x:08X}" for x in offsets[:12])
        print(f"  little-endian uint32 {number:>5}: found {len(offsets):>2}{'+' if len(offsets)==50 else ' '} | {shown or 'none'}")


def inspect_thumb_clues(memory,blob,base,address_ranges):
    try:
        import sys
        from pathlib import Path
        sys.path.insert(0,str(Path.home()/"AppData"/"Local"/"Temp"/
            "SteelseriesDockCustomiser"/"readonly-deps"))
        from capstone import Cs, CS_ARCH_ARM, CS_MODE_THUMB, CS_MODE_LITTLE_ENDIAN
        from capstone.arm import ARM_INS_LDR, ARM_OP_MEM, ARM_REG_PC
    except ImportError:
        print("Optional Capstone not present; skipping disassembly.")
        return
    from math import log2
    md=Cs(CS_ARCH_ARM, CS_MODE_THUMB+CS_MODE_LITTLE_ENDIAN)
    md.detail=True
    print("Section entropy and classification (conservative):")
    primary_range=None
    for start,end in address_ranges:
        chunk=blob[start-base:end-base]
        frequencies=Counter(chunk)
        entropy=-sum((n/len(chunk))*log2(n/len(chunk))
                     for n in frequencies.values())
        magic=chunk[:4].hex().upper()
        vector_ok=(len(chunk)>8 and
            0x20000000<=int.from_bytes(chunk[:4],"little")<=0x200FFFFF and
            start<=((int.from_bytes(chunk[4:8],"little"))&~1)<end)
        if vector_ok:
            primary_range=(start,end)
            label="ARM-vector-backed primary image"
        elif magic=="CAFEBABE":
            label="CA FE BA BE data container; code decoding untrusted"
        else:
            label="unclassified, do not assume executable"
        print(f"  0x{start:08X}..0x{end-1:08X}: {entropy:.3f} bits/byte; {label}")
    print("Thumb PC-relative LDR references to literal uint32(5000):")
    targets=[]
    for addr in range(base,base+len(blob)-4):
        if blob[addr-base:addr-base+4]==struct.pack("<I",5000):
            if all((addr+i) in memory for i in range(4)):
                targets.append(addr)
    for target in targets:
        if primary_range is None or not (primary_range[0]<=target<primary_range[1]):
            print(f"  target 0x{target:08X}: outside verified primary code image;"
                  " no executable cross-reference inferred")
            continue
        refs=[]
        for addr in range(max(primary_range[0],target-4200)&~1,target,2):
            if not all((addr+i) in memory for i in range(4)):
                continue
            decoded=list(md.disasm(blob[addr-base:addr-base+4],addr,count=1))
            if not decoded:continue
            ins=decoded[0]
            if (ins.id==ARM_INS_LDR and len(ins.operands)>=2 and
                ins.operands[1].type==ARM_OP_MEM and
                ins.operands[1].mem.base==ARM_REG_PC):
                literal=((ins.address+4)&~3)+ins.operands[1].mem.disp
                if literal==target:
                    refs.append(ins)
        print(f"  target 0x{target:08X}: {len(refs)} candidate literal-load(s)")
        for ins in refs[:10]:
            print(f"    0x{ins.address:08X} {ins.mnemonic} {ins.op_str}")
            # Decode forward from the candidate; not a verified function start.
            for ctx in md.disasm(blob[ins.address-base:ins.address-base+24],
                                 ins.address,count=6):
                print(f"       0x{ctx.address:08X} {ctx.mnemonic} {ctx.op_str}")
    print("Thumb comparison-byte candidates in vector-backed image only:")
    for imm in (0x93,0x95):
        occurrences=[]
        if primary_range is None:
            continue
        for addr in range(primary_range[0],primary_range[1]-2,2):
            if addr not in memory or addr+1 not in memory:
                continue
            half=blob[addr-base]|(blob[addr-base+1]<<8)
            if (half&0xF800)==0x2800 and (half&0xFF)==imm:
                occurrences.append(addr)
        print(f"  CMP immediate 0x{imm:02X}: {len(occurrences)} candidate(s)")
        for addr in occurrences[:12]:
            print(f"  0x{addr:08X}:")
            for ctx in md.disasm(blob[addr-base:addr-base+16],
                                 addr,count=4):
                print(f"    {ctx.mnemonic} {ctx.op_str}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("firmware", type=Path, help="Original Intel HEX file (read-only)")
    parser.add_argument("--focus", action="append", default=[],
                        help="Read-only Thumb disassembly starting at address (hex).")
    parser.add_argument("--span", type=int, default=120,
                        help="Maximum number of bytes to decode per focus.")
    args = parser.parse_args()
    original = args.firmware.read_bytes()
    memory, counts = load_intel_hex(args.firmware)
    address_ranges = contiguous_ranges(memory)
    print("File:", args.firmware.name)
    print("SHA256:", hashlib.sha256(original).hexdigest())
    print("Intel HEX record types:", dict(sorted(counts.items())))
    print("Populated firmware bytes:", len(memory))
    print("Continuous mapped ranges:")
    for start, end in sorted(address_ranges, key=lambda t: t[1]-t[0], reverse=True)[:15]:
        print(f"  0x{start:08X}..0x{end-1:08X}, {end-start} bytes")
    base = min(memory)
    end = max(memory)+1
    if end-base > 32_000_000:
        raise ValueError("Firmware spans more than 32 MB; refusing dense scan")
    blob = bytes(memory.get(addr, 0xFF) for addr in range(base, end))
    print("Image window:", f"0x{base:08X}..0x{end-1:08X}")
    if all((base+i) in memory for i in range(0,64)):
        vectors = struct.unpack_from("<16I",blob)
        print("Vector table: initial SP", f"0x{vectors[0]:08X}",
              "reset PC", f"0x{vectors[1]:08X}",
              "(odd PC indicates ARM Thumb entry)")
    if args.focus:
        import sys
        sys.path.insert(0,str(Path.home()/"AppData"/"Local"/"Temp"/
            "SteelseriesDockCustomiser"/"readonly-deps"))
        from capstone import Cs, CS_ARCH_ARM, CS_MODE_THUMB, CS_MODE_LITTLE_ENDIAN
        md=Cs(CS_ARCH_ARM,CS_MODE_THUMB+CS_MODE_LITTLE_ENDIAN)
        size=min(512,max(16,args.span))
        for value in args.focus:
            at=int(value,0)
            print(f"--- Raw Thumb hypothesis at 0x{at:08X}, {size} bytes ---")
            print("WARNING: output might decode container data as fake instructions.")
            if at<base or at+size>base+len(blob) or any(
                    addr not in memory for addr in range(at,at+size)):
                print("Unavailable mapped address window")
                continue
            chunk=blob[at-base:at-base+size]
            print("Head bytes:",chunk[:32].hex(" "))
            for ins in md.disasm(chunk,at):
                print(f"  0x{ins.address:08X} {ins.mnemonic:<8} {ins.op_str}")
        return
    print("Embedded ASCII strings (6+ printable bytes; first 30):")
    strings = []
    for match in re.finditer(rb"[\x20-\x7e]{6,}",blob):
        addr = base+match.start()
        if all((addr+i) in memory for i in range(len(match.group()))):
            strings.append((addr,match.group().decode("ascii", errors="replace")))
    for addr, value in strings[:30]:
        print(f"  0x{addr:08X} {value[:96]}")
    print("Candidate UI/USB strings:")
    relevant = [(addr,value) for addr,value in strings
        if any(word in value.casefold() for word in
               ("oled","chatmix","volume","screen","display","timeout",
                "usb","report","firmware","menu","steelseries"))]
    for addr,value in relevant[:35]:
        print(f"  0x{addr:08X} {value[:100]}")
    print("  total matching strings:",len(relevant))
    print("Possible timing constants (not proof of overlay timer):")
    candidates(memory,blob,base)
    inspect_thumb_clues(memory,blob,base,address_ranges)
    print("Analysis is read-only. Address coincidences require disassembly and call-graph verification.")


if __name__ == "__main__":
    main()
