"""Resident-memory footprint harness for the pocket-tts sidecar (main-r8k3w).

Measures where the Python sidecar's standing RAM goes, stage by stage, so the
RAM-cutting levers in main-r8k3w can be judged against numbers instead of
intuition. Mirrors the real load path: utterheim_sidecar `serve` builds
`_RESIDENT_MODELS` by calling `pocket_tts.TTSModel.load_model(language=...)`
once per language (main-039 / ADR 0024).

Why ctypes and not psutil: the embeddable runtime is deliberately zero-dep and
version-checked by the bootstrapper (main-027); psutil is NOT installed and we
must not pip-install into it. So we read working set + commit straight from the
Win32 `GetProcessMemoryInfo` API.

Usage (run with the embeddable interpreter):

    set HF_HOME=%LOCALAPPDATA%\\Utterheim\\models\\pocket-tts
    set HF_HUB_OFFLINE=1
    set PYTHONIOENCODING=utf-8
    python measure_footprint.py --languages english german          # production (ADR 0025)
    python measure_footprint.py --languages english german_24l      # pre-revert arm (main-r8k3w)
    python measure_footprint.py --languages english german --quantize
    python measure_footprint.py --pid 12345                          # probe a live process

The staged single-process run records WorkingSetSize (~RSS) and PagefileUsage
(~commit) after: bare interpreter -> import torch -> each model load. It then
prints the per-model state_dict breakdown (flow_lm vs Mimi) and checks whether
the two models' Mimi codecs are bit-identical (gates the shared-codec lever).
"""

from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as wt
import gc
import sys
import time


# --- Win32 memory probe (no psutil) -----------------------------------------


class PROCESS_MEMORY_COUNTERS(ctypes.Structure):
    _fields_ = [
        ("cb", wt.DWORD),
        ("PageFaultCount", wt.DWORD),
        ("PeakWorkingSetSize", ctypes.c_size_t),
        ("WorkingSetSize", ctypes.c_size_t),
        ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPagedPoolUsage", ctypes.c_size_t),
        ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
        ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
        ("PagefileUsage", ctypes.c_size_t),
        ("PeakPagefileUsage", ctypes.c_size_t),
    ]


_psapi = ctypes.WinDLL("psapi", use_last_error=True)
_kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

# Pin return/arg types so the GetCurrentProcess pseudo-handle (-1) and real
# handles are not truncated to a 32-bit int on win64.
_kernel32.GetCurrentProcess.restype = wt.HANDLE
_kernel32.OpenProcess.restype = wt.HANDLE
_kernel32.OpenProcess.argtypes = [wt.DWORD, wt.BOOL, wt.DWORD]
_kernel32.CloseHandle.argtypes = [wt.HANDLE]
_psapi.GetProcessMemoryInfo.argtypes = [
    wt.HANDLE,
    ctypes.c_void_p,
    wt.DWORD,
]
_psapi.GetProcessMemoryInfo.restype = wt.BOOL

PROCESS_QUERY_INFORMATION = 0x0400
PROCESS_VM_READ = 0x0010


def _counters_for_handle(handle) -> PROCESS_MEMORY_COUNTERS:
    counters = PROCESS_MEMORY_COUNTERS()
    counters.cb = ctypes.sizeof(PROCESS_MEMORY_COUNTERS)
    ok = _psapi.GetProcessMemoryInfo(
        handle, ctypes.byref(counters), counters.cb
    )
    if not ok:
        raise ctypes.WinError(ctypes.get_last_error())
    return counters


def mem_self() -> tuple[int, int]:
    """(working_set_bytes, commit_bytes) for the current process."""
    handle = _kernel32.GetCurrentProcess()
    c = _counters_for_handle(handle)
    return c.WorkingSetSize, c.PagefileUsage


def mem_pid(pid: int) -> tuple[int, int]:
    """(working_set_bytes, commit_bytes) for an arbitrary pid."""
    handle = _kernel32.OpenProcess(
        PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, pid
    )
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        c = _counters_for_handle(handle)
        return c.WorkingSetSize, c.PagefileUsage
    finally:
        _kernel32.CloseHandle(handle)


def mb(n: int) -> float:
    return n / (1024 * 1024)


# --- reporting --------------------------------------------------------------


class Recorder:
    def __init__(self) -> None:
        self.rows: list[tuple[str, int, int]] = []

    def mark(self, label: str) -> None:
        gc.collect()
        ws, commit = mem_self()
        self.rows.append((label, ws, commit))

    def print_table(self) -> None:
        print()
        print(f"{'stage':<34}{'working set':>14}{'(delta)':>12}{'commit':>14}")
        print("-" * 74)
        prev_ws = 0
        for label, ws, commit in self.rows:
            delta = ws - prev_ws
            print(
                f"{label:<34}{mb(ws):>11.1f} MB{mb(delta):>+9.1f} MB"
                f"{mb(commit):>11.1f} MB"
            )
            prev_ws = ws


def report_state_dicts(models: dict[str, object]) -> None:
    from pocket_tts.utils.utils import size_of_dict

    print()
    print("Per-model state_dict breakdown (in-memory tensor bytes):")
    print(f"{'language':<16}{'flow_lm':>14}{'mimi':>14}{'total':>14}")
    print("-" * 58)
    mimi_dicts: dict[str, dict] = {}
    for lang, model in models.items():
        flow = size_of_dict(model.flow_lm.state_dict())
        mimi = size_of_dict(model.mimi.state_dict())
        total = size_of_dict(model.state_dict())
        mimi_dicts[lang] = model.mimi.state_dict()
        print(
            f"{lang:<16}{mb(flow):>11.1f} MB{mb(mimi):>11.1f} MB{mb(total):>11.1f} MB"
        )

    # Shared-codec lever gate: are the two Mimi codecs bit-identical?
    langs = list(mimi_dicts.keys())
    if len(langs) >= 2:
        import torch

        a, b = mimi_dicts[langs[0]], mimi_dicts[langs[1]]
        identical = a.keys() == b.keys() and all(
            torch.equal(a[k], b[k]) for k in a.keys()
        )
        print()
        print(
            f"Mimi codec identical across {langs[0]} / {langs[1]}: {identical} "
            f"(gates the shared-codec lever)"
        )


def run_staged(languages: list[str], quantize: bool) -> None:
    rec = Recorder()
    rec.mark("0. bare interpreter")

    import torch  # noqa: F401

    rec.mark("1. import torch")

    from pocket_tts import TTSModel

    rec.mark("2. import pocket_tts")

    models: dict[str, object] = {}
    for lang in languages:
        t0 = time.monotonic()
        model = TTSModel.load_model(language=lang, quantize=quantize)
        dt = time.monotonic() - t0
        models[lang] = model
        suffix = " +quant" if quantize else ""
        rec.mark(f"3. load {lang}{suffix} ({dt:.1f}s)")

    rec.print_table()
    report_state_dicts(models)
    print()
    print(f"quantize={quantize}  torch={torch.__version__}")
    if quantize:
        from pocket_tts.quantization import _get_backend

        print(f"quant backend selected: {_get_backend()}")


def main() -> int:
    parser = argparse.ArgumentParser(description="pocket-tts sidecar RAM footprint probe")
    parser.add_argument(
        "--languages",
        nargs="+",
        default=["english", "german"],
        help="languages to load, in order (mirrors serve --language)",
    )
    parser.add_argument(
        "--quantize",
        action="store_true",
        help="apply int8 dynamic quantization (load_model(quantize=True))",
    )
    parser.add_argument(
        "--pid",
        type=int,
        default=None,
        help="probe a live process's working set/commit and exit",
    )
    args = parser.parse_args()

    if args.pid is not None:
        ws, commit = mem_pid(args.pid)
        print(f"pid {args.pid}: working set {mb(ws):.1f} MB, commit {mb(commit):.1f} MB")
        return 0

    run_staged(args.languages, args.quantize)
    return 0


if __name__ == "__main__":
    sys.exit(main())
