# Status — 2026-09-15

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Head: `d3f7926`. Module version 0.1.0. Save schema **v4**, definer base id **2749100**.

---

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.8)** | ✅ **all code written**, each piece verified in game. **Acceptance criterion not met** — see below |
| **2 — Court intrigue** | ⬜ spec written and reviewed, no code |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | ⬜ the balance task has effectively started |

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

## The one thing blocking Phase 1

**Acceptance:** *in a 10-year AI-only campaign, wars average under ~3 years, at least one
alliance forms and holds, and no kingdom sits at permanent total war.*

Not met, and **not measurable from a tool call**. `diplomacy.tick_days` and
`diplomacy.ai_week` cannot advance `CampaignTime.Now`, so treaties never expire and clan
influence never regenerates inside them; a 52-week run produced no wars, which is an
artifact and not a finding. The game also throttles to roughly two in-game hours per real
minute when its window is unfocused, so a real run needs a focused window and hours of
wall-clock time.

**The lead is running that campaign now and will send back a log.** When it arrives:

1. Parse `[WAR-ENDED]` lines — `days=` is the war duration. 3 years = **252 days** (a
   Bannerlord year is four 21-day seasons). Compute the mean and the distribution.
2. Parse `[SNAPSHOT]` lines — one per in-game week. Watch `atWar` against `kingdoms` for a
   kingdom stuck at permanent war, and `alliance=` for whether any alliance ever forms.
3. Tune from the data. These constants are marked **UNVALIDATED** in
   `Diplomacy/DiplomacyConstants.cs` and are the ones to move:
   - `AiWarThreshold` (25) — war value a kingdom needs before acting
   - `WarValueLandHunger` (35) — contributes ~0 on a fresh, balanced map by design
   - `AiNonAggressionThreshold` / `AiDefensivePactThreshold` / `AiAllianceThreshold`
     (20 / 45 / 70) — nothing reached the alliance threshold in testing, which is the most
     likely thing to be wrong
   - `WearinessDecayPerDay` (0.15) and `AiMaxWearinessToExpand` (30) — together these were
     the dominant brake on new wars in every trace

**Likely finding to expect:** no alliance formed in any test. Either the threshold of 70 is
too high, or `PactValue` cannot reach it — `SharedThreat` is the biggest term at weight 60
and it is zero whenever a kingdom has no enemies, which is exactly the peacetime state in
which you would want alliances to form. Look there first.

---

## Choose what to do next

**A. Wait for the log and tune.** The highest-value work, and it closes Phase 1. Needs the
lead's file.

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
