# Diplomacy & Intrigue — Roadmap

Three pillars, chosen by the project lead: **inter-kingdom diplomacy**, **court intrigue**, **espionage**. Standalone (no BUTR Diplomacy dependency), English UI.

The ordering below is not arbitrary. Each phase produces something playable on its own, and each later pillar consumes data the earlier one produces — intrigue needs war exhaustion to argue about, espionage needs treaties and grievances to sabotage.

---

## Phase 0 — Foundation ✅ done

Buildable, loadable, save-safe skeleton.

| Delivered | Where |
|---|---|
| `net6.0` project pinned to game v1.4.8, auto-detecting the install | `Directory.Build.props`, `src/DiplomacyIntrigue/DiplomacyIntrigue.csproj` |
| Module manifest with all four framework dependencies | `module/DiplomacyIntrigue/SubModule.xml` |
| Entry point with failure isolation and health flag | `src/DiplomacyIntrigue/SubModule.cs` |
| File logger, log rotation, in-game notify | `Core/Log.cs` |
| Save root + schema versioning + migration + orphan cleanup | `Core/ModState.cs`, `Core/ModSaveDefiner.cs` |
| War ledger tracking vanilla war/peace, with mid-campaign backfill | `Behaviors/CoreBehavior.cs` |
| MCM settings with safe fallback | `Core/ModSettings.cs` |
| Console diagnostics | `Core/DebugCommands.cs` |
| Build / deploy scripts | `scripts/` |

| Load pre-flight diagnostic (catches failures that happen before any module code runs) | `tools/LoadProbe` |

**Acceptance: verified in a live campaign on 2026-09-15.**

| Check | Evidence |
|---|---|
| Module loads | `OnSubModuleLoad complete. Harmony patches applied.` |
| Startup notice shown | `Diplomacy & Intrigue v0.1.0 loaded.` |
| Behaviors registered | `Campaign behaviors registered.` |
| Console commands live | `diplomacy.status` → `healthy: True`, schema v1 |
| War ledger populated | `Backfilled 4 pre-existing war(s)` — the four 1084 starting wars |
| Save → load round-trip | after reload: `Loaded: 0 treaties, 4 war records, schema v1` **and no backfill line**, proving the records came from the save rather than being re-created |

One real bug was found and fixed during this: the module was targeting `net6.0`, which the
game cannot load. See ARCHITECTURE §1.1.

---

## Phase 1 — Diplomacy core ✅ accepted 2026-09-23

The pillar everything else hangs off. Playable target: *wars end for reasons, and peace can be shaped.*

**1.1 War exhaustion** ✅ **implemented** — accrues from elapsed time, battle casualties
(divided by kingdom strength, so it is relative to size), lost towns and castles, raided
villages, sieges endured and enemy-occupied fiefs. War score tracked separately, with a
daily drift toward zero so stalemates trend to a white peace. On peace, half the exhaustion
carries into a per-kingdom weariness pool that decays at 0.15/day.
Code: `Diplomacy/WarExhaustion.cs`, `Diplomacy/DiplomacyConstants.cs`,
`Behaviors/WarExhaustionBehavior.cs`, `Models/KingdomWeariness.cs`.

**1.2 Casus belli** ✅ **implemented** — every war now carries a justification, and
legitimacy (0–1) is the single number the rest of the mod reads. Fief ownership history is
recorded so ancestral claims can exist at all; claims are granted by conquest and by raids,
renewed rather than stacked, and expire. Claim fabrication is implemented end to end
(cost, 30-day timer, 20% exposure with relation damage and a counter-claim); until the 1.8
UI exists its only entry point is `diplomacy.fabricate_claim`.
Code: `Diplomacy/FiefHistory.cs`, `Diplomacy/ClaimRegistry.cs`, `Diplomacy/CasusBelli.cs`,
`Behaviors/ClaimsBehavior.cs`, `Models/Claim.cs`, `Models/FiefOwnershipRecord.cs`,
`Models/FabricationAttempt.cs`.

Verified in a live campaign: war declaration opens a record with correct aggressor/defender
attribution; a fief transfer closes the old ledger row and opens a new one; a fief *gifted*
grants no claim (only `BySiege` does), which is the intended distinction between a grievance
and a transaction; schema migrated v1 → v3 on a pre-existing save without data loss.

**Verified in a live campaign** (1084 sandbox save, reloaded across restarts):

| Check | Result |
|---|---|
| War declaration opens a record | `Vlandia vs Battania` with correct aggressor/defender |
| Daily exhaustion accrual | 30 days → **2.40** on every war, exactly `30 × 0.08` |
| Continued accrual | +5 days → **2.80**, matching `2.40 + 5 × 0.08` |
| Peace closes the war | dropped from the ongoing list; record kept |
| Weariness carry-over | `2.40 × 0.5` → **1.20** each side, logged on the peace |
| Weariness decay | 5 days → **0.45**, matching `1.20 − 5 × 0.15` |
| Fief ledger | transfer closed the old row and opened a new one |
| Gift ≠ grievance | a fief given by kingdom decision granted **no** claim; only `BySiege` does |
| Fabrication guards | refused cleanly when the player holds no kingdom |
| Save round-trip, new schema | 4 war records, 2 weariness entries, 120 fief records, all values identical after a process restart |
| Migration | a schema v1 save migrated to v3 with its war records intact |

Accrual was driven through `diplomacy.tick_days`, which calls the same
`WarExhaustion.DailyTick` the campaign's daily tick calls — a campaign day takes minutes of
real time to pass, which makes rate verification and the Phase 4 balance pass impractical
otherwise.

Still unverified, and honestly so: **battle casualties** and **siege capture** feed
exhaustion and war score through `MapEventEnded` and `OnSettlementOwnerChangedEvent`, and
neither can be triggered from the console — they need a real battle and a real siege. Both
are covered by the Phase 4 long-run task.

**1.3 Treaty engine** ✅ **implemented** — all six types live: signing with per-type
duration and cost, expiry paying a trust dividend, breach, mutual dissolution, tribute
transfers every 7 days with default handling, and an automatic truce recorded whenever a
peace is made. Enforcement is the interesting half: a treaty that forbids war actually
prevents it.
Code: `Diplomacy/TreatyRegistry.cs`, `Diplomacy/TreatyEnforcement.cs`,
`Behaviors/TreatyBehavior.cs`, `Patches/`.

Two decisions worth recording:

- **Vassalage carries military service.** It did not at first, which left it nearly
  identical to a tributary pact - pay and be left alone. A tributary buys peace; a vassal
  buys protection and owes troops in every one of its patron's wars.
- **Blocked on the routine path, defiance on the deliberate one.** A kingdom never wanders
  into a forbidden war because the vanilla decision AI rolled it - `DeclareWarDecision.IsAllowed`
  is narrowed so the proposal never reaches a vote. But breaking a treaty on purpose is
  always available and never blocked; it just costs reputation. Accidents are noise,
  defiance is a story beat.

**1.4 Diplomatic trust** ✅ **implemented** — one value per **ordered** kingdom pair, because
"Vlandia trusts Battania" and the reverse are different facts and diverge sharply after a
betrayal. Relation already covers feeling that fades; trust is reputation. It did not decay
at all until run 06 showed it saturating - since then it drifts toward zero if untended
(goodwill in ~2 years, grudges ~4x slower) and falls during a war (design 01 §4.1). Below −20
nobody will sign anything but a truce or the terms that end a war, and those are never
refused - stopping a war has to stay possible however badly the parties behaved.
Code: `Diplomacy/TrustRegistry.cs`, `Models/TrustRecord.cs`.

**Verified in a live campaign:**

| Check | Result |
|---|---|
| Sign a non-aggression pact | active, expiry two years out |
| War refused while it stands | `campaign.declare_war` printed its own success, but `ApplyByDefault` was refused and logged; no war stance, treaty still active |
| Break it deliberately | allowed; victim trust **−35**, all six other courts **−12**, and the victim gained a `BrokenTreaty` claim at legitimacy 0.95 |
| Trust is directional | only `X → Sturgia` records were created; Sturgia's own view of others untouched |
| War allowed afterwards | yes - defiance has a price, not a lock |
| Trust floor | a new alliance was refused: "Vlandia does not trust Sturgia enough to sign anything but a truce" |
| Vassalage | subordinate recorded explicitly, tribute scheduled; the vassal cannot declare war on anyone, nor on its patron — **all of it set up by `diplomacy.treaty`, the debug command** |
| Double subordination | refused - "Battania is already subordinate to another kingdom" |
| Save round-trip | 2 treaties, 7 trust records, 1 claim, 120 fief records after a process restart; the patron/client link still resolved |

Not verified, and honestly so: anything gated on the campaign **clock** - treaty expiry and
its trust dividend, tribute actually changing hands on day 7, the two-year peace dividend.
`diplomacy.tick_days` drives the daily upkeep but cannot move `CampaignTime.Now`, so these
need real campaign days, like the battle and siege paths. All of it is covered by the
Phase 4 long-run task.

**1.5 Peace table** ✅ **implemented** — peace is a package, not a yes/no. Two numbers decide
different halves, and keeping them separate is the point: **war score** sets what the winner
may demand, **exhaustion** sets whether the loser signs. A winner who is ahead but worn out
takes a white peace; a loser who is fresh refuses to be dismembered and fights on.
Code: `Diplomacy/PeaceTable.cs`, `Models/PeaceTerms.cs`.

Deviation from this spec, deliberately: the design listed demand **tiers**
("45-70: one castle OR tribute and prisoners"). That became a **point budget** - each demand
costs war-score points and the package must fit what the war earned. Same intent, no
exclusive-or branches, and a new demand type is one constant instead of a rewritten table.
The reference points still hold: 45 buys a castle, 90 buys two towns.

**1.6 Call to arms** ✅ **implemented** — alliances, defensive pacts and vassalage pull
signatories into wars. A defensive pact never answers a war of conquest. Refusing an
alliance costs trust and nothing else, because an alliance that cannot be declined is a
suicide pact the AI would never sign. Refusing as a **vassal** breaks the vassalage, because
service is the substance of that bargain.
Code: `Diplomacy/CallToArms.cs`, `Behaviors/CallToArmsBehavior.cs`.

> **Closed by 1.9 on 2026-09-16. Kept here because the shape of the mistake is worth
> remembering.** For three balance runs the *mechanics* of vassalage were implemented and
> verified in the live game — a vassal could not declare war, tribute was scheduled, the call
> to arms pulled it into its patron's war — while the *event* of one kingdom subordinating
> another had no route at all. Every treaty-creating call site was traced and only
> `DebugCommands` could create a `Vassalage` treaty: `PeaceTerms` had no vassalage field and
> the AI never proposed subordination. Run 02 confirmed it from the other side, `vassalage=0`
> across all 157 weekly snapshots.
>
> Calling 1.6 implemented was fair. Leaving the impression that vassalage was *playable* was
> not: a feature with no way to occur is not finished, and this one was the foundation the
> whole hegemony design sits on. 1.9 added the routes - peace table at war score 90, voluntary
> submission, poaching.

Two things worth recording:

- **The cascade is capped at one step.** When an ally joins, the engine raises the war event
  again; unguarded, the war would ripple through allies of allies until half of Calradia was
  involved - which is the exact vanilla failure this mod exists to remove.
- **The obligation runs both ways.** A kingdom dragged into someone else's war is released
  from it when that someone makes peace. Found by testing: Battania answered Vlandia's call,
  Vlandia made peace, and Battania was left fighting alone for a cause it never chose and
  could not end. `WarRecord.CalledBy` now records who called, and peace releases the
  followers.

**Verified in a live campaign:**

| Check | Result |
|---|---|
| Vassal answers the call | Battania joined Vlandia's war on Sturgia; logged and reflected in the game's own war list |
| Joined war is legitimate | recorded as `DefendAlly`, legitimacy **1.00** - honouring a pact never makes a kingdom look like an aggressor |
| A held claim outranks the engine's reason | Vlandia's war filed as `BrokenTreaty` (0.95) rather than the engine's generic `Default` (0.00) |
| Demand budget | war score 0 → "nothing has been earned, white peace only" |
| Casus belli gates land | a fief demand was refused: a `BrokenTreaty` claim at 0.95 legitimacy still does **not** entitle anyone to territory |
| Willingness gate | white peace refused at exhaustion 0.0 against a threshold of 60.0 |
| Peace at the threshold | at exhaustion 64.0 the same offer was signed |
| Peace pipeline | truce recorded, war closed, `64.00 × 0.5` → 32.0 weariness each side |
| Truce blocks re-declaration | "the Truce with Sturgia forbids it" - the vanilla habit of re-declaring the next day is gone |
| Followers released | "Battania leaves the war against Sturgia now that Vlandia has made peace" |
| Save round-trip | 4 treaties, 6 war records, 13 trust records, 120 fief records after a process restart |

Two bugs were found by reading the live log rather than the code, both now fixed:

1. `CasusBelli.FromDeclareWarDetail` only mapped two of the eight engine reasons, so a war
   joined by honouring a pact was filed as naked aggression at legitimacy 0.00 - while the
   calling code's own comment claimed it mapped to `DefendAlly` at 1.00. Code and comment
   disagreed, and the log was the only place that showed it.
2. Campaign event listeners do **not** fire in registration order. A trace showed
   `CallToArmsBehavior` handling a war declaration before `CoreBehavior` did, for the same
   event. Nothing depends on the order, but `CoreBehavior` claimed it did; the comment was
   wrong and is now corrected with a warning not to rely on it.

**1.7 AI diplomacy** ✅ **implemented** — one evaluation per kingdom per week, at most one
action, usually none. Priority order matters: a realm that needs out of a war does not go
shopping for allies. Seek peace → offer a pact → demand tribute → declare war → nothing.
Work is spread across seven daily slots rather than done in a weekly burst, so moves arrive
through the month instead of all at once. The player's own kingdom is never evaluated.
Code: `Diplomacy/AiDiplomacy.cs`, `Behaviors/AiDiplomacyBehavior.cs`.

Peace-seeking is a real negotiation: a white peace first, then a concession ladder -
prisoners, indemnity, tributary pact, a castle, a town - stopping at the cheapest package
the other side will take, and never conceding past what their victory entitles them to.

**1.8 Diplomacy UI** ✅ **implemented** — reachable with **Ctrl+D** on the map, or
`diplomacy.menu`. Our wars with exhaustion and war score, our agreements and what each
obliges, our claims and what they allow, and per-kingdom actions: propose a pact, negotiate
peace with a demand budget, renounce a treaty, fabricate a claim. A player who is a vassal
rather than a ruler gets a view only.
Code: `UI/DiplomacyMenu.cs`, `Diplomacy/ExhaustionBands.cs`.

Built on the game's own selection dialogs rather than a custom Gauntlet screen. That is a
deliberate trade: a hand-built screen looks better and is the eventual goal, but it is also
the most fragile thing a Bannerlord mod can own - it breaks on game updates and takes the
whole screen stack with it. Native dialogs cannot, need no prefab XML, and deliver the full
feature set now.

**Enemy exhaustion is shown as a band, never a figure** - the decision from design review.
The band edges are the behavioural thresholds themselves, verified in game:

```
[#....] Fresh      from 0    nothing is pressing them
[##...] Strained   from 20   feeling the cost, but not yet politically
[###..] Weary      from 40   their court is starting to press for peace
[####.] Exhausted  from 60   will accept a white peace
[#####] Breaking   from 80   will accept unfavourable terms
```

**Verified in a live campaign:**

| Check | Result |
|---|---|
| One week of AI diplomacy | 3 kingdoms sought peace, 4 signed pacts, 1 did nothing |
| Peace at the threshold | all three were at exhaustion 66.8, just past the 60 gate; all three ended in white peace |
| Pact valuation | non-aggression pacts signed at mutual values 28–43 against a threshold of 20; nobody reached the alliance threshold of 70 |
| Both sides must want it | `pact_value` shows each direction; the lower number decides |
| War gates | `war_value` prints every term and names the gate that blocks: Aserai at 1.26 strength and value 28.7 was blocked purely by its own exhaustion of 66.8 |
| Menu renders | screenshot confirms title, sections and the vassal-only notice |
| Band mapping | `diplomacy.bands` reproduces the table above from the same constants the AI reads |

## The un-tuned constants, and an honest correction

`AiWarThreshold`, `WarValueLandHunger` and the pact thresholds started life as first-cut
numbers. Balance runs 01–07 retuned many of them against real campaign data — the evidence
is in the per-feature tables below and in [docs/balance/](balance/) — but **21 constants in
`DiplomacyConstants` still carry an un-tuned marker in their own doc comment**, and that
marker is the authority on which is which.

The limit that made this hard is still there: `diplomacy.tick_days` and `diplomacy.ai_week`
drive the real upkeep and the real evaluation but **cannot move `CampaignTime.Now`**, so
inside them treaties never expire, claims never age out and influence never regenerates. A
52-week simulated run once produced no wars at all and that read as a finding; it was an
artifact. Both commands now print the caveat when asked for a long horizon.

An honest correction, kept here rather than quietly fixed: an earlier pass justified two of
these constants as "measured". They were not — the run behind that claim was confounded by
exactly the frozen clock above. The comments have been corrected.

**1.9 Submission and hegemony** ✅ **implemented**, partly verified — the mechanism by which
one kingdom rises over others. Spec and the selection from the lead's source document:
[design/04](design/04-hegemony.md).

A hegemon is **derived, never declared**: any kingdom holding one active vassalage is one,
several coexist by construction, and there is no title state to keep in sync. Three routes in
- imposed at a peace table at war score 75, offered voluntarily by a cornered kingdom at
submission value 50, or poached off a rival patron - and one new number, `Hold`, per link.
A cornered kingdom may also kneel to the kingdom attacking it. Thresholds as of 2026-09-20; see
[design/04 §13](design/04-hegemony.md#13-one-subjugation-rung-and-a-cliff-2026-09-20-after-run-07).

| Piece | Code |
|---|---|
| The system: who holds whom, Hold and its terms, defiance, revolt, collapse, poaching | `Diplomacy/Hegemony.cs` |
| The top rung of the concession ladder at 90 | `Diplomacy/PeaceTable.cs`, `Models/PeaceTerms.cs` |
| Voluntary submission as a weekly AI move, and the player's prompt to accept or refuse | `Diplomacy/AiDiplomacy.cs` |
| Hold-gated service, the cascade cap, excused-versus-defiant refusals | `Diplomacy/CallToArms.cs` |
| Foreign policy finally enforced for treaties, withheld tribute, no chains of patrons | `Diplomacy/TreatyRegistry.cs` |
| `Hold`, defiance marks, the revolt clock | `Models/Treaty.cs` ids 14-17 |

**Verified in a live campaign (2026-09-16),** driving the real route rather than a debug
shortcut. `Southern Empire vs Aserai` stood at war score 99.2 after 432 days, and
`diplomacy.offer_peace Southern Empire | Aserai | vassalage, prisoners` produced:

```
Peace signed: submit as a vassal paying 500 per period; release prisoners

Hegemons: 1   links: 1
Southern Empire holds 1 vassal(s):
  Aserai  hold 35.0  marks 0  tribute 500  until Winter 3, 1136
      base 40  fear +7.8  protection +0.0  trust +15.0  tribute -3.8
      wars +0.0  rival +0.0  culture -10.0  => 48.9
      resisting - refuses summons, withholds tribute
```

The first hegemon in the project's existence, and every term of the Hold target is visibly
doing its job: Southern Empire is stronger (+7.8), Aserai trusts it (+15.0), the two are of
different cultures (-10.0), the tribute is mild against Aserai's holdings (-3.8). A coerced
vassal starts at 35 - sullen, withholding tribute - and drifts toward 48.9 at a point a day,
crossing into service in a fortnight. That is the designed shape: submission at swordpoint
starts badly and settles only if the patron is strong and does not abuse it.

**Not verified, and it cannot be from a tool call:** everything measured in dates. The revolt
countdown, renewal at the end of a term, and defiance marks being forgotten all read
`CampaignTime.Now`, which no console command can move. Poaching and the cascade cap need a
world with several hegemons, which run 04 is the first chance to produce.

A gap this found, worth recording: `diplomacy.tick_days` drove only exhaustion and claims, not
the treaty upkeep - so Hold sat unchanged through 20 simulated days and looked like a broken
drift. The campaign's own daily handler was always calling it. The command now runs the full
daily set and says which things a frozen clock still cannot move.

**1.10 Hegemony competition and UI** ✅ **implemented** — rival patrons courting each other's
neglected vassals (below Hold 40, for -30 trust and a casus belli), the call-to-arms cascade
cap at half the vassals nearest the target, collapse when a patron is destroyed (treaties
dissolved rather than broken, plus a two-year grace between the freed), and a hegemony view in
the Ctrl+D menu showing our patron, our vassals with their Hold, and every rival sphere.
Vassal-party summons (design 04 §8) is the one piece deliberately left out: it is the most
intrusive and the least load-bearing, and it belongs after Phase 2 gives refusal political
weight.

> **Reworked after run 04's design review (2026-09-16), unverified in a run.** A patron is now
> actually called to defend its vassal, submission reads the patron's ability to protect,
> withheld tribute is defiance, outsiders balance against a dominant sphere, and a revolt can
> carry the other resentful vassals with it. Detail, live evidence and what stays unverified:
> STATUS-history.md, "History — how each run changed the design" §3.

**1.12 Power: ambition, coalitions, greed** ✅ **implemented**, verified piecewise, not yet in a run
— the lead's design of 2026-09-16. Strength against the world makes a ruler ambitious; the rest of
the map finds it easier to stand together against the strongest, and alliances now deter because
the war valuation reads whole sides; a ruler past a quarter of the world's (smoothed) strength turns
greedy, takes no new vassals and may tear up a vassal's oath to conquer it; its vassals dread it.
Annexation only through war, and a kingdom that loses its last settlement is eliminated.
[design/06](design/06-power.md); evidence in STATUS-history.md, "History — how each run changed the design" §3c.

**1.11 Take inter-kingdom diplomacy from vanilla** ✅ **implemented**, partly verified — the
lead's directive that our diplomacy overrides *all* vanilla diplomacy, plus the two bugs run
02 exposed. Run 02 measured **86.8 % of wars ending without our peace table**, median length
**6 days**, because we had taken war declaration and left vanilla holding peace, alliances,
trade agreements and call-to-war. Inventory and levers:
[design/05](design/05-vanilla-override.md).

Four `GameModel` overrides and no new Harmony patch — `KingdomDecisionPermissionModel`,
`DiplomacyModel`, `AllianceModel`, `TradeAgreementModel`. The permission model also feeds
`KingdomDiplomacyVM`, so a war our treaties forbid greys out the vanilla button **and says
why**. The two existing patches stay: the model has no proposer argument and so cannot
express "the AI may not, the player may".

Plus `PeaceTable.WinnerWouldAccept` — the missing half that made the concession ladder
unreachable (`terms=white_peace` 13/13 in run 02: the side suing offered a white peace and
the only willingness check asked that same side) — a sue-for-peace path in the menu, without
which blocking vanilla peace would strand a losing player, and a one-chosen-war-at-a-time cap
on the AI.

| Check | Result |
|---|---|
| Models install and are reached | live, 2 in-game days: `vanillaPeaceRefused=5 vanillaAlliancesRefused=6 vanillaTradeRefused=1 vanillaCallToWarRefused=74` |
| Our own peace still works | `Western Empire sued for peace with Aserai at exhaustion 64.6: white peace` — correct, war score 0.00 leaves nothing to collect |
| War declaration still works | `Northern Empire declared war on Southern Empire (ReclaimAncestralLand, legitimacy 0.70, value 46, cost 52 influence)` |
| One-war cap | three further simulated weeks produced 24 × `None` and no second war. **Weak evidence**: the clock was frozen, so influence never regenerated either, and the two cannot be told apart from outside. `diplomacy.war_value` now prints the cap as a gate so run 03 can attribute it |
| Wars now last; the ladder fires | **not verified.** Needs run 03 — a two-day sample says nothing about median war length |
| Dormant wars lapse | **not verified in game.** No console command can age a war: `CampaignTime.Now` cannot be moved, and a war's age is read from it. Verified by construction only |


---

## Phase 1 — accepted by the project lead, 2026-09-23

Phase 1 is **closed**. The bar was *"in a 10-year AI-only campaign, wars average under ~3
years, at least one alliance forms and holds, and no kingdom sits at permanent total war"*,
and balance run 01 cleared it over 28 in-game years — 340 weekly snapshots, 247 wars, 0
errors. Runs 02–07 then reworked what run 01 exposed; **run 07** (20.8 in-game years, 100
wars, 0 errors, and the first vassal links this project ever produced) is the current
reference measurement: [balance/run-07.md](balance/run-07.md).

The lead accepted the pillar on 2026-09-23 so Phase 2 can start. **Acceptance is a decision
about priority, not a claim that everything under it is measured.** What is carried forward
unresolved, stated plainly:

| Carried debt | State | Lands in |
|---|---|---|
| **§13** — merged subjugation rung, the cliff at 75, reachable ceiling, indemnity resize, threshold 50 | **measured by balance run 08** (2026-09-26, 20 in-game years, 0 errors): all five questions of [design/04 §13.7](design/04-hegemony.md#137-what-the-next-run-must-answer) answered. Tribute has vanished from the peace table, and 2 imposed links formed in ten years, not the multiple expected. Whether that calls for a change is the lead's call | the lead |
| Strength margin on `IsStrongEnoughToHold` ([design/04 §13.6](design/04-hegemony.md)) | **Run 08 (2026-09-26): no link doomed at signing in 20 in-game years** (lowest starting target 24.1, at ratio 1.10). Left as it is | Phase 4 |
| Whether the indemnity price should bite ([design/04 §13.4](design/04-hegemony.md)) | **undecided** — 8 points per 1,000 denars is trivial against a late-game treasury | Phase 4 |
| Peace-table multi-selection checklist against a real budget | **unverified in a live game** — `save007`'s wars are all war score ~0, so only the white-peace short path has been seen on screen | first run that produces a war with terms |
| AI → player incoming peace-offer inquiry | **unverified in a live game**, same reason | as above |
| Battle casualties and siege capture feeding exhaustion | wired, reviewed, exercised in long runs — never asserted against hand-computed values | Phase 4 |
| Realm tab widens the tab strip into the leader portrait's caption | cosmetic, unfixed | whenever the court UI (2.7) touches that screen |

**Run 08 was run on 2026-09-26** and answers §13.7's five questions ([balance/run-08.md](balance/run-08.md) §5,
[design/04 §13.7](design/04-hegemony.md#137-what-the-next-run-must-answer)). The §13 row above
stays open on the indemnity price, which run 08 does not settle.

---

## Phase 2 — Court intrigue 🔄 started 2026-09-23

Playable target: *being a king is a political problem, not just a military one.*

**2.1 Grievances** — event-sourced records with decay: fief given to a rival, war the clan opposed, humiliating tribute, a relative left in captivity, forced levies.

**2.2 Vassal loyalty** — derived from grievances, relation, fief wealth, and war exhaustion. Drives defection risk and vote behaviour. Derived, never saved, and it applies to the player's clan on the same terms as any AI clan (§9.2).

**2.3 Court blocs** — clans coalesce into the agendas in `Models/CourtAgenda` (doves, hawks, autonomists, centralists, pretenders) around a leading clan. Blocs vote as units, which makes kingdom decisions predictable enough to play against.

**2.4 Crown legitimacy** — a per-kingdom pool: illegitimate wars, lost fiefs, broken treaties drain it; victories and just wars restore it. Low legitimacy unlocks pretender bids.

**2.5 Succession crises** — on a ruler's death, competing claims resolve through bloc support rather than a silent assignment.

**2.6 Civil war** — a strong pretender bloc can secede into a rival kingdom, taking its fiefs. The end state of unmanaged internal pressure. *Built instead as war inside the kingdom, which does not split (design/07): **2.6** the internal war and **2.6b** a house divided by its succession, verified live 2026-09-23/24; **2.6c** conceding and changing sides for gold, shown on the Court tab, run live 2026-09-24, and from the two places left unseen (an AI leader's offer to the player's house, the player as claimant) on 2026-09-25. Secession stays the last rung of design/07 §2 and is not built. What is still unverified is listed in STATUS.md.*

**2.7 Intrigue UI** — court screen: blocs, loyalty, grievance ledger, legitimacy — in full for your own court, as a qualitative band for rivals (§9.1). **Own court: built and verified live 2026-09-23** as a seventh Kingdom-screen tab, "Court" (STATUS-history.md). **Rival court: built and verified live 2026-09-23** — a bands-only "Court" section on the kingdom's Encyclopedia page, every edge a behavioural threshold (`CourtBands`).

**2.8 Statecraft** — the six political skills count in the mod's own judgments, and the acts it added train them ([design/08](design/08-statecraft.md)). Built after Phase 3, on the lead's delegation of D1-D10 (2026-09-26). **S0-S2 built and run live 2026-09-26**: every term predicted by hand and matched in game, the player's war now costs what the AI's does, Firebrand and Silver Tongue reach the mod's own acts. **S3**, the on/off measurement, is balance run 08 (design/08 §17). It ran on 2026-09-26 with 0 errors: war lengths and white peace are unchanged and XP drift is negligible, but §12's acceptance is not yet met (total wars +20%, all obligation wars; pacts 47 against 31, which needs a second pair; points 2 and 4 not measured). S4 offices and S5 traits wait on S3. *S4 is now design/09's C2 (2026-09-26): one concept of a court seat.*

**2.9 Court verbs** (R-2 of the 2026-09-24 review) — the ruler's acts on the court, decided by the lead on 2026-09-26 ([design/09](design/09-court-verbs.md)): **C1 make amends**, **C2 offices and patronage**, **C3 tribute per vassal**, each priced in influence and gold scaled by skill (the lead's rule, CLAUDE.md §3). **C1 built and run live 2026-09-26** (design/09 §8): prices matched by hand, the Court tab's button, the save round trip, the AI's weekly pass. C2 and C3 not built.

**Acceptance:** an AI kingdom that loses a long illegitimate war visibly fractures — blocs shift, then either sues for peace or splits. The player can survive it by managing grievances.

*Measured 2026-09-26 with C1, and not met in its hardest form (design/09 §8).* Amends move a house across a band and can pull a great house out of a pretender bloc; in Battania, one tick from rising after a contested succession, two amends took the bloc from 61% to 56% and the rising from 5 houses to 4, and the rising still came. What held was crown legitimacy (25 against 35), which no court verb reaches, and relation, which sank three houses below 25. Whether the line requires surviving a court already at the trigger is the lead's call (TODO.md item 1).

### What Phase 1 already left waiting for it

| Hook | Where |
|---|---|
| `ExhaustionCourtPressure` = 40 — the doves threshold in [design/02 §3](design/02-intrigue.md) | `Diplomacy/DiplomacyConstants.cs` |
| A caught fabrication already computes its legitimacy penalty and only logs it, "pending the Phase 2 legitimacy pool" | `Diplomacy/ClaimRegistry.cs:283` |
| Policy votes, annexation, clan expulsion and king selection were **left alone on purpose** for Phase 2 to extend | `GameModels/ModKingdomDecisionPermissionModel.cs:29` |
| `EnableIntrigue` settings toggle, already shipped and defaulting on | `Core/ModSettings.cs:31` |
| The `Hold` formula wants a crown-legitimacy term | [design/04 §1.2](design/04-hegemony.md) |
| `CourtAgenda`, `SpyMissionType`, `MissionOutcome` — enums written in Phase 0 and already registered at definer ids 23–25 | `Models/Enums.cs`, `Core/ModSaveDefiner.cs` |
| `AiDiplomacy.TryDemandTribute` accepts on strength ratio and trust alone, with no sense of the target court's willingness | **done 2026-09-25**: the target's court answers (`Intrigue/TributeCourt`), and a player-ruled target is asked instead of signed for. Verified live; STATUS-history.md |

Two Phase 1 pieces are parked until Phase 2 makes them mean something: **vassal-party
summons** ([design/04 §8](design/04-hegemony.md) — the most intrusive and least load-bearing
part of hegemony) and **titles** (Emperor, Khagan), which sit on top of legitimacy at 2.4.

### Save ids this phase may take

The frozen-id rules in CLAUDE.md §3 apply unchanged. Free inside the reserved block
`2749100`–`2749199`: **class ids from 10**, and `ModState` **properties from 11**. The three
intrigue enums are already defined. Every new savable type needs a class definition **and**
a container definition in `ModSaveDefiner` — a missing container definition crashes on save,
which is the single most common way to break a Bannerlord mod.

### Design decisions, taken before any Phase 2 code

[design/02 §9](design/02-intrigue.md) carried four questions. Three were settled by the
project lead on 2026-09-23; the reasoning is in that section.

| # | Question | Decision |
|---|---|---|
| 1 | Does the player see *rival* kingdoms' internal politics without espionage? | **Band for rivals, full ledger for your own court.** Exact rival figures are Phase 3 `ReadCourt`'s to sell |
| 2 | Is the player's own clan subject to this when serving another king? | **Yes**, on the same terms as any AI clan |
| 3 | Kingdom decisions: extend `KingdomDecision` or replace it? | **Extend**, until it visibly constrains us |
| 4 | Civil-war trigger thresholds (§6) | **Still open** — guesses by admission, deferred to a long AI-only run. Blocks nothing in 2.1–2.5 |

---

## Phase 3 — Espionage

Playable target: *information and subversion are a third way to fight.*

**3.1 Spy networks** — per kingdom-pair strength, built up over time, decayed by enemy counter-intelligence. *Owned per clan, not per kingdom pair (the lead's call, design/03 §9). **Built and verified live 2026-09-25**: founding, handlers, the weekly growth paid from the owner's purse, daily decay, save round trip (design/03 §10).*

**3.2 Missions** — the types in `Models/SpyMissionType`: scout armies, read court, sabotage garrison, spread dissent, bribe a lord, forge letters, steal treasury, assassinate. Resolution from network strength + agent skill (Roguery/Charm) vs target counter-intelligence. *Built and verified live 2026-09-25, all eight missions (BribeLord and ForgeLetters with 3.5).*

**3.3 Counter-intelligence** — passive defence a kingdom invests in; determines exposure chance. *Built and verified live 2026-09-25, a fresh-process save round trip included. A realm's ruler pays a weekly budget, +1 per 1,500 paid; the "security focus" term is dropped (the lead's calls, design/03 §9 decisions 9-10).*

**3.4 Exposure as diplomacy** — a burned network is a *diplomatic incident*: the victim gains the `EspionageExposed` casus belli and trust drops. This is the cross-pillar hinge that keeps espionage from being a free action. *Built with 3.2 and verified live 2026-09-25: a mission that could be caught for free was not worth allowing.*

**3.5 Cross-pillar payoffs** — `BribeLord` feeds Phase 2 defection; `ForgeLetters` manufactures grievances; `ReadCourt` reveals pending kingdom decisions. *Built and verified live 2026-09-25 (design/03 §10 has the five checks and their results). A bribed house loses 20 loyalty for two years and takes the rising's side if an internal war starts in that window; forged letters are a weight-8 grievance on a house the player picks; a live ReadCourt fills the Encyclopedia's ledger with exact figures. The lead's three calls and the check list are in design/03 §9-§10.*

**3.6 AI espionage** *(design/03 §8's numbering; this entry was missing here)* — AI realms running their own networks, counter-intelligence and operations under the player's rules. *Built 2026-09-25 and compiled clean; **not run in game**. One network per AI ruling house against a clear rival, operations only at ≤10% exposure, assassination only at war on a field commander, and an AI bribe reaching the player's house becomes an offer (the lead's calls, design/03 §9 decisions 11-13). Run live 2026-09-25: plans, execution and the offer hold, but **vanilla still takes AI handlers** - one made a governor despite the GameModel override, others given parties within days - so AI networks do not grow. Open.*

**3.7 Espionage UI** — network map, mission board, running operations. *Built and verified live 2026-09-26: a fifth Clan-screen tab, Intelligence, with a list in place of a map, the board, operations under way, and overlays for planning an operation and posting a handler; a ruler-only counter-intelligence section on the Realm tab. The lead approved the mockup first (2026-09-25).*

**Acceptance:** a player can flip a border lord through bribery, and a caught operation drags them into a war they did not choose.

---

## Phase 4 — Integration, balance, release

- **Cross-pillar wiring pass** — every hinge above actually connected and logged.
- **Balance pass** — long AI-only campaign runs with telemetry from the log; tune exhaustion, costs, decay rates.
- **Compatibility** — detect BUTR Diplomacy and warn (the two are mutually exclusive by design); document every overridden `GameModel`.
- **Localization** — all strings behind `TextObject` keys in `ModuleData/Languages/EN`; community translations become drop-in.
- **Performance** — daily/weekly tick budget measured; no per-frame work.
- **Release** — Nexus page, changelog, bug-report instructions pointing at the log directory.

---

## Cross-pillar dependency map

```
Casus belli ──────────────┬─► war legitimacy ──► crown legitimacy ──► pretender bids
                          │
War exhaustion ───────────┼─► AI peace-seeking
                          └─► vassal loyalty ──► defection / civil war
Broken treaty ────────────┬─► diplomatic trust ──► who will ally with you
                          └─► BrokenTreaty casus belli
Exposed spy network ──────┬─► EspionageExposed casus belli
                          └─► trust loss
Bribed lord ──────────────► defection during civil war
Forged letters ───────────► manufactured grievance
```

---

## Standing risks

| Risk | Handling |
|---|---|
| Game patch breaks our Harmony patches | Keep patch count minimal; prefer models and events; version-stamp each patch (§3 of ARCHITECTURE) |
| Save-data corruption across mod versions | Frozen ids, `SchemaVersion` + `Migrate()`, orphan sweep on load |
| AI feedback loops (endless war or instant world peace) | Slow evaluation cadence, clamped values, balance pass with log telemetry |
| Scope creep across three pillars | Each phase ships playable on its own; no pillar starts before the previous one meets its acceptance bar |
| Conflicts with other diplomacy mods | Declared incompatible with BUTR Diplomacy; overridden models documented |
