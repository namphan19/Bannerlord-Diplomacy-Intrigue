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
      and AC5 live), 3.10 (AC1 only; AC2/AC3 now stageable with `test_found_network`), 3.11 (compile
      only), and 3.12's telemetry and runbook. Design 03 §9 records the decisions as 14-18. **What is
      left is run 11**, below.
- [ ] **A rival's court can be read off the peace hint.** "They start listening at N" shows a
      rival's exact exhaustion (older than today) and now its court-moved bar, from which its
      Doves/Hawks balance can be inferred - against the band rule (design 02 §9 decision 1).
      Show those as bands, or accept. Found by package A; left as it was.
- [ ] **The Kingdom-UI loose ends** - the Realm tab widening the tab strip, `ConcessionLadder`'s
      `townsFirst` flag. Out of this pass by the lead's call.
- [x] **Localization (Phase 4)** - built 2026-10-03, [story 4.1](docs/stories/4.1-localization.md):
      852 keys behind `DiText.T` (including all 92 prefab labels, moved into view models), English generated into `Languages/EN/di_strings.xml`, 13 language
      folders ready to drop a translation into, `scripts/check-localization.ps1` in
      `build.ps1`/`deploy.ps1`, guide in [docs/localization.md](docs/localization.md).
      **First live pass 2026-10-04** (story ST-7, §9): six surfaces seen on two saves, and it
      found **four defects no check could see** - all 653 keys were drawing `{DI_}KEY` because
      `DiText.O` built the wrong id form; three labels had come out of the conversion with U+00A7
      where the prefab had U+00B7; the Court panel's heading had lost "The"; and enum names
      (`ReclaimAncestralLand`, `DefensivePact`) were reaching the screen as text. All four fixed,
      each re-seen on screen, and rule 8 of the check now blocks the first of them.
      **Second pass, same day** (story §9a): the tool's remaining 122 candidates were read by eye
      for the first time and **32 were wrong, three of them losing English outright** - a conditional
      the tool would not rewrite was being turned into a `{VARIABLE}` with its text deleted. Three
      rules fixed, then 90 expressions keyed into **118 keys**, for **771** in all, screenshot-checked
      on the Realm, Court and Diplomacy tabs. A fourth fix - the shape rules used to run before the
      boundary rule, which mislabelled log lines as "a human has to write this" - is why the
      backlog below is not 440.
      **Third pass, same day** (story §9b): a lookup in the tool had **never matched**, so the whole
      producer list was dead and the exhaustion bands, office titles and grievance titles were being
      called log text - work this file had claimed was done. Fixing it surfaced 69 real candidates
      and a **second live enum-on-the-screen defect in eight places** (`Treaty.Type` printed
      `TributaryPact` on the war row). `Treaty.NameOf` names the six kinds behind keys in one place;
      **852 keys** now, AC1 re-checked live on all three tabs, 0 ERROR / 0 WARN.
- [ ] **Language-folder fixes from the 2026-10-04 review of this branch** (done on the branch, none seen in game):
      (1) `under_development` removed from all 13 `language_data.xml` - by IL it overwrote vanilla's flag
      and the retail game hides such a language, which could turn a player's own language into English;
      (2) every `di_strings.xml` had a comment before `<base>`, and `LoadLanguage` reads
      `ChildNodes[1].FirstChild`, so **no non-English file would ever have loaded a string** - comments now
      sit inside `<base>`, in the emitter and the fixture too; (3) `release.ps1` drops `VI`, `deploy.ps1`
      finds the game folder through MSBuild (its VI step never ran). `check-localization.ps1` rules 12-13
      block (1) and (2). **Second round, same review:** (4) the 28 keys whose whole English was one
      `{VARIABLE}` are gone - literals keyed where they are chosen (874 keys now; rule 14 blocks the shape);
      (5) `{cost}` -> `{COST}`, rule 15; (6) `DiText.English()` scope: all 94 `diplomacy.*` commands,
      `AiEspionage.Plan`, the three war-veto log lines and the Offices appointment line build English whatever
      the game's language; (7) the enum leaks the review's grep found beside them: `over ReclaimAncestralLand`
      on the Realm war row, the peace table, the Diplomacy menu, `Renounce our DefensivePact`, `best:
      <CasusBelliType>` - now `NameOf`; `Treaty.NameInSentence` for mid-sentence names; `StatecraftVM` compared
      a translated word to "you" - now compares the hero. **Not done:** `{GETSKILLVALUE}`-style variable names
      (ugly, harmless), `{STANDINGWORD}` lower-cased in code (English rule), `Missions` log reasons that are
      still English literals. The AC2 look below now also tests (1) and (2).
- [ ] **AC2: one look at the German fixture, and it is already written.** The plumbing is **proven
      live** - the lead's session on 2026-10-04 switched the game to Deutsch and the game log shows
      it opening `DiplomacyIntrigue/ModuleData/Languages/DE/di_strings.xml`, so the mod's folder is
      found and read. Every screen read English because that file holds no entries: the 12 folders
      ship empty on purpose, so all 653 keys then took the English fallback (852 now), which is R1 working and
      looks exactly like the mod ignoring the language. `scripts/localization-fixture.ps1 -Action Write`
      has put fourteen German keys into the deployed folder; switch the language in the launcher
      (**no restart needed** - the game reloads every module's strings at runtime), open the Realm
      and Court tabs, then `-Action Remove`. What to expect is in story §6a. `deploy.ps1` overwrites
      that folder, so re-run the fixture after a deploy. It is a fixture, not a translation.
- [ ] **`diplomacy.test_open_encyclopedia` crashes the game on v1.5.3** - a vanilla
      `NullReferenceException` in `GauntletLayer.IsFocusedOnInput` from
      `SandBox.EncyclopediaData.OnTick`, mod log clean, no frames of ours (story §9). The
      Encyclopedia court page is therefore the one converted surface never seen. Worth deciding
      whether it is our push or vanilla's before anyone runs it again.
- [ ] **Localization, what is left** (story 4.1, in order of risk):
      (1) **166 strings**, a human has to write: 78 sentences spread over several statements, 34 with
      a conditional inside a clause, 31 fragments of a longer sentence, 23 whose `{VARIABLE}` would
      carry English rather than a value. Plus 18 that cannot be keyed at all (MCM's setting names
      and hints), and two whole methods held in `exceptions.txt` with their reason:
      `Power.Describe` (a line assembled in code from a share plus a band clause) and
      `PeaceTable.DescribeAllowance` (price rows carry their column alignment inside the string, so
      the padding has to move into the prefab first). Listed as `manual` with a reason in
      artifacts/localization/inventory.csv; until they are done those particular sentences stay
      English in every language. The worst files are `UI/DiplomacyMenu.cs`,
      `UI/EncyclopediaPages/EncyclopediaCourtVM.cs` and `UI/KingdomScreen/RealmVM.cs`.
      **The figure moved 440 -> 318 -> 162 -> 166.** The first was inflated by the tool counting log
      lines as screens (§9a); the last rose because a lookup in the tool had never matched and was
      hiding real work (§9b). **Do not treat any older number here as current.**
      (2) Then AC6 - fit in the longest language and a CJK one - which needs the launcher session.
      Two layout overlaps seen in English are pre-existing and not localization: the Realm tab's
      left column (a long sphere explanation runs into the block below it) and the Court tab's
      right column (the "Make amends" button over the loyalty line). They belong with the
      Kingdom-UI loose ends above.
- [ ] **Vietnamese** - separate, [story 4.2](docs/stories/4.2-vietnamese.md). **Started 2026-10-04 on
      `feature/4.1-localization`**, because 4.1's ST-4 and ST-6 exist. ST-1 (on disk) and ST-2 (the
      patch's terms, read from 23,808 of its strings) are **done**; D3 settled by the lead's decision
      to follow the patch (town *thị trấn*, fief *lãnh thổ*, ruler *người cai trị*, realm tab *vương
      quốc*, court tab *triều đình*). **AC2 is true by construction**: `deploy.ps1` removes the `VI`
      folder when the community patch is absent, so no patch means no folder. **ST-3 is not started -
      852 keys** - and rule 11 of `check-localization.ps1` now fails the build on a part-filled folder,
      so it is all-or-nothing. Two things are not ours: ST-5's AC1 needs the launcher set to
      Vietnamese, and ST-6 is the lead's review. `Hold` is a trap: the patch translates it "Giữ",
      which is the verb "to hold", not our concept.

## Pending work

- [x] **Run 11** - done 2026-10-02, [docs/balance/run-11.md](docs/balance/run-11.md).
- [x] **Run 11 §8, answered 2026-10-02** (design 03 §9, decisions 19-21): (1) the tech lead chose -
      AI cap 12,000 and share 6%, a built network kept through a peace, a new one aimed where it can
      grow; (2) Harmony, `HeroSpawn_GetBestAvailableCommander_Patch`; (5) `test_player_army`. Built and
      compiled against v1.4.8 and v1.5.3; **not yet run live**.
- [x] Live check of decisions 19-21 and the legitimacy changes: **run 12**, [docs/balance/run-12.md](docs/balance/run-12.md) (AC2a and the oath price still unrun).
- [ ] **For the lead, from run 12 §4:** year-2 or year-3 network bar; StealTreasury at 76% of AI operations; **the civil-war loop** - the lead took all three remedies on 2026-10-03 (stalemate retires the claim, 252-day cooldown, 84-day bar on the claimant); built and seen live in a staged case, a long run still owed.
- [ ] (old) **Live check of decisions 19-21:** `ai_espionage` shows the growth per rival and the keep rule;
      `forced-party` stays at 0 over a Part B-length run (the startup line names the seventh patch);
      AC2a staged with `test_player_army`.
- [x] **(3)** The lead accepted the AI-to-AI exposure war for Phase 3's acceptance (2026-10-02).
- [ ] **(4) Built 2026-10-02, three changes** (design 02 §4): vassal oath -8, wartime +1 per 21 days under 30,
      a claimant from a weak crown. Live check owed: a Part B-length run should show claimants arising,
      legitimacy off the floor, and (the point) whether a civil war now starts - and not too often.
- [ ] No `player_died` telemetry when the player dies with no heir (run 11 D-4; vanilla's no-heir path
      never calls `MakeDead`).
- [ ] **Phase 4 B/C, built 2026-10-03, not run in game:** the incompatibility warning (load with
      `Bannerlord.Diplomacy` present, or read the code), the `[PERF]` line and `diplomacy.perf` (the next
      long run gives the first tick budget).
- [x] **0.3.0 uploaded to the Steam Workshop** 2026-10-03 at the lead's call, from `3693030` on
      `feature/phase4-bc` (item `3810668052`: content, description and preview replaced). Uploaded before
      the checks below, which are still owed:
- [ ] The civil-war remedies over a long run (run 12 overshot: 14 in 12 years).
- [ ] A 0.2.0 save loaded in the 0.3.0 release build. The changelog says it loads; the evidence is
      indirect (saves from before 1.10c's new save data, `di_phase1_full` among them, load in the current
      code), not a 0.2.0 save in the release DLL.
- [ ] The Workshop page's required items (Harmony, ButterLib, UIExtenderEx, MCM), set by hand on Steam.
- [ ] 0.3.0 on Nexus: the zip is `artifacts/release/DiplomacyIntrigue-v0.3.0.zip`; texts in the lead's
      untracked `docs/release/`. Uploaded by hand.

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
