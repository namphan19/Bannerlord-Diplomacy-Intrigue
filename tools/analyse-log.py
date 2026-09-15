import re, sys, statistics, collections, pathlib

path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8", errors="replace").splitlines()

DAYS_PER_YEAR = 84          # four 21-day seasons
TARGET_DAYS = 3 * DAYS_PER_YEAR   # "under ~3 years" = 252 days

wars = []
snaps = []

for line in text:
    if "[WAR-ENDED]" in line:
        d = dict(re.findall(r"(\w+)=([^\s]+)", line.split("[WAR-ENDED]", 1)[1]))
        wars.append(d)
    elif "[SNAPSHOT]" in line:
        body = line.split("[SNAPSHOT]", 1)[1]
        # date= holds spaces; pull it out first, then parse the rest
        m = re.search(r"date=(.*?)\s+kingdoms=", body)
        date = m.group(1) if m else "?"
        d = dict(re.findall(r"(\w+)=([\-\d\.]+)", body[body.index("kingdoms="):]))
        d["date"] = date
        snaps.append(d)

print("=" * 66)
print("RUN SIZE")
print("=" * 66)
print(f"  weekly snapshots : {len(snaps)}   (~{len(snaps)*7/DAYS_PER_YEAR:.1f} in-game years)")
print(f"  wars ended       : {len(wars)}")
if snaps:
    print(f"  first snapshot   : {snaps[0]['date']}")
    print(f"  last snapshot    : {snaps[-1]['date']}")

# ---------------- war durations -------------------------------------------
print()
print("=" * 66)
print("WAR DURATION  (acceptance: mean under 252 days = 3 years)")
print("=" * 66)
durations = [int(w["days"]) for w in wars if "days" in w]
if durations:
    durations_sorted = sorted(durations)
    print(f"  count   : {len(durations)}")
    print(f"  mean    : {statistics.mean(durations):.1f} days  ({statistics.mean(durations)/DAYS_PER_YEAR:.2f} years)")
    print(f"  median  : {statistics.median(durations):.1f} days")
    print(f"  min/max : {min(durations)} / {max(durations)} days")
    verdict = "PASS" if statistics.mean(durations) < TARGET_DAYS else "FAIL"
    print(f"  VERDICT : {verdict}")
    print()
    buckets = [(0,0),(1,7),(8,30),(31,84),(85,168),(169,252),(253,504),(505,10**9)]
    labels  = ["0 days","1-7d","8-30d","1-3mo","3-6mo","6mo-3y","3-6y",">6y"]
    print("  distribution:")
    for (lo, hi), label in zip(buckets, labels):
        n = sum(1 for d in durations if lo <= d <= hi)
        if n == 0: continue
        bar = "#" * max(1, int(50 * n / len(durations)))
        print(f"    {label:>8}  {n:4d}  {bar}")

# ---------------- how wars started ----------------------------------------
print()
print("=" * 66)
print("CASUS BELLI of ended wars")
print("=" * 66)
for cb, n in collections.Counter(w.get("casusBelli", "?") for w in wars).most_common():
    print(f"  {cb:<24} {n:4d}   {100*n/len(wars):5.1f}%")

print()
print("  called in by an ally (vs chose the war):")
called = collections.Counter(w.get("calledBy", "none") for w in wars)
for k, n in called.most_common(6):
    print(f"    {k:<24} {n:4d}")

# ---------------- outcomes -------------------------------------------------
print()
print("=" * 66)
print("OUTCOMES")
print("=" * 66)
fiefs = [(int(w.get("fiefsTaken","0/0").split("/")[0]), int(w.get("fiefsTaken","0/0").split("/")[1]))
         for w in wars if "fiefsTaken" in w]
if fiefs:
    total_transfers = sum(a+b for a, b in fiefs)
    decisive = sum(1 for a, b in fiefs if a+b > 0)
    print(f"  wars with any fief change : {decisive} / {len(fiefs)}  ({100*decisive/len(fiefs):.1f}%)")
    print(f"  total fiefs changing hands: {total_transfers}")
scores = [float(w["finalScore"]) for w in wars if "finalScore" in w]
if scores:
    print(f"  final war score |mean|    : {statistics.mean(abs(s) for s in scores):.1f}")
    print(f"  white-ish (|score| < 5)   : {sum(1 for s in scores if abs(s) < 5)} / {len(scores)}")

# ---------------- world state over time ------------------------------------
print()
print("=" * 66)
print("WORLD STATE  (sampled every ~26 weeks)")
print("=" * 66)
if snaps:
    keys = ["kingdoms","atWar","wars","avgExhaustion","claims",
            "nonAggressionPact","truce","defensivePact","alliance","tributaryPact","vassalage"]
    hdr = f"  {'date':<20}" + "".join(f"{k[:9]:>11}" for k in keys)
    print(hdr)
    for i in range(0, len(snaps), 26):
        s = snaps[i]
        row = f"  {s['date']:<20}" + "".join(f"{s.get(k,'-'):>11}" for k in keys)
        print(row)
    s = snaps[-1]
    print(f"  {'FINAL':<20}" + "".join(f"{s.get(k,'-'):>11}" for k in keys))

# ---------------- the alliance question ------------------------------------
print()
print("=" * 66)
print("DID ALLIANCES EVER FORM?")
print("=" * 66)
if snaps:
    for key in ["alliance", "defensivePact", "nonAggressionPact", "tributaryPact", "vassalage"]:
        vals = [int(s.get(key, 0)) for s in snaps]
        print(f"  {key:<20} max={max(vals):3d}  weeks with any={sum(1 for v in vals if v>0):4d} / {len(vals)}")

# ---------------- permanent war check --------------------------------------
print()
print("=" * 66)
print("PERMANENT TOTAL WAR CHECK")
print("=" * 66)
if snaps:
    at_war = [int(s.get("atWar", 0)) for s in snaps]
    kingdoms = [int(s.get("kingdoms", 0)) for s in snaps]
    frac = [a / k if k else 0 for a, k in zip(at_war, kingdoms)]
    print(f"  kingdoms alive: first {kingdoms[0]}, last {kingdoms[-1]}, min {min(kingdoms)}")
    print(f"  share of kingdoms at war: mean {statistics.mean(frac)*100:.1f}%"
          f"  min {min(frac)*100:.1f}%  max {max(frac)*100:.1f}%")
    all_at_war = sum(1 for a, k in zip(at_war, kingdoms) if k and a == k)
    none_at_war = sum(1 for a in at_war if a == 0)
    print(f"  weeks with EVERY kingdom at war : {all_at_war} / {len(snaps)}  ({100*all_at_war/len(snaps):.1f}%)")
    print(f"  weeks with NOBODY at war        : {none_at_war} / {len(snaps)}  ({100*none_at_war/len(snaps):.1f}%)")
    exh = [float(s.get("avgExhaustion", 0)) for s in snaps]
    print(f"  average exhaustion across run   : {statistics.mean(exh):.1f}  (max {max(exh):.1f})")
