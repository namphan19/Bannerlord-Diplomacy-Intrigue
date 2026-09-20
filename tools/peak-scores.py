"""Reconstruct peak |war score| per war from weekly [WAR] telemetry.

The logs record a war's *current* score every week and its *final* score when it ends.
Nobody has measured the peak, which is the number that decides whether the peace table's
top rungs are reachable at all: the score bleeds toward zero while exhaustion builds, so a
war is settled far below what it was worth at its height.

Wars are segmented per (aggressor, defender) pair: a new war starts whenever the reported
`days` figure drops, which is what happens when the pair fights again.
"""
import re
import sys
import collections

WAR = re.compile(r"\[WAR\] .*?aggressor=(\S+) defender=(\S+) days=([\d.]+) .*?score=(-?[\d.]+)")
ENDED = re.compile(r"\[WAR-ENDED\] .*?aggressor=(\S+) defender=(\S+) days=(\d+) .*?finalScore=(-?[\d.]+)")


def main(paths):
    # pair -> list of segments; each segment is [peak_abs, last_days, final_or_None]
    segments = collections.defaultdict(list)

    for path in paths:
        with open(path, encoding="utf-8", errors="replace") as handle:
            for line in handle:
                m = WAR.search(line)
                if m:
                    a, d, days, score = m.group(1), m.group(2), float(m.group(3)), abs(float(m.group(4)))
                    key = (a, d)
                    segs = segments[key]
                    if not segs or days < segs[-1][1] or segs[-1][2] is not None:
                        segs.append([score, days, None])
                    else:
                        segs[-1][0] = max(segs[-1][0], score)
                        segs[-1][1] = days
                    continue

                m = ENDED.search(line)
                if m:
                    a, d, final = m.group(1), m.group(2), abs(float(m.group(4)))
                    segs = segments[(a, d)]
                    if segs and segs[-1][2] is None:
                        segs[-1][0] = max(segs[-1][0], final)
                        segs[-1][2] = final
                    else:
                        segs.append([final, float(m.group(3)), final])

    peaks, finals, both = [], [], []
    for (a, d), segs in segments.items():
        for peak, _days, final in segs:
            peaks.append(peak)
            if final is not None:
                finals.append(final)
                both.append((peak, final, a, d))

    if not peaks:
        print("no [WAR] telemetry found")
        return

    peaks.sort()
    print(f"wars observed (segments)      : {len(peaks)}")
    print(f"of those, observed to the end : {len(finals)}")
    print()
    print("PEAK |score| distribution")
    bands = [(0, 20), (20, 40), (40, 60), (60, 80), (80, 95), (95, 100), (100, 130), (130, 10**6)]
    for lo, hi in bands:
        n = sum(1 for p in peaks if lo <= p < hi)
        if not n:
            continue
        label = f"{lo}-{hi}" if hi < 10**6 else f"{lo}+"
        print(f"  {label:>8}  {n:3d}  {'#' * n}")
    print()
    print(f"  max peak observed : {max(peaks):.1f}")
    print(f"  median peak       : {peaks[len(peaks) // 2]:.1f}")
    print(f"  peaks >= 95       : {sum(1 for p in peaks if p >= 95)} / {len(peaks)}")
    print(f"  peaks >= 130      : {sum(1 for p in peaks if p >= 130)} / {len(peaks)}")

    if both:
        print()
        print("BLEED between peak and settlement (wars seen to the end)")
        drops = sorted((p - f, p, f, a, d) for p, f, a, d in both)
        mean = sum(x[0] for x in drops) / len(drops)
        print(f"  mean drop   : {mean:.1f} points")
        print(f"  median drop : {drops[len(drops) // 2][0]:.1f} points")
        print("  largest drops:")
        for drop, p, f, a, d in drops[-6:][::-1]:
            print(f"    {a} vs {d}: peaked {p:.1f}, settled {f:.1f}  (-{drop:.1f})")


if __name__ == "__main__":
    main(sys.argv[1:])
