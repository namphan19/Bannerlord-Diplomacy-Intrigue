# Phase 1 UI proposal — the approved mockup

The lead approved this mockup on 2026-09-20. It is the target for the Phase 1 player UI. The
original is a claude.ai design canvas ("Diplomacy & Intrigue — UI Proposal"). The four boards
are copied here as `.dc.html` source so they can be read without a claude.ai login. They were
copied on 2026-09-23 from canvas version `1790171593-32ae`.

The files do not render outside that canvas, because `support.js` is not here. **Read them
as source.** The markup shows layout, hierarchy and copy. The `<script>` block at the bottom
of each board shows behaviour and the sample data.

| File | Board | Surface in the game |
|---|---|---|
| [1-diplomacy-tab.dc.html](1-diplomacy-tab.dc.html) | 1. One relationship | Kingdom screen, **Diplomacy** tab (vanilla tab, extended) |
| [2-realm-tab.dc.html](2-realm-tab.dc.html) | 2. The whole realm | Kingdom screen, **Realm** tab (6th tab, ours) |
| [3a-peace-table.dc.html](3a-peace-table.dc.html) | 3a. Negotiation, we propose | **Peace table**, our own Gauntlet screen |
| [3b-peace-table-incoming.dc.html](3b-peace-table-incoming.dc.html) | 3b. Negotiation, they propose | the same screen, read-only, Accept / Refuse |

## The rule behind it: a surface per scope

The mockup puts each action on a surface according to its scope. Use that rule to decide
where a new piece of UI goes. Do not revisit the placement.

- **One relationship** goes on the Diplomacy tab.
- **The whole realm** (sphere, Hold, claims, agreements, power, our wars) goes on the Realm tab.
- **One negotiation** goes on the peace table, which has its own screen. It is the only
  Gauntlet screen the project owns. The exception was taken for two reasons. The running
  total against the war-score budget cannot be built inside an inquiry. And GABS cannot click
  inside an inquiry, so an inquiry flow can never be verified.
- **Ctrl+D** is no longer a second UI. It becomes a shortcut into the Realm tab.

Phase 2's Court tab (7th) and the rival-court section on the Encyclopedia are a separate
mockup ("Court Intrigue Screen") and are already built. They are not part of this proposal.

## How to read a mockup here

- **Structure, content and behaviour are the spec. The web styling is not.** Cinzel and
  EB Garamond do not exist in the game. Use the game's native fonts and the brushes in
  `module/DiplomacyIntrigue/GUI/Brushes/DiRealm.xml`. Match the mockup's hierarchy, grouping,
  order, copy and colour *meaning*: green is good for us, red is bad for us, gold is a
  threshold.
- **Every number in the mockup is sample data.** In the game, each figure comes from the
  resolver the AI uses. Prices come from `PeaceTable.CostOf` and `BudgetFor`, and what can be
  demanded from `PeaceTable.IsDemandable`. Signing comes from `WouldAccept` /
  `BothWouldSign`, and the pact verdicts from the court's `PactValue`. Never recompute a
  figure for display (CLAUDE.md §3, "one resolver per concept"). If the UI needs a number
  that no resolver exposes, stop and ask.
- The peace table's term list maps one to one onto `Models/PeaceTerms.cs`: captives
  (`ReleasePrisoners`), indemnity (`IndemnityGold`), tribute (`ImposeTributaryPact`),
  land (`FiefsCeded`), releasing their vassals (`DissolveHegemony`) and submission
  (`ImposeVassalage`). The mutual exclusions in the mockup (submission excludes tribute and
  dissolution) must come from the model's rules. The screen must not hard-code them.
- The mockup was drawn at 1280x720. The game must also work at 1920x1080.

## What already exists (from docs/STATUS.md, "Kingdom screen UI", 2026-09-21)

| Board | State |
|---|---|
| 1 Diplomacy tab | **Built and verified.** War rows show the exhaustion band and score. Truce rows show the relationship summary. The headline sits under the banners. The "What their court would sign" chooser has per-rung Propose buttons, and our action strip replaces vanilla's |
| 2 Realm tab | **Built and verified.** It has the standing strip, wars with a Peace-table button, vassals with Hold, spheres, claims (with fabrication progress), agreements, and Write a report. Differences from the mockup have not been audited |
| 3a Peace table | **Not built as designed.** It is still a nested `MultiSelectionInquiry` in `UI/DiplomacyMenu.cs` (around line 1072), with no running total and no verdict. Only the white-peace short path has ever been seen working |
| 3b Incoming offer | **Not built as designed.** The AI's offer to the player is a plain `InquiryData` in `Diplomacy/AiDiplomacy.cs` (around line 316). It has **never been seen working** in a live game |

The biggest gap is 3a/3b.
