"""
Turns one balance run into the numbers the design is judged on.

    python tools/analyse-log.py run.log [more.log ...]

A run is often several logs: every game restart opens a new one, and a run resumed from a save
made midway repeats the days after that save. Logs are read in the order given, and when a log
starts on a day earlier than the previous one reached, everything the previous logs recorded
from that day on is discarded - the resumed branch replaces it. Pass the logs oldest first.

Records it reads (all `[KIND] key=value`, one per line):
  [SNAPSHOT] weekly world totals     [KINGDOM] weekly, one per kingdom
  [LINK]     weekly, one per vassal  [WAR]     weekly, one per ongoing war
  [WAR-ENDED] per war                [EVENT]   per decision or engine event
  [RUN] / [CONFIG] once per session: the build and every constant
"""
import re, sys, statistics, collections, pathlib

DAYS_PER_YEAR = 84          # four 21-day seasons
TARGET_DAYS = 3 * DAYS_PER_YEAR   # "under ~3 years" = 252 days
SEASONS = {"Spring": 0, "Summer": 1, "Autumn": 2, "Winter": 3}


def date_to_day(text):
    """'Summer 8, 1139' or 'Summer_8;_1139' -> an absolute day, or None."""
    m = re.match(r"(Spring|Summer|Autumn|Winter)[ _](\d+)[,;]?[ _](\d+)", text or "")
    if not m:
        return None
    return int(m.group(3)) * DAYS_PER_YEAR + SEASONS[m.group(1)] * 21 + int(m.group(2)) - 1


def kv(body):
    return dict(re.findall(r"(\w+)=([^\s]+)", body))


records = []     # (day, kind, dict)
configs = []     # (file, {name: value})
runs = []        # [RUN] dicts

for name in sys.argv[1:]:
    lines = pathlib.Path(name).read_text(encoding="utf-8", errors="replace").splitlines()
    current_day = None
    file_records = []
    file_config = {}
    for line in lines:
        m = re.search(r"\[(SNAPSHOT|KINGDOM|LINK|WAR|WAR-ENDED|EVENT|RUN|CONFIG)\]", line)
        if not m:
            continue
        kind = m.group(1)
        body = line[m.end():]
        if kind == "CONFIG":
            file_config.update(kv(body))
            continue
        if kind == "SNAPSHOT":
            dm = re.search(r"date=(.*?)\s+kingdoms=", body)
            d = kv(body[body.index("kingdoms="):]) if "kingdoms=" in body else {}
            d["date"] = dm.group(1) if dm else "?"
            day = date_to_day(d["date"])
        else:
            d = kv(body)
            day = int(d["day"]) if "day" in d else date_to_day(d.get("date", "").replace("_", " "))
        if day is None:
            day = current_day          # old WAR-ENDED lines carry no date
        else:
            current_day = day
        if kind == "RUN":
            runs.append(d)
            continue
        file_records.append((day, kind, d))
    if file_records:
        first = next((r[0] for r in file_records if r[0] is not None), None)
        if first is not None:
            records = [r for r in records if r[0] is None or r[0] < first]
    records.extend(file_records)
    if file_config:
        configs.append((name, file_config))

wars = [d for _, k, d in records if k == "WAR-ENDED"]
snaps = [d for _, k, d in records if k == "SNAPSHOT"]
# A session launch writes a baseline snapshot; keep one snapshot per date.
_seen = {}
for s_ in snaps:
    _seen[s_["date"]] = s_
snaps = sorted(_seen.values(), key=lambda x: date_to_day(x["date"]) or 0)
kingdom_rows = [(day, d) for day, k, d in records if k == "KINGDOM"]
link_rows = [(day, d) for day, k, d in records if k == "LINK"]
events = [(day, d) for day, k, d in records if k == "EVENT"]

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

# Report the two populations first. A war a kingdom chose and a war it was
# dragged into by an ally are different things; averaging them hid the number
# that mattered in run 01.
chosen = [w for w in wars if w.get("calledBy", "none") == "none"]
oblig  = [w for w in wars if w.get("calledBy", "none") != "none"]
for label, group in [("CHOSEN wars      ", chosen), ("OBLIGATION wars  ", oblig)]:
    if not group: continue
    ds = [int(w["days"]) for w in group]
    print(f"  {label} n={len(group):4d}  mean={statistics.mean(ds):6.1f}d"
          f"  median={statistics.median(ds):5.1f}d  max={max(ds):4d}d"
          f"  ({statistics.mean(ds)/DAYS_PER_YEAR:.2f} years)")
print()

if any("endedBy" in w for w in wars):
    print("  who ended them:")
    for k, n in collections.Counter(w.get("endedBy", "unrecorded") for w in wars).most_common():
        print(f"    {k:<20} {n:4d}   {100*n/len(wars):5.1f}%")
    print()
    print("  terms conceded:")
    for k, n in collections.Counter(w.get("terms", "unrecorded") for w in wars).most_common(8):
        print(f"    {k:<28} {n:4d}")
    print()
else:
    print("  (endedBy/terms absent - log predates the v2 telemetry)")
    print()

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


# ================= Everything below needs the 2026-09-17 telemetry =============
def section(title):
    print()
    print("=" * 66)
    print(title)
    print("=" * 66)


def f(x, default=0.0):
    try:
        return float(x)
    except (TypeError, ValueError):
        return default


if runs or configs:
    section("RUN")
    for r in runs:
        print(f"  session at {r.get('date','?'):<16} mod {r.get('mod','?')} built {r.get('built','?')}"
              f"  aggressiveness {r.get('aggressiveness','?')}  player {r.get('player','?')}")
    if len(configs) > 1:
        base = configs[0][1]
        for name, cfg in configs[1:]:
            changed = {k: (base.get(k), v) for k, v in cfg.items() if base.get(k) != v}
            if changed:
                print(f"  CONSTANTS CHANGED in {name}: {changed}")

if kingdom_rows:
    section("POWER  (yearly: live dominance / smoothed / greed / fortifications / vassals)")
    by_year = collections.defaultdict(dict)
    for day, d in kingdom_rows:
        by_year[day // DAYS_PER_YEAR][d["kingdom"]] = d
    names = sorted({d["kingdom"] for _, d in kingdom_rows})
    for year in sorted(by_year):
        row = by_year[year]
        cells = []
        for n in names:
            d = row.get(n)
            if d is None:
                cells.append(f"{n[:14]:<14} gone")
                continue
            forts = int(f(d.get("towns"))) + int(f(d.get("castles")))
            cells.append(f"{n[:14]:<14} {f(d.get('dominance')):4.2f}/{f(d.get('smoothedDominance')):4.2f}"
                         f" g{f(d.get('greed')):3.1f} f{forts:2d} v{int(f(d.get('vassals')))}")
        print(f"  year {year}:")
        for c in cells:
            print(f"      {c}")

    section("TOP KINGDOM OVER TIME")
    top = collections.Counter()
    for year in sorted(by_year):
        best = max(by_year[year].values(), key=lambda d: f(d.get("dominance")))
        top[best["kingdom"]] += 1
        print(f"  year {year}: {best['kingdom']:<18} dominance {f(best.get('dominance')):.2f}"
              f"  smoothed {f(best.get('smoothedDominance')):.2f}  greed {f(best.get('greed')):.2f}")

    section("AI MOVES  (weekly evaluations, per kingdom)")
    moves = collections.defaultdict(collections.Counter)
    for _, d in kingdom_rows:
        moves[d["kingdom"]][d.get("lastMove", "?")] += 1
    for n in names:
        print(f"  {n:<18} " + ", ".join(f"{m}={c}" for m, c in moves[n].most_common()))

if events:
    counts = collections.Counter(d.get("kind") for _, d in events)
    section("EVENTS  (counts)")
    for k, n in counts.most_common():
        print(f"  {k:<26} {n:5d}")

    section("HEGEMONY TIMELINE")
    wanted = {"vassalage_formed", "poach", "revolt", "annexation_breach", "vassalage_collapsed",
              "vassalage_renewed", "kingdom_eliminated", "ai_war_declared", "defection"}
    for day, d in events:
        k = d.get("kind")
        if k not in wanted:
            continue
        if k == "ai_war_declared" and d.get("annexation") != "true":
            continue
        date = d.get("date", "?")
        if k == "vassalage_formed":
            print(f"  {date:<18} VASSAL   {d.get('vassal')} -> {d.get('patron')} ({d.get('route')}, value {d.get('value')}, hold {d.get('startHold')})")
        elif k == "poach":
            print(f"  {date:<18} POACH    {d.get('suitor')} took {d.get('vassal')} from {d.get('oldPatron')} (war opened {d.get('warOpened')})")
        elif k == "revolt":
            print(f"  {date:<18} REVOLT   against {d.get('patron')}: {d.get('rebels')} (at war: {d.get('atWar')})")
        elif k == "annexation_breach":
            print(f"  {date:<18} ANNEX    {d.get('patron')} turned on {d.get('vassal')} (greed {d.get('greed')})")
        elif k == "ai_war_declared":
            print(f"  {date:<18} ANNEXWAR {d.get('kingdom')} -> {d.get('target')} value {d.get('value')} opened {d.get('opened')}")
        elif k == "vassalage_collapsed":
            print(f"  {date:<18} COLLAPSE {d.get('vassal')} / {d.get('patron')}")
        elif k == "vassalage_renewed":
            print(f"  {date:<18} RENEWED  {d.get('vassal')} -> {d.get('patron')} at hold {d.get('hold')}")
        elif k == "defection":
            print(f"  {date:<18} DEFECT   {d.get('vassal')} left {d.get('oldPatron')} for its attacker {d.get('newPatron')} (hold {d.get('hold')}, signed {d.get('signed')})")
        elif k == "kingdom_eliminated":
            print(f"  {date:<18} GONE     {d.get('kingdom')} ({d.get('warsClosed')} wars closed)")

    section("FIEFS  (fortifications gained minus lost, by kingdom)")
    net = collections.Counter()
    moved = 0
    for _, d in events:
        if d.get("kind") != "fief_changed":
            continue
        moved += 1
        if d.get("from") not in (None, "none"):
            net[d["from"]] -= 1
        if d.get("to") not in (None, "none"):
            net[d["to"]] += 1
    print(f"  fortifications changing hands: {moved}")
    for k, n in sorted(net.items(), key=lambda x: -x[1]):
        print(f"    {k:<20} {n:+d}")

    section("COALITIONS  (call to arms by role and outcome)")
    cta = collections.Counter((d.get("role"), d.get("outcome")) for _, d in events if d.get("kind") == "call_to_arms")
    for (role, outcome), n in sorted(cta.items()):
        print(f"  {role:<8} {outcome:<9} {n:5d}")
    pulled = [d for _, d in events if d.get("kind") == "ai_pact_signed"]
    if pulled:
        with_pull = [d for d in pulled if f(d.get("balancingPull")) > 0]
        print(f"  AI pacts signed: {len(pulled)}, carrying a balancing pull: {len(with_pull)}")
        for d in with_pull[:10]:
            print(f"    {d.get('date','?'):<18} {d.get('kingdom')} + {d.get('partner')} {d.get('type')}"
                  f" mutual {d.get('mutualValue')} pull {d.get('balancingPull')} against {d.get('against')}")
    declared = [d for _, d in events if d.get("kind") == "ai_war_declared"]
    if declared:
        supported_targets = sum(1 for d in declared if f(d.get("theirSupport")) > 0)
        supported_attackers = sum(1 for d in declared if f(d.get("ourSupport")) > 0)
        print(f"  AI wars declared: {len(declared)}; targets with expected support: {supported_targets}"
              f"; aggressors with expected support: {supported_attackers}")

    section("ENGINE  (rulers, clans, the player)")
    for kind in ("ruler_died", "ruler_changed", "player_died"):
        rows = [d for _, d in events if d.get("kind") == kind]
        print(f"  {kind:<16} {len(rows)}")
        for d in rows[:12]:
            print(f"    {d.get('date','?'):<18} {d.get('kingdom')} {d.get('hero', d.get('ruler'))} {d.get('detail','')}")
    defect = collections.Counter()
    by_detail = collections.Counter()
    for _, d in events:
        if d.get("kind") == "clan_changed_kingdom":
            detail = d.get("detail", "?")
            by_detail[detail] += 1
            if detail not in ("JoinAsMercenary", "LeaveAsMercenary", "LeaveByKingdomDestruction"):
                defect[(d.get("from"), d.get("to"))] += 1
    total = sum(by_detail.values())
    print(f"  clans changing kingdom: {total}"
          + ("  (" + ", ".join(f"{k}={v}" for k, v in by_detail.most_common()) + ")" if total else ""))
    print(f"  real moves only (excl. mercenary/destruction): {sum(defect.values())}")
    for (a, b), n in defect.most_common(12):
        print(f"    {a} -> {b}: {n}")

if link_rows:
    section("VASSAL LINKS  (weeks observed, hold min/mean/last, weeks at breaking point)")
    per = collections.defaultdict(list)
    for _, d in link_rows:
        per[(d.get("patron"), d.get("vassal"))].append(d)
    for (patron, vassal), rows in per.items():
        holds = [f(r.get("hold")) for r in rows]
        breaking = sum(1 for r in rows if f(r.get("hold")) < f(r.get("revoltLine")))
        last = rows[-1]
        print(f"  {vassal:<18} -> {patron:<18} weeks {len(rows):3d}  hold {min(holds):5.1f}/{statistics.mean(holds):5.1f}/{holds[-1]:5.1f}"
              f"  breaking {breaking:3d}  last terms fear {last.get('fear')} prot {last.get('protection')} dread {last.get('dread')}")
