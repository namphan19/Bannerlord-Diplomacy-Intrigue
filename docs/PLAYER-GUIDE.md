# Diplomacy & Intrigue — Player Guide

For Mount & Blade II: Bannerlord v1.4.8.

This mod replaces Calradia's diplomacy. Wars now start for stated reasons and end when
someone is too worn down to keep fighting, treaties bind the kingdoms that sign them, and a
strong realm can make weaker ones kneel instead of conquering them one castle at a time.

Nothing here is hidden from you and applied to the AI, or the other way round. Every number
this guide names is the number an AI court uses to make the same decision.

---

## Where the mod lives

**Kingdom screen → Diplomacy tab.** Everything the mod knows is here, next to the game's own
information. Select any kingdom in the left list.

**Ctrl+D on the campaign map.** The same actions in a text menu, plus a few lists the screen
does not show: all your wars at once, your claims, and every hegemony on the map.

You need to belong to a kingdom to see the Diplomacy tab at all. If you are a vassal rather
than the ruler, you can see everything and act on nothing — foreign policy belongs to whoever
wears the crown.

---

## 1. Wars end now

Two separate numbers decide two different things. Keeping them apart is the whole design.

| | What it measures | What it decides |
|---|---|---|
| **War exhaustion** | how worn down a kingdom is: days at war, casualties against your size, fiefs lost, villages raided, sieges endured | whether a court will **sign** anything |
| **War score** | who is winning: battles, captures, raids | what the winner may **demand** |

A winner who is exhausted takes a white peace. A loser who is still fresh refuses to be
carved up and fights on. Being ahead is not enough to dictate terms, and being tired is not
enough to be forgiven.

### Reading the enemy

Your own exhaustion is shown exactly. The enemy's is shown as a band, and the band edges are
the thresholds their behaviour actually changes at:

| Band | From | What it means |
|---|---|---|
| **Fresh** | 0 | nothing is pressing them |
| **Strained** | 20 | feeling the cost, not yet politically |
| **Weary** | 40 | their court is starting to press for peace |
| **Exhausted** | 60 | they will accept a white peace |
| **Breaking** | 80 | they will accept unfavourable terms |

"Exhausted" is not a mood. It means that if you offer a white peace now, they sign it.

The exact figure behind the band is deliberately not for sale — an espionage report will buy
it when that pillar ships.

---

## 2. Wars need a reason

You cannot declare war by pointing at a map any more. Every war carries a **casus belli**,
and its **legitimacy** (0.00–1.00) decides what the war costs you and what the world thinks
of you for starting it.

| Casus belli | Legitimacy | How you get it |
|---|---|---|
| Defend an ally | 1.00 | honouring a defensive pact or alliance |
| Broken treaty | 0.95 | they broke an agreement with you |
| Avenge a raid | 0.80 | they raided your villages |
| Reclaim ancestral land | 0.70 | you held the fief within living memory |
| Espionage exposed | 0.60 | (Phase 3) |
| Trade dispute | 0.40 | (Phase 3) |
| **Conquest** | 0.20 | no case at all — you simply want it |

**A war costs influence, and a war without a case costs double.** The base is 40 influence,
multiplied by `2 − legitimacy`: a war of reclamation costs about 52, naked conquest about 72,
and both get more expensive if your realm is already weary of fighting.

The **Declare war** button on the Diplomacy tab proposes the decision your court then votes
on, and charges exactly that price — the figure an AI ruler pays for the same war, shown on
the button. (The game's own proposal cost of 200, or 400 under War Tax, no longer applies.)
A pact between you greys it out and names itself. The Charm perk **Firebrand** takes a
quarter off every war and treaty you propose, as it does for AI rulers who hold it.

Legitimacy also decides whether your allies answer your call, and what a defensive pact
obliges them to do. A pact never drags anyone into a war of conquest.

### Claims

A claim is a standing entitlement to a specific fief. You get one by losing the fief to an
enemy, or by being raided, or by manufacturing one.

**Only a territorial claim lets you demand land at the peace table.** A grievance about a
broken treaty justifies the war; it does not entitle you to a castle.

**Fabricating a claim** costs 150 influence and 20,000 denars, takes 30 days, and carries a
**20% chance of being caught**. If you are caught, relations drop with *every* court, and the
kingdom you were plotting against gains a claim of its own. It is the most expensive way to
start a war and sometimes the only one available.

---

## 3. Treaties

Six kinds, each with a term. When one runs its full course, both parties gain trust for
having honoured it.

| Treaty | Term | Cost | What it obliges |
|---|---|---|---|
| **Truce** | 1 year | free | no war between you. Signed automatically with every peace |
| **Non-aggression pact** | 2 years | 60 influence | no war between you |
| **Defensive pact** | 3 years | 100 influence | you join if they are attacked — never a war they started |
| **Alliance** | 3 years | 180 influence | you join their wars, including ones they choose |
| **Tributary pact** | 2 years | negotiated | one side pays the other every 7 days for peace |
| **Vassalage** | 5 years | negotiated | tribute **and** military service, and no foreign policy of your own |

The other side has to want it. The Diplomacy tab shows you what their court values the pact
at and what it needs to see — those are their real numbers, not a hint.

**A treaty that forbids war actually prevents it.** If a pact stands between you, the
declare-war option greys out and tells you which agreement is in the way.

**Tribute can also be demanded without a war.** The **Demand tribute** button is coercion,
not negotiation: you need a territorial claim for the pretext, at least twice their strength
for the argument, and enough of their trust that paying looks better than fighting. Those are
the same conditions an AI kingdom checks before making the demand of you.

### Breaking one

Always possible. Never free. Renouncing a treaty costs you trust with the victim, trust with
**every other court**, and hands the victim a `BrokenTreaty` claim worth 0.95 legitimacy —
the second-best reason for war in the game, now pointed at you.

The mod never blocks a deliberate betrayal. It only prices it.

### Trust

One value per direction: what you think of them is a different number from what they think of
you, and a betrayal splits the two apart permanently.

**Trust fades slowly, and only if nobody tends it.** Relation covers feelings that pass in a
season; trust is reputation. Goodwill left alone drains to nothing in about two years, a
grudge about four times as slowly — a broken treaty can shut a court's door to you for
months — and every day at war drives the number down, faster the longer the war runs, though
war alone never takes it below −35, the cost of one betrayal: deeper than that is earned only
by betraying.
Any good turn between two courts holds the fading off for a month. Below **−20** a court
will sign nothing with you but a truce or the terms that end a war — those are never
refused, because stopping a war has to stay possible however badly everyone has behaved.

---

## 4. Making peace

Open the Diplomacy tab, select the kingdom, and press **Negotiate peace**. You will be told
exactly what the war has earned you:

- **War score below ~17** — nothing has been earned. White peace only.
- **Around 45** — a castle, or tribute and prisoners.
- **75 or more** — their submission as a vassal. If they already hold vassals of their own they
  cannot kneel, so you may instead make them release every one of them.

Each demand costs points from that budget, and the package has to fit. You can always ask for
less. You cannot ask for land without a territorial claim, however well the war has gone.

Above 75 the AI stops accepting anything smaller: a victory that can take a kingdom's standing
asks for it, rather than settling for tribute. Between about 65 and 75 tribute is still what a
win is worth.

Then the other side decides whether to sign, and that is exhaustion, not score. A realm at
Fresh will refuse a white peace even while losing.

---

## 5. Being called to war, and calling

An alliance or a defensive pact that cannot be declined is a suicide pact no one would sign,
so refusal is always available:

- **Refusing an alliance** costs trust and nothing else.
- **Refusing as a vassal** is defiance, and it is counted (see below).
- **As a patron, you are called when your vassal is attacked** — and when a kingdom submits
  to you, into the wars it was already defending. You are never called into a war your vassal
  started. Refusing costs trust with the vassal, and its Hold on you slips every day the war
  goes unanswered.
- A kingdom dragged into someone else's war is **released from it** when that someone makes
  peace. You will not be left fighting a war whose cause has ended.

Only half a patron's vassals are called into any one war, nearest the target first. The
alternative is every war becoming a world war, which is the vanilla behaviour this mod exists
to remove.

---

## 6. Hegemony: rising over other kingdoms

A **hegemon** is simply any kingdom holding at least one vassalage. There is no title to
claim and no ceremony. Several can exist at once, and the moment its last vassal leaves, a
hegemon stops being one.

### Making a kingdom kneel

Three routes:

1. **At the peace table**, at war score 75 — submission is the top rung of the ladder. You
   have to be the stronger of the two: a war won on points does not make you able to hold them.
   War score has no ceiling, so a crushing win can exceed 100.
2. **Voluntarily**, when a cornered kingdom decides a protector is better than the war it is
   losing. It only asks a kingdom stronger than itself, and it counts the danger **you could
   actually take off its hands** — not an enemy you are bound by treaty not to fight, and not
   more than you could match. It also weighs how close you are, how worn down it is, and
   whether it trusts you, against its own pride and the insult of serving a foreign culture.
   It kneels at 55.
3. **By poaching** a rival hegemon's neglected vassal — which means war with that rival. The
   price is yours, not the vassal's. The **Court them** button appears on a resentful vassal's
   row and says what the move would cost before anything is torn up.

And the door swings both ways: **Kneel to them** offers your own kingdom's oath to a stronger
power, in peace or as the peace itself when a war is going badly. The gates are the ones an
AI candidate faces — they must be stronger than you, able to sign, and willing to hold a
vassal — plus what the oath buys: tribute out, foreign policy surrendered, protection owed
to you.

A patron grown greedy has one more move: **Tear up their oath** repudiates a vassal at the
full price of a breach and opens a war of annexation, the same way a greedy AI patron turns
on its own. Every other vassal you hold takes the lesson in Hold.

### Hold: the strength of the bond

Every vassalage carries a **Hold** from 0 to 100, drifting toward a target made of fear,
protection, trust, the weight of the tribute, shared wars, a rival's offer, and culture. What
a vassal does depends on where Hold sits:

| Hold | The vassal |
|---|---|
| **70+** | renews the bond when its term ends |
| **40–69** | serves: answers summons, pays tribute |
| **30–39** | **passive resistance** — withholds tribute, refuses summons |
| **15–29** | **diplomatic defiance** — signs treaties with outsiders behind your back, and will listen to a rival patron |
| **below 15** | thirty days here and it **revolts**: a war of independence, and every other vassal of yours takes −10 Hold for having watched — any left below 25 **rise with it** |

The revolt line is 15 only for a vassal as strong as you. It rises to **30** for a vassal twice
your strength — a kingdom that could beat you does not wait long — and falls to **0** for one
half your size, which will not revolt alone however much it resents you. `diplomacy.strength`
shows where every kingdom stands. Fear works the same way in both directions: being twice your
vassal's strength holds it by +25, being half of it costs you −25.

Refusing a summons, treating with outsiders, or withholding tribute earns a **defiance mark**
(withholding at most once every four weeks). Two marks and the vassalage will not renew when
its term runs out, and the next refused summons breaks it.

### If you are the vassal

You pay tribute every 7 days and you owe troops. You cannot declare war on your own account,
and you cannot declare war on your patron as a routine decision — but you can defy them, and
everything above is available to you exactly as it is to an AI vassal.

Independence is the one foreign-policy act that stays yours. Once Hold sits below the
breaking point, **Declare independence** appears on your patron's row: renouncing the oath
and fighting the war of independence is the same revolt an AI vassal runs — and their other
resentful vassals may rise with you. If you let the collapse run instead, the mod asks you
before it acts. Below breaking point the exit is still there, as the ordinary Renounce —
which breaks the oath at breach price without a war. And when a patron will not defend you,
**Beg their mercy** lets you kneel to the kingdom attacking you; your patron,
not you, is named the oathbreaker.

---

## 7. Power: ambition, coalitions, greed

Every kingdom's strength is measured against the whole world, not only against its neighbours.
The **Power** bar on the Diplomacy tab shows each side's share of Calradia's strength, and its
hover carries the rest — ambition and greed are public knowledge. Ctrl+D → Other kingdoms shows
the same in words: *"holds 27% of Calradia's strength; growing greedy"*.

- **Strength breeds ambition.** A kingdom well above its even share wants war more, and one holding
  a quarter of Calradia's strength will run two wars at once.
- **The strong make the weak stand together.** Kingdoms outweighed by a sphere find each other's
  pacts worth more, and an alliance now **deters**: a court choosing whom to attack weighs the
  allies who would answer, not the target alone — and an ally no longer abandons you because your
  enemy is strong, only because *both sides together* are hopeless.
- **Too much strength turns to greed.** A ruler holding a quarter or more of the world's strength,
  sustained over about a year, stops accepting vassals and starts wanting provinces. It may tear up
  a vassal's oath and conquer it. Its vassals see it coming: their Hold falls, and they are willing
  to revolt at worse odds. **This applies to you**: grow too strong and your vassals will fear you.
- **Conquest ends kingdoms.** A kingdom that loses its last town or castle is gone; its wars end
  with it, and anyone fighting only because it called them in is released.

## 8. Your skills in politics

Six skills count in the mod's decisions. Each is measured against the **median of the same
office across every realm**, so a ruler exactly as good as the other rulers changes nothing,
and one far below or above them feels it. The **Statecraft** strip on the Realm tab lists who
holds each office for your realm, their skill against that median, and what it moves.

| Office | Skill | Who holds it | What it moves |
|---|---|---|---|
| Ruler | Leadership | the ruler, in person | how fast your realm tires in a war (±15%), every vassal's Hold (±10), every house's loyalty (±5) |
| Envoy | Charm | the best Charm in the ruling house | what a victory buys at the peace table (±15%, envoy against envoy), how a court you ask values a pact (±10) |
| Steward | Steward | the best Steward in the ruling house | how fast grievances against the crown fade (×0.5 to ×1.5), the legitimacy a year of peace restores |
| Treasurer | Trade | the best Trade in the ruling house | what a house costs to turn in a civil war (±15%, treasurer against treasurer) |
| Spymaster | Roguery | the best Roguery in the ruling house | the chance a claim you fabricate is caught (10–30%, against their Watch) |
| Watch | Scouting | the best Scouting in the ruling house | catching someone else's fabrication |

**"The ruling house" includes your companions and family.** A companion with Charm 250 is your
envoy the day they join; a captured one hands the office to the next best. Leadership is the
exception: a king cannot delegate being followed.

**Personal Charm matters twice more.** A claimant's own Charm counts in every house's choice at
a succession, and a house head's Charm decides who speaks for a court bloc — a charming vassal
can lead a bloc it does not bankroll.

**Doing the work trains the skill**, through the game's own learning rate: signing pacts and
peace trains the envoy's Charm, a vassal kneeling or a war carried to its end trains the
ruler's Leadership, a year of peace the steward's Steward, tribute and buying houses the
treasurers' Trade, fabricating the spymaster's Roguery. AI heroes train the same way.

**Silver Tongue** (Trade 250) takes 15% off the price of buying a house in a civil war, when
your treasurer holds it.

A new ruler usually starts far below the kings around them. Put your best companions where
they count; the numbers on the Realm tab say which office needs them most.

## 9. Common questions

**Why can't I declare war on anyone?**
Check the Diplomacy tab: a treaty may forbid it, or you may already be a vassal, or your
ruling clan may not have the influence. The button tells you which.

**Why did my ally refuse my call to arms?**
A defensive pact does not answer a war you started. An ally with its own exhaustion above 50
is excused. Both are visible before you call.

**Why won't anyone sign anything with me?**
Look at the trust bar in the Diplomacy tab, in the direction pointing at you. Below −20 you
have burned your name and only a truce is available until you have honoured some agreements.

**A kingdom declared war on me with no warning.**
Look at what it filed the war under. If it is `Conquest` at legitimacy 0.20, it had no case
and paid double for the privilege — and you now have a grievance the whole map can see.

**My vassal stopped paying.**
Its Hold has fallen below 40. Protect it, ease its tribute, or frighten it — or watch the
number keep falling until it treats with your rivals at 30 and revolts at 15. Each month it
keeps the money is a defiance mark, so a vassal that goes on withholding will not renew.

**Several of my vassals revolted at once.**
One of them reached breaking point, and the others were already close. A revolt is news
every vassal hears; the ones below 25 after hearing it join rather than wait their turn.

---

## Settings

Mod Configuration Menu → Diplomacy & Intrigue.

- **AI diplomacy aggressiveness** — a multiplier on how readily AI kingdoms choose war over
  pacts. 1.00 is the tuned default; lower for a quieter Calradia, higher for a bloodier one.
- **Statecraft** — whether skills count in politics (§8). Off, every skill term is neutral and
  political acts train nothing. It can be switched mid-campaign; nothing is saved by it.
- **Announce AI decisions** — a map notification each time a court acts.
- **Verbose logging** — every decision written to the log. Slower, and what a bug report
  needs.

Logs live in `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\`.

## Compatibility

**Not compatible with the BUTR Diplomacy mod.** Both replace the same systems and the two
will fight. Use one or the other.

This mod overrides four of the game's diplomacy models (kingdom decisions, peace, alliances,
trade agreements) and patches two methods. Any other mod that changes how kingdoms declare
war or make peace will conflict.
