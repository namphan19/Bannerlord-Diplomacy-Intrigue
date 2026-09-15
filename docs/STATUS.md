# Status — 2026-09-15

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**.
Last measured: balance run 01, 28 in-game years — see below.

---

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.8)** | ✅ code complete, **acceptance met** over a measured 28-year run. Two constants tuned from it and awaiting a confirming run |
| **2 — Court intrigue** | ⬜ spec written and reviewed, no code |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | 🔄 balance run 01 done, run 02 wanted |

### Phase 1, feature by feature

All of this exists, builds, and was exercised in a live campaign. `docs/ROADMAP.md` carries
the per-feature evidence tables.

| | Feature | Code |
|---|---|---|
| 1.1 | War exhaustion, war score, weariness | `Diplomacy/WarExhaustion.cs` |
| 1.2 | Casus belli, fief ledger, claims, fabrication | `Diplomacy/ClaimRegistry.cs`, `FiefHistory.cs`, `CasusBelli.cs` |
| 1.3 | Treaty engine, six types, tribute, enforcement | `Diplomacy/TreatyRegistry.cs`, `TreatyEnforcement.cs`, `Patches/` |
| 1.4 | Diplomatic trust, directional and non-decaying | `Diplomacy/TrustRegistry.cs` |
| 1.5 | Peace table on a war-score budget | `Diplomacy/PeaceTable.cs` |
| 1.6 | Call to arms, refusal, follower release | `Diplomacy/CallToArms.cs` |
| 1.7 | Weekly AI evaluation, one action per kingdom | `Diplomacy/AiDiplomacy.cs` |
| 1.8 | Ctrl+D diplomacy menu, exhaustion bands | `UI/DiplomacyMenu.cs`, `Diplomacy/ExhaustionBands.cs` |

---

## Phase 1 acceptance: met, measured over 28 in-game years

Balance run 01 is in: **340 weekly snapshots, 247 wars, 0 errors**, Summer 1084 to Autumn
1112. Full analysis in [docs/balance/run-01.md](balance/run-01.md).

| Criterion | Result |
|---|---|
| Wars average under ~3 years | 18.4 days mean — passes hugely |
| An alliance forms and holds | up to 8 at once, present 85% of weeks |
| No permanent total war | every kingdom at war in 0.9% of weeks |
| Stability | 28 years, zero errors, zero tribute defaults |

**The criteria are met and the run still found a real problem: wars are ~10× shorter than
the design intends.** A chosen war reaches exhaustion 51.6 in 23.6 days, i.e. 2.19 per day
of which elapsed time is 0.08 — casualties were doing ~96% of the work, about 30× what the
design assumed.

Two constants were tuned from that data and **both need a second run to confirm**:

- `ExhaustionCasualtyStrengthDivisor` 100 → **20** (casualty exhaustion 5× weaker)
- `AncestralClaimMemoryYears` 20 → **12** (live claims had settled at 83–93, so everyone
  held a claim on everyone and `Conquest` was never needed)

**Next balance run should check:** do chosen wars now last 100–200 days; does the peace
table's concession ladder ever fire (it fired **zero** times in 28 years, so the whole
demand-budget half of 1.5 is currently dead code); do live claims settle nearer 30–40.

## Choose what to do next

**A. A second balance run.** Confirms the two tuned constants and shows whether longer wars
bring the peace table's concession ladder to life. Cheapest high-value step: the lead just
plays, the mod reports.

**B. Start Phase 2 — court intrigue.** Fully specced in `docs/design/02-intrigue.md` and
independent of the tuning. Implementation order is 2.1 grievances → 2.2 loyalty → 2.3 blocs
and voting → 2.4 legitimacy → 2.5 succession → 2.6 civil war → 2.7 UI. Phase 1 already
leaves hooks: `ExhaustionCourtPressure` (40) is where doves are meant to gain support, and
`CallToArms.WouldAnswer` has an explicit note where vassal defiance should read grievances.

**C. Hegemony (emperor / khagan).** The lead proposed a supra-kingdom tier — one title over
several vassalage treaties, with election and defection cascades. Assessed as feasible and
cheap, because `TreatyType.Vassalage` already does the hard part. **Agreed to sit at 2.8,
after grievances and legitimacy exist**, because without those counter-pressures a hegemony
is a one-way ratchet that decides the map. Not yet specced; `docs/design/04-hegemony.md`
would be the place, and four questions are still open — how the title is founded, whether
membership gives anything or is pure coercion, how to cap call-to-arms cascades across it,
and what happens when the overlord is destroyed.

---

## Decisions already made. Do not re-litigate.

| Decision | Detail |
|---|---|
| Three pillars | Diplomacy, court intrigue, espionage. **Not** economy/trade |
| Standalone | No dependency on the BUTR Diplomacy mod. Mutually incompatible with it by design |
| English UI only | Localization keys for future translation, English shipped |
| Minor factions out of scope | Treaties, claims and exhaustion are kingdom-only |
| AI plays by the same rules | Enforced in code — no "is this the player" argument anywhere |
| Enemy exhaustion shown as a band | Five bands whose edges are the behavioural thresholds. Phase 3 `ReadCourt` buys the exact figure |
| Vassalage stays in Phase 1 | And it carries military service; a tributary pays, a vassal pays and fights |
| Blocked routine path, deliberate defiance | The AI never wanders into a forbidden war; breaking a treaty on purpose is always possible and always expensive |
| Native dialogs, not a Gauntlet screen | A custom screen is the eventual goal and the most fragile thing a mod can own |
| `net472` | See CLAUDE.md §1 |

---

## Known gaps and loose ends

- **Battle and siege paths unverified.** Casualties reach exhaustion through `MapEventEnded`
  and fief capture through `OnSettlementOwnerChangedEvent`. Neither can be triggered from a
  console, so both need a real battle and a real siege. They are wired and reviewed, not
  observed.
- **Clock-dependent behaviour unverified** for the same reason: treaty expiry and its trust
  dividend, tribute changing hands on day 7, the two-year peace dividend.
- **Menu navigation past the root** was never clicked through — GABS cannot click inside a
  `MultiSelectionInquiry`. The root renders correctly (screenshot) and the vassal/ruler
  distinction works. Ask the lead to walk the submenus.
- **`AiDiplomacy.TryDemandTribute` accepts on a strength ratio and a trust floor only.** It
  has no notion of the target's willingness beyond that; a weak kingdom with high trust will
  submit readily. Worth revisiting when Phase 2 gives courts an opinion.
- **A vassal's existing wars are untouched when it submits.** Signing vassalage does not end
  the client's own wars, so a patron can inherit a war it did not choose. Deliberate for now;
  decide when hegemony is specced.
- **`ConcessionLadder` yields castles before towns** via a two-pass flag that reads awkwardly
  (`townsFirst: false`). It works; it would read better as two explicit loops.

## Tools written for this project

| | |
|---|---|
| `tools/LoadProbe` | Pre-flight: target framework vs game host, reference resolution, `SubModuleClassType`. Catches the class of failure that produces no log at all |
| `tools/ApiDump` | Dumps the real public surface of game types to `artifacts/api/`. Use before writing against any unfamiliar API |
| `diplomacy.war_value A \| B` | The AI war valuation term by term, naming the gate that blocks. Written after guessing wrong twice |
| `diplomacy.tick_days N` | N days of upkeep, real functions, clock unmoved |
| `diplomacy.ai_week N` | N weeks of AI evaluation plus matching upkeep. Prints its own limitations past 4 weeks |
| `diplomacy.report` | Telemetry snapshot to the log plus a full world report to file |
| `tools/analyse-log.py` | Parses a run log into the acceptance numbers: war durations, alliance formation, permanent-war check, casus belli mix. `python tools/analyse-log.py <log>` |
