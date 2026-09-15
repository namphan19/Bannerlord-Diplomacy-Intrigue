# Design 05 — Taking inter-kingdom diplomacy from vanilla

Project-lead directive (2026-09-15): *our diplomacy system overrides all vanilla diplomacy.*

This document is the inventory. Every vanilla inter-kingdom diplomatic surface in v1.4.8, the
lever we take it with, and — as importantly — what we deliberately leave alone.

Everything here was read out of the game's own assemblies with Cecil, not inferred.

---

## 0. Why this became necessary

Balance run 02, 13.1 in-game years: **145 of 167 wars (86.8 %) ended without our peace table
being involved**, at a median length of **6 days**, with 46 of them dying on the day they
started at war score 0.00. We had taken over war *declaration* and left vanilla holding
*everything else*, so the mod declared wars and vanilla quietly undid them.

Two systems with opinions about the same relationship produce the worse of the two. Either
vanilla owns inter-kingdom diplomacy or we do.

## 1. The inventory

| Vanilla surface | What it does | Our lever |
|---|---|---|
| `DeclareWarDecision` | AI and player kingdom war votes | already taken: `IsAllowed` patch (proposer-aware) + `KingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` for treaty blocks |
| `MakePeaceKingdomDecision` | AI and player kingdom peace votes | `KingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms` |
| `PeaceBarterable`, `DiplomaticBartersBehavior.DailyTickClan` | **the 0-day peaces** — AI barters peace on a daily tick | `DiplomacyModel.GetScoreOfDeclaringPeaceForClan` and `GetScoreOfDeclaringPeace` |
| `DefaultDiplomacyModel.IsPeaceSuitable` | gate consulted by the decision path and the permission model | `DiplomacyModel.IsPeaceSuitable` |
| `StartAllianceDecision`, `AllianceCampaignBehavior` | **vanilla has its own alliances** | `AllianceModel.CanMakeAlliance` → false, `MaxNumberOfAlliances` → 0 |
| `ProposeCallToWarAgreementDecision`, `AcceptCallToWarAgreementDecision` | vanilla's own call-to-war — this is the `CausedByCallToWarAgreement` detail in our logs | `AllianceModel.GetScoreOfCallingToWar`, `GetScoreOfJoiningWar`, `GetCallToWarCost` |
| `TradeAgreementDecision`, `TradeAgreementsCampaignBehavior` | vanilla trade agreements between kingdoms | `TradeAgreementModel.CanMakeTradeAgreement` → false, `GetMaximumTradeAgreementCount` → 0 |
| `NoAttackBarterable` | vanilla non-aggression via barter | barter valuation flows through `DiplomacyModel`; verify in run 03 |
| `DeclareWarBarterable` | war via barter | `DiplomacyModel.GetScoreOfDeclaringWar`, plus the existing `DeclareWarAction` backstop |
| `PeaceOfferCampaignBehavior` | AI kingdoms sending the player peace offers | falls silent once peace scores are negative; verify |
| `DefaultDiplomacyModel.GetDailyTributeToPay` | vanilla tribute attached to vanilla peace | irrelevant once vanilla peace cannot happen; our tribute is our own |

### 1.1 What we deliberately do **not** touch

These are internal or non-diplomatic, and taking them would be scope creep with a real cost:

- `KingdomPolicyDecision`, `ExpelClanFromKingdomDecision`, `KingSelectionKingdomDecision`,
  annexation, `SettlementClaimantDecision` — a kingdom's **internal** politics. Phase 2 will
  extend these, not replace them.
- `ChangeKingdomAction`, `JoinKingdomAsClanBarterable`, `LeaveKingdomAsClanBarterable`,
  mercenary contracts — clans moving between kingdoms. That is Phase 2's territory too.
- Rebellion, crime rating, player hostility, kingdom creation — engine war paths we left open
  on purpose when we took war declaration, and for the same reason.
- Fief barter, prisoner barter, marriage, safe passage — not inter-kingdom policy.

## 2. Why this is mostly not Harmony

CLAUDE.md's order of preference is events → `GameModel` overrides → Harmony as a last resort.
When the war takeover was built, only the Harmony route was known. It turns out four of the
five levers above are `MBGameModel` subclasses, registered with one `AddModel` call each:

```
KingdomDecisionPermissionModel   war / peace / alliance decision gates, with a reason string
DiplomacyModel                   peace suitability and every peace/war AI score
AllianceModel                    vanilla alliances and vanilla call-to-war
TradeAgreementModel              vanilla trade agreements
```

Two consequences worth stating plainly:

1. **The permission model gives the player a reason in the UI.** `KingdomDiplomacyVM
   .GetIsProposingPeaceEnabledWithReason` and `…WarEnabledWithReason` consult it, so a war
   forbidden by one of our treaties can now grey out the vanilla button *and say why* —
   something the Harmony patch could never do.
2. **The `DeclareWarDecision.IsAllowed` patch still has to stay.** The permission model takes
   only the two kingdoms; it has no proposer argument, so it cannot express "the AI may not
   start this war but the player may". That distinction is the whole basis of the takeover, so
   the patch keeps it and the model handles the universal treaty rules. Patch count stays at
   two.

There is also `CampaignGameStarter.RemoveBehavior` / `RemoveBehaviors`, which would let us
delete `DiplomaticBartersBehavior`, `AllianceCampaignBehavior` and
`TradeAgreementsCampaignBehavior` outright. **Not used, and recorded here so the next session
does not rediscover it as a bright idea:** those behaviours carry saved data, so removing them
changes what a save contains, and a model override that makes a behaviour decline to act is
reversible while a deleted behaviour is not. It stays the fallback if a model proves
insufficient.

## 3. What replaces what

Vanilla's diplomacy is not merely switched off; every capability it had has a replacement, or
is deliberately gone:

| Vanilla capability | Replacement |
|---|---|
| Declare war | our war valuation with casus belli, influence cost and treaty gates |
| Make peace | `PeaceTable` with the concession ladder |
| Alliance | our `Alliance` treaty, with trust thresholds and refusable calls to arms |
| Call to war | our `CallToArms`, with the cascade cap |
| Trade agreement | **gone for now.** We have no trade model, and a stub agreement that does nothing is worse than none. Reconsider if the economy is ever in scope |
| Tribute at peace | our tribute fields on the treaty, paid on a real schedule |
| Non-aggression barter | our `NonAggressionPact` |

## 3.1 What the live smoke test established (2026-09-15)

Built, deployed, and driven through `di_phase1_full`. **Two in-game days** after loading:

```
date=Autumn 16, 1125 kingdoms=8 atWar=2 wars=1
vanillaWarsRefused=0 vanillaPeaceRefused=5 vanillaAlliancesRefused=6
vanillaTradeRefused=1 vanillaCallToWarRefused=74
```

What that does and does not prove:

- **Proved:** all four overrides are installed, reached, and refusing. Vanilla wanted to make
  peace, form alliances, sign a trade agreement and call kingdoms to war within two days of
  play, and was refused every time.
- **Also learned, and it corrects an earlier claim of mine:** 74 call-to-war refusals in two
  days means vanilla's own call-to-war agreements were a live source of wars in run 02. I had
  attributed all 45 `DefendAlly` wars there to our treaties; some were vanilla's, and only 28
  had a matching `CallToArms` line of ours.
- **Not proved:** that wars now last. Median war length and the concession ladder need a real
  campaign - §4. A two-day sample says nothing about either.

`vanillaWarsRefused=0` is expected here and not a failure: that counter belongs to the
war-decision patch, and vanilla only proposed ~1.1 wars a year in run 02.

## 3.2 What vanilla was quietly doing for us: dormant wars

Found while thinking through what the override removes rather than what it adds, and fixed
before run 03 could be wasted on it.

Exhaustion accrues **0.08/day from elapsed time alone**; the rest comes from casualties. So a
war between two kingdoms whose armies never actually meet needs about **750 days** to reach
the threshold at which either side will negotiate. Every real war in run 02 got there in
roughly 90 days, but only because blood was being spilled. With vanilla's peace gone, nothing
would have closed the quiet ones, and the map would have filled up with wars nobody was
fighting - which would have looked exactly like the takeover working and the war rate
exploding.

The rule: a war past **42 days** with **under 300 casualties** between both sides is dormant,
and either side may end it on white terms alone. Only white terms — indifference concedes
nothing, so a patient winner cannot extract anything by waiting. `[WAR-ENDED]` reports these
as `endedBy=Dormant`, kept separate from `PeaceTable` precisely so a balance run can tell a
settled war from a war that was never really a war.

**Unverified in game.** `diplomacy.tick_days` cannot move `CampaignTime.Now`, and a war's age
is measured from it, so no console command can age a war to 42 days. The rule is verified by
construction and by its unit arithmetic only, and run 03 is where it gets tested.

## 4. Acceptance

Run 03 answers this, and these are the numbers that decide whether the takeover is real:

1. `endedBy=External` falls from **86.8 %** to near zero. Anything above ~10 % means a vanilla
   path is still live and the inventory above is incomplete.
2. Median war length rises from **6 days** toward the 90–110 the exhaustion model predicts at
   its measured 0.65/day accrual.
3. `terms=` on `[WAR-ENDED]` stops being `white_peace` every single time.
4. No vanilla alliance, trade agreement or call-to-war agreement appears in a campaign —
   checked by logging any we see rather than by trusting the models.
5. Nothing internal broke: policy votes, clan defections, king selection and annexation still
   happen at their normal rates.
