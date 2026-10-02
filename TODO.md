# TODO

The one list of open decisions and pending work. Evidence and arithmetic live in the linked
docs; this file is only the list. Tick an item off, or delete it, when it is done.

For where the work stands overall, read [docs/STATUS.md](docs/STATUS.md). The saves are listed
there too, and only there.

## Answered — nothing to do

Kept as one line each so nobody re-opens them.

- ~~No vassal link forms~~ — **run 07 produced 7 links through all 4 routes in 20.8 years**, run
  08 five per run in ten years. The reasoning is in [design/04 §12](docs/design/04-hegemony.md)
  and [§13](docs/design/04-hegemony.md#13-one-subjugation-rung-and-a-cliff-2026-09-20-after-run-07),
  the evidence in [balance/run-07.md](docs/balance/run-07.md). The proposal to rewrite `cover` to
  weigh a patron's willingness stays **withdrawn** pending late-game evidence.
- ~~F3 defection never verified~~ — ran end to end, Autumn 1096, Hold 38.6.
- ~~`settlesWar` signing after the peace below −20~~ — exercised by all three routes that settle
  a war with an oath.
- ~~12b, the ladder ordering question~~ — closed by §13.2's cliff.
- ~~The strength margin (`Hegemony.IsStrongEnoughToHold`, no margin)~~ — **run 08 §5.4: no link
  started doomed.** The lowest starting Hold target was 24.1, the closest margin 1.10× (that link
  survived). The links that reached a target of 0 got there after signing, through protection
  and revolt, which a margin at signing would not have stopped. The margin stays where the lead
  left it. This was also R-8 in the 2026-09-24 review.
- ~~Run 08~~ — run 2026-09-26, [balance/run-08.md](docs/balance/run-08.md). It answers
  [design/04 §13.7](docs/design/04-hegemony.md#137-what-the-next-run-must-answer).

## Decided 2026-09-27 — built, waiting on run 09

The lead handed every open decision below to the tech lead on 2026-09-27 ("toàn quyền quyết
định"), except the Kingdom-UI loose ends, which are out of this pass. Each was decided, built and
compile-checked the same day; **none has run in game**. What to run, and what to expect, is
[docs/balance/run-09-runbook.md](docs/balance/run-09-runbook.md). The design docs named carry the rules.

| Item | Decision | Where |
|---|---|---|
| 1. Phase 2's acceptance | Accepted by the lead, 2026-09-27 (ROADMAP carries its debt) | ROADMAP |
| 2. The court and the AI's foreign policy (R-1) | **Wired**, every term capped under a third of the bar it feeds. Own court: Doves lower, Hawks raise the three peace bars (×0.70-×1.15) and the war value (±6). A rival's weakness, read **only through the bands** the player sees (internal war +4, crown Failing +3, claimant +2, Questioned +1, cap 6). A realm at war with itself chooses no new foreign war | design/02 §7.2 |
| R-9 | A balancing threat lowers the trust floor for **defensive pacts only**, to −30 at full pull; the AI offers a defensive pact when trust alone refuses the alliance | design/02 §7.2, design/06 |
| 3. Espionage on by default | Kept on (the lead, 2026-09-26) | — |
| 4a. Tribute at the table (§13 bands) | **Not moved.** Design 10 changes how war score is earned, so run 08's "0 of 123" says nothing about the new score. Run 09 question 3 decides it | run-09-runbook |
| 4b. Cadet branches | At a split the cadet takes the parent's influence **in proportion to the adults who leave, at most 50%**. On the code's numbers that reaches a pretender's 30% only when the ruling house held most of the court's influence - recorded, nothing more built | design/07 §5 |
| 5. Pacts under statecraft | **Run the second pair**: run 09B (statecraft off, 10 years) against 09A's first decade | run-09-runbook |
| 6. Should money bite? | **Yes.** An indemnity is priced against the loser's ruler's gold: 0.5% a point, never under 125 denars, never over 40% of it | design/01 §4.2, design/04 §13.4 |
| 7. Grace shields a war | War is checked before the grace: trust bleeds from a war's first day. **No clawback** of the +12 - the pact was honoured to its end | design/01 §4.1 |
| 8. The weakest kingdom eaten | **At most 2 tributes paid at once** (vassalage counts), in `CanSign`, so demand, peace table and console agree. **No** "don't dogpile" term. Submission is not capped | design/01 §3.3 |
| 9. F3 legal neglect | **(a) fractional**: a war the patron is treaty-bound to stay out of counts as half an ignored one. A war between two clients of the same patron does not count | design/04 §10a |
| 10. Tribute re-imposed on expiry | A receiver cannot demand tribute of the same payer for **84 days** after one ends. The peace table is not bound | design/01 §3.3 |
| 10a. Design 10 | D1 as recommended, D2 kept, D6 dropped (the lead) | design/10 §10 |
| 11. Before a release | The test levers and every state-changing command **need cheat mode** (`CampaignCheats.CheckCheatUsage`); diagnostics stay open. `EnableTelemetry` **defaults off in a release build** - applied when Phase 4 cuts one | CLAUDE.md §2 |
| 12. The 2026-09-24 review | Merged; its register is current. R-3 (agenda bias, Crown party), R-5 (rally round the flag): **no**. R-4: **no mean reversion**; the dividend now needs a real year of unbroken peace. R-6: **yes to both** (fiefs lost wear an internal war's side down; a crown win restores what the rebels took). U-2 petitions, U-8 secession, U-9 the loser's fate: **deferred** | review decisions.md |

Found and fixed in the same pass, not on any list before: the bribe window was 730 calendar days
(8.7 campaign years) instead of two campaign years; the trust peace dividend paid a peace that had
been broken and remade; a foreign war kept charging a retaken fief daily; an open bribe offer was
re-rolled after a reload (now saved, `SpyMission` 12); stale figures in six design docs.

## Still open

- [ ] **Phase 3 and "the AI plays by the same rules"** (the lead's question, 2026-09-27). The rule
      layer is shared (same launch, odds, prices, exposure), but four asymmetries sit outside it:
      1. AI networks never grow - vanilla makes AI handlers governors or party leaders (3.6, parked);
         the lever needs vanilla IL (`tools/CallSites` on the lead's machine).
      2. AI bribe/forgery targets are chosen from exact rival loyalty and legitimacy the player sees
         only as bands (`AiEspionage.cs:230, 242`) - the fault R-1 fixed for the war valuation.
         Recommended: read the bands, like R-1.
      3. The AI never assassinates anyone of the player's house (`:510`, the lead's decision 11).
      4. The AI never forges letters to the player's house (`:250`).
      Run 09 §4 gathers the evidence; then the lead decides 1's lever, 2, and whether 3-4 stay.
      **Decided 2026-10-01 (lead), to be built:** 1 - the lever, the station enforced, no Harmony
      (story 3.8); 2 - bands (3.9); 3 - the exemption removed, the player hero included (3.10);
      4 - an offer to believe or dismiss the letters (3.10). Odds that move after launch are told
      to the player (3.11, run 09 D-8). Phase 3 acceptance: story 3.12.
      **Merged into `development` 2026-10-02:** 3.8 (partly verified live, story §10), 3.9 (AC1-AC3
      and AC5 live), 3.10 (AC1 only; AC2/AC3 now stageable with `test_found_network`). Still to build:
      3.11, 3.12.
- [ ] **A rival's court can be read off the peace hint.** "They start listening at N" shows a
      rival's exact exhaustion (older than today) and now its court-moved bar, from which its
      Doves/Hawks balance can be inferred - against the band rule (design 02 §9 decision 1).
      Show those as bands, or accept. Found by package A; left as it was.
- [ ] **The Kingdom-UI loose ends** - the Realm tab widening the tab strip, `ConcessionLadder`'s
      `townsFirst` flag. Out of this pass by the lead's call.

## Pending work

- [ ] **Run 09**, all of [docs/balance/run-09-runbook.md](docs/balance/run-09-runbook.md): the targeted
      checks (A), the 20-year balance run and the second statecraft pair (B), the civil-war run (C).
      It closes design 10's balance run, the §13 tribute question, and the civil-war balance.

## Not verified in game

- [ ] **`ReconcileWithSiblings` (§12.4.5)** has never executed its real branch: not in run 07,
      not in run 08 (§5.5). It needs a hegemon whose new vassal is at war with one of its older
      vassals.
- [ ] **The dissolution rung, chosen by the AI.** Verified end to end by console command
      (design/04 §12.8) but no war in run 07 had a hegemon as its loser inside the affordable
      band.
- [ ] `Hegemony.DissolveChains` — needs a save that already holds a chain; none of ours does.
- [ ] Player-offer cooldown (42 days) and the inquiry callbacks — the test hero is not a ruler.
- [ ] **The `DeclareWarAction.ApplyByKingdomDecision` prefix** since it was split into its own
      file (2026-09-26): shown applied by Harmony at startup, not run. Its only vanilla caller is a
      passed war vote.
- [ ] The rest of the carried list — the peace table's two unseen surfaces, the civil-war line,
      the Court tab's gaps — is in [STATUS.md](docs/STATUS.md), "Not verified — carried".
