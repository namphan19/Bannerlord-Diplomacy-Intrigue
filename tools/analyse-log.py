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

[EVENT] kinds with a section of their own since run 09 (the 2026-09-27 build):
  battle_scored (design 10 war score)          indemnity_paid
  tribute_accepted / tribute_refused           ai_war_declared's fromOwnCourt / fromTargetWeakness (R-1)
  amends, office_appointed / _dismissed / _vacated, tribute_level (court verbs, 2.9)
and the new fields [WAR] battles= aggressorPrisoners= defenderPrisoners=, [WAR-ENDED] finalBattles=,
[LINK] legalNeglect=.

Some things reach the log only as prose, with no telemetry record. Where the prose is fixed enough
to parse, it is read, and dated to the last telemetry record before it (plain lines carry only the
wall clock):
  (InternalWar)    internal wars begun and ended, side changes, captures, restitution
  (ClanSuccession) clan successions and cadet splits
  (AI)             AI peaces, for "(its court's bar N)" (R-1)
The last section of the report lists what no log line records at all.

Every section needing a record an older log lacks says so and moves on, so an old log still runs
and its earlier sections print exactly as they did.
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


records = []     # (day, kind, dict); kind PLAIN is a prose line, {"source", "text"}
configs = []     # (file, {name: value})
runs = []        # [RUN] dicts

# Prose lines worth keeping (see the docstring). An (AI) line is kept only when it is a peace,
# the one kind of AI prose read here. A PLAIN record takes the day of the telemetry record before
# it, and never sets that day itself, so it cannot move where a resumed log is cut.
PLAIN = re.compile(r"\[(?:INFO|WARN)\] \((InternalWar|ClanSuccession|AI)\) (.*)$")

for name in sys.argv[1:]:
    lines = pathlib.Path(name).read_text(encoding="utf-8", errors="replace").splitlines()
    current_day = None
    file_records = []
    file_config = {}
    for line in lines:
        m = re.search(r"\[(SNAPSHOT|KINGDOM|LINK|WAR|WAR-ENDED|EVENT|RUN|CONFIG)\]", line)
        if not m:
            p = PLAIN.search(line)
            if p and (p.group(1) != "AI" or " at exhaustion " in p.group(2)):
                file_records.append((current_day, "PLAIN", {"source": p.group(1), "text": p.group(2)}))
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
            # Not a political move: mercenary contracts, and a clan leaving because it or its
            # kingdom was destroyed (ChangeKingdomActionDetail, v1.4.8).
            if detail not in ("JoinAsMercenary", "LeaveAsMercenary",
                              "LeaveByClanDestruction", "LeaveByKingdomDestruction"):
                defect[(d.get("from"), d.get("to"))] += 1
    total = sum(by_detail.values())
    print(f"  clans changing kingdom: {total}"
          + ("  (" + ", ".join(f"{k}={v}" for k, v in by_detail.most_common()) + ")" if total else ""))
    print(f"  real moves only (excl. mercenary, clan/kingdom destruction): {sum(defect.values())}")
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


# ================= Everything below needs the 2026-09-27 telemetry =============
# Design 10's war score, the tribute rules, the indemnity, legal neglect, R-1 and the court
# verbs of 2.9, plus the prose lines named in the docstring. Each section prints a line saying
# what the log lacks rather than nothing, so an empty section is never mistaken for a zero.

def none_here(what="no such events in this log"):
    print(f"  {what}")


def cfg(name, default=None):
    """The constant as the latest session that logged it ran with."""
    for _, c in reversed(configs):
        if name in c:
            return f(c[name], default)
    return default


def kinds(*names):
    return [(day, d) for day, d in events if d.get("kind") in names]


def has(d, key):
    return d.get(key) not in (None, "none")


def stats(xs, fmt="{:.1f}"):
    if not xs:
        return "-"
    return (f"mean {fmt.format(statistics.mean(xs))}  median {fmt.format(statistics.median(xs))}"
            f"  max {fmt.format(max(xs))}")


def pct(n, of):
    return f"{100 * n / of:.1f}%" if of else "-"


def day_to_date(day):
    if day is None:
        return "?"
    year, rem = divmod(int(day), DAYS_PER_YEAR)
    return f"{['Spring', 'Summer', 'Autumn', 'Winter'][rem // 21]} {rem % 21 + 1}, {year}"


def parse_parties(text):
    """battle_scored's party list: name:kind:faction:counted:men:died[:gone], '/' between parties."""
    out = []
    if not text or text == "none":
        return out
    for entry in text.split("/"):
        bits = entry.split(":")
        gone = bits[-1] == "gone"
        if gone:
            bits = bits[:-1]
        if len(bits) < 6:
            continue
        out.append({"name": ":".join(bits[:-5]), "kind": bits[-5], "faction": bits[-4],
                    "counted": bits[-3], "men": int(f(bits[-2])), "died": int(f(bits[-1])), "gone": gone})
    return out


ended = [(day, d) for day, k, d in records if k == "WAR-ENDED"]
war_rows = [(day, d) for day, k, d in records if k == "WAR"]
plain = [(day, d["source"], d["text"]) for day, k, d in records if k == "PLAIN"]
all_days = [day for day, _, _ in records if day is not None]

# ---------------- war score, design 10 ----------------------------------------
section("WAR SCORE  (design 10: battles and prisoners)")
battles = kinds("battle_scored")
closing = [(day, w) for day, w in ended if "finalBattles" in w]
weekly = [d for _, d in war_rows if "battles" in d]
if not battles and not closing and not weekly:
    none_here("no battle_scored events and no battles= field on [WAR]/[WAR-ENDED]: log predates design 10")
else:
    if battles:
        cap = cfg("WarScoreBattleCap")
        points = [abs(f(d.get("proportional")) + f(d.get("award"))) for _, d in battles]
        capped = sum(1 for _, d in battles if cap is not None and abs(f(d.get("proportional"))) >= cap - 0.005)
        print(f"  battles scored: {len(battles)}   by type: "
              + ", ".join(f"{t}={n}" for t, n in collections.Counter(d.get("type", "?") for _, d in battles).most_common()))
        print("  won by: " + ", ".join(f"{w}={n}" for w, n in
                                     collections.Counter(d.get("winner", "?") for _, d in battles).most_common()))
        print(f"  |points| per battle: {stats(points)}   at the cap ({cap if cap is not None else '?'}): {capped}")
    else:
        none_here("no battle_scored events in this log")

    # A battle belongs to the war between its two kingdoms that was running on its day; the
    # engine keeps at most one war per pair, so pair and day identify it.
    ended_wars = []
    for day, w in ended:
        if day is None or "days" not in w:
            continue
        ended_wars.append({"key": frozenset((w.get("aggressor"), w.get("defender"))),
                           "start": day - int(f(w["days"])), "end": day, "w": w, "n": 0})
    unmatched = 0
    for day, d in battles:
        key = frozenset((d.get("attacker"), d.get("defender")))
        hits = [e for e in ended_wars if e["key"] == key and day is not None and e["start"] - 1 <= day <= e["end"]]
        if hits:
            min(hits, key=lambda e: e["end"])["n"] += 1
        else:
            unmatched += 1
    new_wars = [e for e in ended_wars if "finalBattles" in e["w"]]
    if battles and new_wars:
        per = [e["n"] for e in new_wars]
        print(f"  battles per ended war (n={len(new_wars)}): {stats(per, '{:.1f}')}"
              f"   wars with none scored: {sum(1 for n in per if n == 0)}")
        print(f"  battles not in an ended war (still running at the end, or before the war's first logged day): {unmatched}")
        print("  (a war begun before the log opened is missing the battles fought before it)")

    if closing:
        rows = []
        for _, w in closing:
            total, bat = f(w.get("finalScore")), f(w.get("finalBattles"))
            rows.append((total, bat, total - bat, w))
        print()
        print("  at each war's end: prisoners = finalScore - finalBattles (both aggressor-positive, 0.1 rounding)")
        print("  prisoner share = |prisoners| / (|battles| + |prisoners|)")
        for label, sub in [("all wars", rows), ("peace table", [r for r in rows if r[3].get("endedBy") == "PeaceTable"])]:
            if not sub:
                continue
            shares = [abs(p) / (abs(b) + abs(p)) for _, b, p, _ in sub if abs(b) + abs(p) > 0]
            print(f"    {label:<12} n={len(sub):4d}  |battles| mean {statistics.mean(abs(b) for _, b, _, _ in sub):5.1f}"
                  f"  |prisoners| mean {statistics.mean(abs(p) for _, _, p, _ in sub):5.1f}"
                  f"  |final| mean {statistics.mean(abs(t) for t, _, _, _ in sub):5.1f}")
            if shares:
                print(f"    {'':<12} prisoner share mean {100 * statistics.mean(shares):5.1f}%"
                      f"  median {100 * statistics.median(shares):5.1f}%"
                      f"   |prisoners| > |battles|: {sum(1 for _, b, p, _ in sub if abs(p) > abs(b))} / {len(sub)}"
                      f"   pulling opposite ways: {sum(1 for _, b, p, _ in sub if b * p < 0)}")
        print("  |final score| at the end (wars on the new build):")
        scores = [t for t, _, _, _ in rows]
        for lo, hi, label in [(0, 5, "< 5"), (5, 20, "5-20"), (20, 40, "20-40"), (40, 60, "40-60"),
                              (60, 75, "60-75"), (75, 10 ** 9, ">= 75")]:
            n = sum(1 for s in scores if lo <= abs(s) < hi)
            if n:
                print(f"    {label:>6}  {n:4d}  {'#' * max(1, int(50 * n / len(scores)))}")
        print(f"    aggressor ahead {sum(1 for s in scores if s > 0)}, defender ahead {sum(1 for s in scores if s < 0)},"
              f" level {sum(1 for s in scores if s == 0)}")
    else:
        none_here("no [WAR-ENDED] finalBattles field: the closing split is not in this log")

    if weekly:
        shares, over = [], 0
        for d in weekly:
            b = f(d.get("battles"))
            p = f(d.get("aggressorPrisoners")) - f(d.get("defenderPrisoners"))
            if abs(b) + abs(p) > 0:
                shares.append(abs(p) / (abs(b) + abs(p)))
            if abs(p) > abs(b):
                over += 1
        print(f"  weekly [WAR] rows of running wars: {len(weekly)}; prisoner share mean "
              f"{100 * statistics.mean(shares) if shares else 0:.1f}%; weeks with |prisoners| > |battles|: {over}")

    if battles:
        # Design 10 section 9a: a siege whose defender "fielded 0".
        zero = []
        for day, d in battles:
            which = [s + part for s in ("attacker", "defender") for part in ("Fielded", "Manpower")
                     if has(d, s + part) and int(f(d.get(s + part))) == 0]
            if which:
                zero.append((day, d, which))
        print()
        print(f"  battles where a side's counted men or manpower read 0 (design 10 section 9a): {len(zero)} / {len(battles)}")
        if zero:
            for (t, s), n in collections.Counter((d.get("type", "?"), s) for _, d, w in zero for s in w).most_common():
                print(f"    {t:<22} {s:<18} {n:4d}")
            there = sum(1 for _, d, w in zero for s in w
                        if s.endswith("Fielded") and f(d.get(s.replace("Fielded", "SideMen"))) > 0)
            print(f"    of which a side counted 0 men while its side had men there (sideMen > 0): {there}")
            print("    the first few in full:")
            for _, d, _ in zero[:4]:
                print("      " + " ".join(f"{k}={v}" for k, v in d.items()))
        # The table in design 10 section 9a that decides what a won assault's defender list means.
        won = [d for _, d in battles if d.get("type") == "Siege" and d.get("winner") == "attacker"]
        if won:
            sig = collections.Counter()
            for d in won:
                parties = parse_parties(d.get("defenderParties"))
                garrisons = [p for p in parties if p["kind"] == "garrison"]
                if any(p["counted"] == "yes(walls)" for p in garrisons):
                    sig["a garrison counted yes(walls): the capture came first"] += 1
                if any(p["counted"] == "yes" and p["men"] > 0 for p in garrisons):
                    sig["a garrison counted yes, with men: the owner changes after"] += 1
                if not any(p["men"] > 0 for p in garrisons) and any(p["kind"] == "militia" and p["men"] > 0 for p in parties):
                    sig["no garrison with men, militia only"] += 1
                if any(p["kind"] == "settlement" and p["men"] > 0 for p in parties):
                    sig["a settlement entry with men above 0"] += 1
                if any(p["kind"] == "lord" and p["counted"] == "no(realm)" and p["faction"] == "none" and p["gone"]
                       for p in parties):
                    sig["a lord no(realm), faction none, gone"] += 1
                if d.get("settlementNow") == d.get("attacker"):
                    sig["settlementNow is the attacker"] += 1
            print(f"  sieges the attacker won: {len(won)}; defender lists by design 10 section 9a's cases (a line can show several):")
            for k, n in sig.most_common():
                print(f"    {k:<58} {n:4d}")

# ---------------- peace outcomes per decade ---------------------------------
DECADE = 10 * DAYS_PER_YEAR
TERM_STARTS = ("submit_as_a_vassal", "release_every_vassal", "tributary_pact_at", "indemnity_of_",
               "release_prisoners", "white_peace")


def peace_tags(terms, ended_by):
    """What one [WAR-ENDED] terms= conceded, as tags, and how many fiefs changed hands."""
    tags, fiefs, ceding = set(), 0, False
    for part in (terms or "").split(",_"):
        if part.startswith("cede_"):
            ceding, fiefs = True, fiefs + 1
            tags.add("fiefs")
            continue
        if ceding and part and not part.startswith(TERM_STARTS):
            fiefs += 1          # the next fief of the same "cede A, B" list
            continue
        ceding = False
        if part == "white_peace":
            tags.add("dormant" if ended_by == "Dormant" else "white")
        elif part.startswith("tributary_pact_at"):
            tags.add("tribute")
        elif part.startswith("submit_as_a_vassal"):
            tags.add("subjug")
        elif part == "vassalage":
            tags.add("kneel")
        elif part == "release_every_vassal":
            tags.add("dissolve")
        elif part.startswith("indemnity_of_"):
            tags.add("indemn")
        elif part == "release_prisoners":
            tags.add("captives")
        elif part:
            tags.add("other")
    if tags != {"captives"}:
        tags.discard("captives")   # prisoners ride along with most packages; alone they are a package
    return tags, fiefs


section("PEACE OUTCOMES PER DECADE  (10 years = 840 days from the log's first day; [WAR-ENDED] terms=)")
with_terms = [(day, w) for day, w in ended if "terms" in w and day is not None]
if not with_terms:
    none_here("no [WAR-ENDED] terms= in this log (it predates the v2 telemetry)")
else:
    origin = min(all_days)
    last = max(all_days)
    cols = ["white", "dormant", "tribute", "subjug", "kneel", "indemn", "fiefs", "dissolve", "captives", "other"]
    table = collections.defaultdict(lambda: collections.Counter())
    for day, w in with_terms:
        tags, fiefs = peace_tags(w.get("terms"), w.get("endedBy"))
        row = table[(day - origin) // DECADE]
        row["wars"] += 1
        row["fiefCount"] += fiefs
        for t in tags:
            row[t] += 1
    print(f"  {'decade':<28}{'wars':>5}" + "".join(f"{c:>9}" for c in cols))
    decades = range(0, (last - origin) // DECADE + 1)
    total = collections.Counter()
    for k in decades:
        row = table.get(k, collections.Counter())
        total.update(row)
        years = min(DECADE, last - origin - k * DECADE + 1) / DAYS_PER_YEAR
        label = f"{k + 1} from {day_to_date(origin + k * DECADE)}" + (f" ({years:.1f}y)" if years < 10 else "")
        cells = [f"{row['fiefs']}/{row['fiefCount']}" if c == "fiefs" else str(row[c]) for c in cols]
        print(f"  {label:<28}{row['wars']:>5}" + "".join(f"{c:>9}" for c in cells))
    cells = [f"{total['fiefs']}/{total['fiefCount']}" if c == "fiefs" else str(total[c]) for c in cols]
    print(f"  {'TOTAL':<28}{total['wars']:>5}" + "".join(f"{c:>9}" for c in cells))
    print("  wars per column; a package counts in each column it contains. white = white peace at a table,")
    print("  dormant = a lapsed war, subjug = 'submit as a vassal' at the table, kneel = endedBy Submission or")
    print("  Defection, fiefs = wars/fiefs ceded, dissolve = a sphere released, captives = prisoners released")
    print("  and nothing else, other = released_by / eliminated / ordered_by / unknown (no table).")
    for kind, label in [("tribute_accepted", "tribute demands accepted"), ("vassalage_formed", "vassalage formed (all routes)")]:
        when = [day for day, _ in kinds(kind) if day is not None]
        if when:
            per = collections.Counter((day - origin) // DECADE for day in when)
            print(f"  {label}: " + ", ".join(f"decade {k + 1} {per[k]}" for k in decades))

# ---------------- indemnity ---------------------------------------------------
section("INDEMNITY  (indemnity_paid)")
paid = kinds("indemnity_paid")
if not paid:
    none_here()
else:
    gold = [f(d.get("gold")) for _, d in paid]
    shares = [f(d.get("gold")) / f(d.get("treasury")) for _, d in paid if f(d.get("treasury")) > 0]
    cap = cfg("PeaceIndemnityMaxTreasuryShare")
    print(f"  indemnities paid: {len(paid)}, {sum(gold):,.0f} denars in all")
    print(f"  gold                  : {stats(gold, '{:,.0f}')}")
    print(f"  share of the treasury : {stats([100 * s for s in shares], '{:.1f}%')}"
          + (f"   at the cap ({100 * cap:.0f}%): {sum(1 for s in shares if s >= cap - 0.005)}" if cap is not None else "")
          + (f"   payer with no treasury: {len(paid) - len(shares)}" if len(shares) < len(paid) else ""))
    print(f"  points                : {stats([f(d.get('points')) for _, d in paid])}")
    by_payer = collections.Counter(d.get("payer") for _, d in paid)
    print("  payers: " + ", ".join(f"{k} {n}" for k, n in by_payer.most_common(8)))

# ---------------- tribute demands ---------------------------------------------
section("TRIBUTE DEMANDS  (tribute_accepted / tribute_refused)")
answers = [d for _, d in kinds("tribute_accepted", "tribute_refused")]
if not answers:
    none_here()
else:
    def answer(d):
        return "accepted" if d.get("kind") == "tribute_accepted" else d.get("reason", "?")

    lines = collections.Counter(answer(d) for d in answers)
    pairs = collections.defaultdict(set)
    for d in answers:
        pairs[answer(d)].add((d.get("demander"), d.get("target")))
    print(f"  {'answer':<14}{'lines':>7}{'pairs':>7}   (a standing refusal repeats weekly, so count pairs)")
    order = ["accepted", "court", "player", "cap", "cooldown"]
    for r in order + sorted(k for k in lines if k not in order):
        print(f"  {r:<14}{lines[r]:>7}{len(pairs[r]):>7}")
    print(f"  distinct (demander, target) pairs answered: {len({(d.get('demander'), d.get('target')) for d in answers})}")

    refuses_at = next((f(d.get("refusesAt")) for d in answers if has(d, "refusesAt")), cfg("AiTributeCourtRefusalShare"))
    court = [d for d in answers if d.get("courtAnswers") == "true" and has(d, "courtShare")
             and answer(d) in ("accepted", "court")]
    acc = [d for d in court if answer(d) == "accepted"]
    ref = [d for d in court if answer(d) == "court"]
    if court:
        acc_p = {(d.get("demander"), d.get("target")) for d in acc}
        ref_p = {(d.get("demander"), d.get("target")) for d in ref}
        print(f"  court answers (an AI crown whose court was read): {len(court)} lines, refused {len(ref)}"
              f" ({pct(len(ref), len(court))}); by pair: {len(ref_p | acc_p)} pairs, {len(ref_p)} ever refused"
              f" ({pct(len(ref_p), len(ref_p | acc_p))}), {len(acc_p)} accepted"
              + (f", {len(ref_p & acc_p)} of them refused first" if ref_p & acc_p else ""))
        print(f"  courtShare (share of the court left ready to break) against refusesAt={refuses_at}:")
        print(f"    accepted : {stats([f(d.get('courtShare')) for d in acc], '{:.2f}')}")
        print(f"    refused  : {stats([f(d.get('courtShare')) for d in ref], '{:.2f}')}")
        print(f"    {'band':<11}{'accepted':>9}{'refused':>9}")
        for i in range(10):
            lo, hi = i / 10, (i + 1) / 10
            a = sum(1 for d in acc if lo <= f(d.get("courtShare")) < hi or (i == 9 and f(d.get("courtShare")) >= 1))
            r = sum(1 for d in ref if lo <= f(d.get("courtShare")) < hi or (i == 9 and f(d.get("courtShare")) >= 1))
            if a or r or (refuses_at is not None and lo <= refuses_at < hi):
                mark = "   <- refusesAt" if refuses_at is not None and lo <= refuses_at < hi else ""
                print(f"    {lo:.1f}-{hi:.1f}  {a:>9}{r:>9}{mark}")
        if refuses_at is not None:
            odd_a = sum(1 for d in acc if f(d.get("courtShare")) >= refuses_at)
            odd_r = sum(1 for d in ref if f(d.get("courtShare")) < refuses_at)
            if odd_a or odd_r:
                print(f"    ODD: accepted at or above the line {odd_a}, refused below it {odd_r}")
    else:
        none_here("no answer where an AI crown's court was read (courtAnswers=true with a courtShare)")
    player = [d for d in answers if d.get("courtAnswers") == "false"]
    if player:
        shares = [f(d.get("courtShare")) for d in player if has(d, "courtShare")]
        print(f"  a player crown answering: {len(player)} lines (accepted {sum(1 for d in player if answer(d) == 'accepted')},"
              f" refused by the player {sum(1 for d in player if answer(d) == 'player')});"
              f" the share its court read: {stats(shares, '{:.2f}')}")

# ---------------- R-1: the court in foreign policy ----------------------------
section("COURT AND FOREIGN POLICY  (R-1: ai_war_declared, AI peace bars)")
declared_all = [d for _, d in kinds("ai_war_declared")]
r1 = [d for d in declared_all if "fromOwnCourt" in d or "fromTargetWeakness" in d]
if not declared_all:
    none_here("no ai_war_declared events in this log")
elif not r1:
    none_here(f"{len(declared_all)} ai_war_declared events, none carrying fromOwnCourt / fromTargetWeakness"
              " (log predates R-1)")
else:
    own = [f(d.get("fromOwnCourt")) for d in r1]
    weak = [f(d.get("fromTargetWeakness")) for d in r1]
    print(f"  declarations carrying the R-1 terms: {len(r1)}  (opened {sum(1 for d in r1 if d.get('opened') == 'true')})")
    print(f"  fromOwnCourt       : mean {statistics.mean(own):+.2f}  non-zero {sum(1 for x in own if x != 0)}"
          f"  hawkish (>0) {sum(1 for x in own if x > 0)}  dovish (<0) {sum(1 for x in own if x < 0)}"
          f"  range {min(own):+.1f} .. {max(own):+.1f}")
    print(f"  fromTargetWeakness : mean {statistics.mean(weak):+.2f}  non-zero {sum(1 for x in weak if x != 0)}"
          f"  max {max(weak):.1f}")
    threshold = cfg("AiWarThreshold")
    if threshold is None:
        none_here("no AiWarThreshold in [CONFIG]: the without-them count cannot be made")
    else:
        def aggressiveness(day):
            best = None
            for r in runs:
                rd = int(r["day"]) if "day" in r else None
                if rd is not None and day is not None and rd <= day:
                    best = r
            return f((best or (runs[0] if runs else {})).get("aggressiveness"), 1.0)

        gated, both, own_only, weak_only = 0, 0, 0, 0
        for day, d in kinds("ai_war_declared"):
            if not ("fromOwnCourt" in d or "fromTargetWeakness" in d):
                continue
            value = f(d.get("value"))
            if value < threshold:
                continue
            gated += 1
            a = aggressiveness(day)
            o, w = f(d.get("fromOwnCourt")), f(d.get("fromTargetWeakness"))
            both += value - a * (o + w) < threshold
            own_only += value - a * o < threshold
            weak_only += value - a * w < threshold
        print(f"  at or above the bar ({threshold:g}): {gated}; would have fallen under it without both terms: {both},"
              f" without the own-court term: {own_only}, without the target-weakness term: {weak_only}")
        if gated < len(r1):
            print(f"  below the bar: {len(r1) - gated} (the player's annexation button is not gated by it)")
        print("  (value = terms x aggressiveness; only this target's value is recomputed - without the terms")
        print("   another target, or none, might have been chosen, and a war a court talked down leaves no line)")

# AI peaces, from the prose: " (its court's bar N)" appears only when the court moved the bar.
PEACE = re.compile(r"^(?P<k>.+?) (?P<how>sued for peace with|bought peace from|imposed terms on|let) (?P<e>.+?)(?: go)?"
                   r" at exhaustion (?P<x>-?[\d.]+)(?: \(its court's bar (?P<bar>-?[\d.]+)\))?")
peaces = [m for _, s, t in plain if s == "AI" for m in [PEACE.match(t)] if m]
if not peaces:
    none_here("no AI peace lines ('(AI) ... at exhaustion N') in this log")
else:
    base = cfg("ExhaustionSeekPeace", 60.0)
    bars = [f(m.group("bar")) for m in peaces if m.group("bar")]
    print(f"  AI peaces in the prose log: {len(peaces)}; with '(its court's bar N)': {len(bars)}"
          f" - sooner (bar < {base:g}) {sum(1 for b in bars if b < base)}, later {sum(1 for b in bars if b > base)}"
          + (f"; bar {stats(bars)}, min {min(bars):.1f}" if bars else ""))
    if not bars and not r1:
        print("  (no bar notes, and no R-1 fields on the declarations: the build most likely predates R-1)")

# ---------------- hegemony: legal neglect --------------------------------------
section("HEGEMONY LEGAL NEGLECT  ([LINK] legalNeglect, logged as a cost, so <= 0)")
neglect = [d for _, d in link_rows if "legalNeglect" in d]
if not neglect:
    none_here("no [LINK] legalNeglect field in this log" if link_rows else "no [LINK] records in this log")
else:
    nz = [f(d["legalNeglect"]) for d in neglect if f(d["legalNeglect"]) != 0]
    print(f"  link-weeks: {len(neglect)}; with legal neglect: {len(nz)} ({pct(len(nz), len(neglect))})"
          + (f"; when non-zero mean {statistics.mean(nz):.2f}, worst {min(nz):.2f}" if nz else ""))
    per = collections.defaultdict(lambda: [0, 0])
    for d in neglect:
        c = per[(d.get("patron"), d.get("vassal"))]
        c[0] += 1
        c[1] += f(d["legalNeglect"]) != 0
    for (patron, vassal), (weeks, hit) in per.items():
        if hit:
            print(f"    {vassal:<18} -> {patron:<18} {hit:3d} of {weeks:3d} weeks")

# ---------------- internal wars and houses (prose) ------------------------------
section("INTERNAL WARS AND HOUSES  (prose lines: no telemetry exists; days ~ the record before)")
IW_START = re.compile(r"^(?P<k>.+?): (?P<claimant>.+?) takes up arms against (?P<ruler>.*?) \((?P<why>.*?)\)\. "
                      r"Legitimacy (?P<leg>-?[\d.]+), bloc (?P<bloc>-?\d+)%, (?P<disloyal>\d+) clans below 25\. "
                      r"Rebels \((?P<n>\d+)\): (?P<names>.*?)\. Crown side held (?P<crown>-?\d+)% of the court\.")
IW_END = re.compile(r"^(?P<k>.+?): the internal war ends - (?P<outcome>\w+) \((?P<reason>.*)\)\. "
                    r".*?exhaustion rebels (?P<re>-?[\d.]+) / crown (?P<ce>-?[\d.]+)")
IW_SIDE = re.compile(r"^(?P<k>.+?): (?P<clan>.+?) went over to (?P<side>.+?)"
                     r"(?: for (?P<price>[\d.,\s\u00a0\u202f']+?) paid by (?P<buyer>.+?)| \(forced, unpaid\))\. "
                     r"Exhaustion rebels")
IW_CAPTURE = re.compile(r"^(?P<k>.+?): losing (?P<fief>.+?) costs (?P<who>the rising|the crown) exhaustion")
IW_RESTORED = re.compile(r"^(?P<k>.+?): (?P<n>\d+) fief\(s\) restored after the crown's win \((?P<taken>\d+) fief\(s\)")
IW_RESTITUTION = re.compile(r"^(?P<k>.+?): restitution - (?P<fief>.+?) passes from (?P<frm>.+?) to ")
IW_LEFT = re.compile(r"^(?P<k>.+?): (?:(?P<clan>.+?) left the realm and the rebellion\."
                     r"|(?P<n>\d+) rebel clan\(s\) left the realm mid-war\.)")
IW_DEPOSED = re.compile(r"^(?P<k>.+?): (?P<ruler>.+?) was deposed and remains a pretender")
# Lines read only so that "not recognised" below counts real drift in the wording, not these.
IW_KNOWN = re.compile(r"^.+?: (?:\w+, rebels lost \d+, crown lost \d+ -> exhaustion"
                      r"|.+? passed from .*? to .*? - the rising now holds \d+ fiefs\."
                      r"|the player's clan joined the rebellion\.)")
IW_NOT_TAKEN = re.compile(r"^.+?: the declaration did not take - ")
SUCCEEDS = re.compile(r"^(?P<clan>.+?): (?P<new>.+?) succeeds (?P<old>.+?)(?:; runner-up (?P<ru>.+?))?"
                      r" - (?P<div>the house divides|no division) \((?P<reason>.*)\)\.$")
DIVIDES = re.compile(r"^(?P<parent>.+?) divides: (?P<founder>.+?) founds (?P<cadet>.+?) \((?P<id>[^,()]+), tier (?P<tier>\d+)\)")
CADET_TAKES = re.compile(r"takes (?P<amount>\d+) of .*?'s (?P<parent>-?\d+) influence \((?P<leaving>\d+) of (?P<adults>\d+)"
                         r" adults, share (?P<share>\d+)%\)")

iw, iw_open, loose = [], {}, collections.Counter()
successions, divisions, cadet_amounts = 0, [], []


def war_of(k):
    """The internal war running in realm k, or a new row for one whose start line was not seen."""
    w = iw_open.get(k)
    if w is None:           # begun before the log opened, or abandoned before its start line
        w = {"k": k, "claimant": "?", "start": None, "end": None, "outcome": "ongoing", "reason": "",
             "sides": 0, "paid": 0, "forced": 0, "gold": 0, "toRising": 0, "lostRising": 0, "lostCrown": 0,
             "restored": None, "rebels": "?", "crown": "?"}
        iw.append(w)
        iw_open[k] = w
    return w


for day, source, text in plain:
    if source == "ClanSuccession":
        if SUCCEEDS.match(text):
            successions += 1
        elif DIVIDES.match(text):
            m = CADET_TAKES.search(text)
            divisions.append((day, DIVIDES.match(text), m))
            if m:
                cadet_amounts.append(int(m.group("amount")))
        continue
    if source != "InternalWar":
        continue

    m = IW_START.match(text)
    if m:
        iw_open.pop(m.group("k"), None)
        w = war_of(m.group("k"))
        w.update(claimant=m.group("claimant"), start=day, rebels=m.group("n"), crown=m.group("crown") + "%")
        continue
    m = IW_END.match(text)
    if m:
        w = war_of(m.group("k"))
        w.update(end=day, outcome=m.group("outcome"), reason=m.group("reason"))
        iw_open.pop(m.group("k"), None)
        continue
    m = IW_SIDE.match(text)
    if m:
        w = war_of(m.group("k"))
        w["sides"] += 1
        w["toRising"] += m.group("side") != "the crown"
        if m.group("price"):
            w["paid"] += 1
            w["gold"] += int(re.sub(r"\D", "", m.group("price")) or 0)
        else:
            w["forced"] += 1
        continue
    m = IW_CAPTURE.match(text)
    if m:
        w = war_of(m.group("k"))
        w["lostRising" if m.group("who") == "the rising" else "lostCrown"] += 1
        continue
    m = IW_RESTORED.match(text)
    if m:
        # Written after the end line, so it belongs to the war that just ended in that realm.
        target = next((w for w in reversed(iw) if w["k"] == m.group("k")), None)
        if target is not None:
            target["restored"] = int(m.group("n"))
        continue
    if IW_RESTITUTION.match(text):
        loose["restitution line (one fief given back)"] += 1
        continue
    m = IW_LEFT.match(text)
    if m:
        loose["rebel clans leaving the realm mid-war"] += int(m.group("n")) if m.group("n") else 1
        continue
    if IW_DEPOSED.match(text):
        loose["deposed rulers kept as pretenders"] += 1
        continue
    if IW_NOT_TAKEN.match(text):
        loose["risings abandoned: the declaration did not take"] += 1
        continue
    if IW_KNOWN.match(text):
        if "the player's clan joined" in text:
            loose["the player's clan joined a rebellion"] += 1
        continue
    loose["(InternalWar) lines not recognised (wording changed?)"] += 1

if not iw and not successions and not divisions and not loose:
    none_here("no (InternalWar) or (ClanSuccession) lines in this log")
else:
    if iw:
        ended_iw = [w for w in iw if w["end"] is not None or w["outcome"] != "ongoing"]
        print(f"  internal wars seen: {len(iw)}, begun in the log {sum(1 for w in iw if w['start'] is not None)},"
              f" ended {len(ended_iw)}   outcomes: "
              + ", ".join(f"{k}={n}" for k, n in collections.Counter(w["outcome"] for w in iw).most_common()))
        conceded = sum(1 for w in iw if w["reason"].endswith(" conceded"))
        print(f"  ended by a concession: {conceded}" + ("   reasons: " if ended_iw else "")
              + "; ".join(f"{k} {n}" for k, n in collections.Counter(
                  "<leader> conceded" if w["reason"].endswith(" conceded") else w["reason"]
                  for w in ended_iw).most_common(6)))
        sides = sum(w["sides"] for w in iw)
        print(f"  side changes: {sides} (paid {sum(w['paid'] for w in iw)}, forced {sum(w['forced'] for w in iw)};"
              f" to the rising {sum(w['toRising'] for w in iw)}, to the crown {sides - sum(w['toRising'] for w in iw)};"
              f" gold {sum(w['gold'] for w in iw):,})")
        print(f"  fiefs taken across the line: from the rising {sum(w['lostRising'] for w in iw)},"
              f" from the crown {sum(w['lostCrown'] for w in iw)};"
              f" restored after crown wins: {sum(w['restored'] or 0 for w in iw)}")
        print(f"  {'realm':<17}{'claimant':<17}{'began ~':<18}{'ended ~':<18}{'days':>5} {'outcome':<10}"
              f"{'rebels':>7}{'crown':>6}{'sides':>6}{'taken r/c':>10}{'restored':>9}")
        for w in iw:
            days = (w["end"] - w["start"]) if w["start"] is not None and w["end"] is not None else None
            print(f"  {w['k'][:16]:<17}{w['claimant'][:16]:<17}"
                  f"{day_to_date(w['start']) if w['start'] is not None else 'not seen':<18}"
                  f"{day_to_date(w['end']) if w['end'] is not None else '-':<18}"
                  f"{days if days is not None else '-':>5} {w['outcome']:<10}{w['rebels']:>7}{w['crown']:>6}"
                  f"{w['sides']:>6}{str(w['lostRising']) + '/' + str(w['lostCrown']):>10}"
                  f"{w['restored'] if w['restored'] is not None else '-':>9}")
    for k, n in loose.most_common():
        print(f"  {k:<50} {n:4d}")
    print(f"  clan successions: {successions}; houses divided (cadet branches): {len(divisions)}"
          + (f"; influence a cadet took: {stats(cadet_amounts, '{:.0f}')}" if cadet_amounts else ""))
    for day, m, took in divisions[:8]:
        print(f"    ~{day_to_date(day):<16} {m.group('parent')} -> {m.group('cadet')} (founder {m.group('founder')})"
              + (f", took {took.group('amount')} of {took.group('parent')} influence ({took.group('share')}%)" if took else ""))

# ---------------- court verbs, 2.9 -------------------------------------------
section("COURT VERBS  (2.9: amends, offices, vassal tribute levels)")
amends = [d for _, d in kinds("amends")]
appointed = [d for _, d in kinds("office_appointed")]
removed = [d for _, d in kinds("office_dismissed", "office_vacated")]
levels = [d for _, d in kinds("tribute_level")]
if not (amends or appointed or removed or levels):
    none_here()
else:
    if amends:
        print(f"  amends made: {len(amends)}   by grievance: "
              + ", ".join(f"{k}={n}" for k, n in collections.Counter(d.get("type") for d in amends).most_common()))
        print(f"    influence {stats([f(d.get('influence')) for d in amends], '{:.0f}')}"
              f"   gold {stats([f(d.get('gold')) for d in amends], '{:,.0f}')}")
        print(f"    loyalty gained {stats([f(d.get('loyaltyAfter')) - f(d.get('loyaltyBefore')) for d in amends])}"
              f"   by realm: " + ", ".join(f"{k} {n}" for k, n in collections.Counter(d.get("kingdom") for d in amends).most_common(8)))
    else:
        none_here("amends: none")
    if appointed or removed:
        print(f"  offices: appointed {len(appointed)}"
              + (" (" + ", ".join(f"{k}={n}" for k, n in collections.Counter(d.get("seat") for d in appointed).most_common()) + ")"
                 if appointed else "")
              + f", dismissed {sum(1 for d in removed if d.get('kind') == 'office_dismissed')},"
                f" vacated {sum(1 for d in removed if d.get('kind') == 'office_vacated')}")
        if appointed:
            print(f"    cost: influence {stats([f(d.get('influence')) for d in appointed], '{:.0f}')}"
                  f"   gold {stats([f(d.get('gold')) for d in appointed], '{:,.0f}')}")
            print(f"    seat skill gained {stats([f(d.get('skillAfter')) - f(d.get('skillBefore')) for d in appointed], '{:.0f}')}")
        if removed:
            print("    why seats emptied: " + "; ".join(f"{k.replace('_', ' ')} {n}" for k, n in collections.Counter(
                re.sub(r"_to_.*$", "_to_<someone>", d.get("reason", "?")) for d in removed).most_common(6)))
    else:
        none_here("offices: none")
    if levels:
        up = [d for d in levels if f(d.get("to")) > f(d.get("from"))]
        down = [d for d in levels if f(d.get("to")) < f(d.get("from"))]
        print(f"  vassal tribute set: {len(levels)} (raised {len(up)}, lowered {len(down)},"
              f" to none {sum(1 for d in levels if f(d.get('to')) == 0)})   "
              + ", ".join(f"{a}->{b} {n}" for (a, b), n in
                          collections.Counter((d.get("from"), d.get("to")) for d in levels).most_common(6)))
        print(f"    Hold when raised {stats([f(d.get('hold')) for d in up])}   when lowered {stats([f(d.get('hold')) for d in down])}")
    else:
        none_here("vassal tribute levels: none")

# ---------------- what no log line records -----------------------------------
section("NOT IN TELEMETRY  (what this build cannot be measured on from a log)")
for line in [
    "Read from prose above, not telemetry - dated only to the record before, and the wording can drift:",
    "  internal wars (start, end, outcome, concessions, side changes, captures, restitution),",
    "  clan successions and cadet splits, and the court's peace bar (only when it moved the bar;",
    "  a peace offered to the player or a dormant lapse carries none).",
    "Not in the log in any parseable form:",
    "  legitimacy and its changes, grievances raised (amends are recorded, grievances are not),",
    "  loyalty, bloc shares, pretenders, royal successions (Succession prose only), espionage;",
    "  a war a court of Doves talked a crown out of (nothing is written when no war is declared);",
    "  an internal war's exhaustion over time (prose on each battle, no weekly record).",
]:
    print(f"  {line}")
