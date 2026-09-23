# UI integration: Kingdom screen and Encyclopedia

How to put this mod's information and actions **inside the game's own screens**, learned
from reading the BUTR Diplomacy mod (which has shipped this for years) and checked against
the real v1.4.8 assemblies. Read this before touching `src/DiplomacyIntrigue/UI/`.

Everything marked **verified** was read from `TaleWorlds.CampaignSystem.ViewModelCollection.dll`
(v1.4.8) or `Bannerlord.UIExtenderEx.dll` (v2.13.2) with Mono.Cecil, or from the vanilla prefab
XML in `Modules/SandBox/GUI/Prefabs`, on 2026-09-16. Everything else says so.

Reference source: <https://github.com/DiplomacyTeam/Bannerlord.Diplomacy>, folder
`src/Bannerlord.Diplomacy/`. A local clone may sit next to this repo at
`../Bannerlord.Diplomacy`. **It is CC BY-NC-SA: learn the technique, do not paste its code
or XML.** Vanilla TaleWorlds markup is what we copy from.

Constraints that still apply (CLAUDE.md, docs/STATUS.md): game v1.4.8, `net472`,
UIExtenderEx v2.13.2, native look, **no Gauntlet screen of our own - with one named
exception, the peace table** (lead's decision, narrowed on 2026-09-20: a budget that updates
as terms are picked cannot be done in an inquiry, and GABS cannot click inside an inquiry, so
inquiry flows cannot be verified at all), no throw crossing into the engine, English text.
The approved mockup is in [ui-proposal/](ui-proposal/README.md).

---

## 0. The three things that were going wrong

Read these first. Each one cost real time already.

1. **The comparison rows vanished because the mixin hooks the wrong method.**
   `DiplomacyItemMixin.cs` hooks `RefreshValues`. The rows in the comparison panel are built by
   `KingdomDiplomacyItemVM.UpdateDiplomacyProperties`, which **starts with `Stats.Clear()`**, and
   that runs every time an item is selected (`OnSelect`). `KingdomTruceItemVM` does not even
   override `RefreshValues`. So anything added after `RefreshValues` is erased on the next
   click. Diplomacy hooks `UpdateDiplomacyProperties` and its rows survive. See §3.
2. **The `Prefabs2` API *can* be used from outside UIExtenderEx.** The comment in
   `WarTupleExtension.cs` says `PrefabExtensionTextAttribute` is internal. It is
   **`protected internal`**, a nested type of `Prefabs2.PrefabExtensionInsertPatch`, so any
   subclass can use it. Diplomacy ships on v2.13.2 using `[PrefabExtensionXmlDocument]` exactly
   this way. The obsolete `Prefabs` namespace is not required. See §4.
3. **XML written as C# string concatenation** (`DiplomacyPanelExtension.cs`) is hard to read,
   hard to diff against vanilla, and easy to break. Move it into real prefab files under
   `module/DiplomacyIntrigue/GUI/Prefabs/` and inject them by name. See §4.3.

Both code comments in (1) and (2) state a wrong rationale. CLAUDE.md §5 says to fix those,
so correct them in the same change that fixes the code.

---

## 0b. Six more, learned building the Court tab and its Encyclopedia section (2026-09-23)

Each of these rendered wrong or would have failed at load, and none of them produced an error.

1. **A `CoverChildren` widget holding a `StretchToParent` child stretches to fill its parent.**
   A card built as a `CoverChildren` `Widget` with a full-size background child (the usual
   `Sprite="BlankWhiteSquare_9" Color=...` backdrop) did not wrap its content - the first bloc card
   filled the entire column and pushed everything below it off the panel. Give cards a **Fixed**
   height, as the Realm panel already does for its war cards.
2. **`HorizontalAlignment="Right"` does nothing inside a horizontal `ListPanel`.** A ListPanel
   stacks its children; alignment is ignored. "Hawks" and "78%" rendered as "Hawks78%". For a
   left/right pair, use a plain `Widget` (children overlap) and align each child.
3. **An XML comment may not contain `--`.** `<!-- ---- Column 1 ---- -->` is malformed XML, and
   .NET's `XmlDocument` rejects it. Run new prefabs through any XML parser before deploying:
   `python -c "import xml.dom.minidom,sys; xml.dom.minidom.parse(sys.argv[1])" file.xml`.
4. **Two `Append` patches on the same anchor have no guaranteed order.** Our two tab buttons must
   go Realm then Court (the last tab wears the end-cap art). Insert both from **one** patch: a
   document with a throwaway root, `LoadXml("<DiTabs><DiRealmTabButton /><DiCourtTabButton /></DiTabs>")`,
   returned under `[PrefabExtensionXmlDocument(true)]` - the `true` is `removeRootNode`, which
   inserts the children in document order.
5. **The GABS bridge clicks the first widget whose text matches - hidden panels included.** A row
   whose text also appears in a vanilla list earlier in the tree cannot be clicked by text, and
   `__index:N` does not follow `ui.get_screen`'s ordering (it clicked a policy in the hidden
   Policies tab). `ui.call_viewmodel_method_at_index` reflects the vanilla VM's C# type, so it
   cannot see a mixin's properties either. Give a panel a test hook that calls the same method the
   click would (`diplomacy.test_court_select`), and say plainly that the click itself was not
   exercised.

6. **The bridge cannot scroll.** A section below the fold of a `ScrollablePanel` (the
   Encyclopedia's right column) cannot be reached by any bridge tool. Real input can: bring
   the game window forward with `SetForegroundWindow`, put the cursor over the panel with
   `SetCursorPos`, and send `mouse_event(MOUSEEVENTF_WHEEL, ..., +/-120)` per notch. Six notches
   crossed most of a kingdom page; aim, screenshot, adjust.

And one that went right and is worth copying: **run the whole checklist in §7 on a second world**.
The vassal's view, mercenary exclusion and a RELIABLE band only appeared on the second save.

## 1. Mental model of Gauntlet (just enough)

| Piece | What it is |
|---|---|
| **Movie / prefab** | An XML file (`<Prefab><Window>...</Window></Prefab>`). A tag that is not a built-in widget, e.g. `<WarTuple />`, **instantiates the prefab of that name**. This is how vanilla composes screens, and how we inject our own files. |
| **ViewModel (VM)** | A C# class deriving `TaleWorlds.Library.ViewModel`. Public properties marked `[DataSourceProperty]` are bindable. |
| `Text="@Name"` | Bind an attribute to property `Name` of the **current data context**. |
| `DataSource="{Child}"` | Change the data context for this widget and its children to property `Child`. `{..}` goes up one level. `{A\B}` is a path. |
| `Command.Click="ExecuteX"` | Call method `ExecuteX` on the current data context. On a mixin, mark it `[DataSourceMethod]`. |
| `<ListPanel DataSource="{Items}"><ItemTemplate>` | `Items` must be an `MBBindingList<SomeVM>`. The template is repeated per item, each with the item as context. |
| `!ConstantName` | A `<Constant>` declared in the prefab's `<Constants>`. |
| `*ParamName` | A `<Parameter>` of a reusable prefab (see Diplomacy's `Standard/BasicDiplomacyButton.xml`). |
| **Brush** | Named style (fonts, colours, sprite layers, hover states). Vanilla brushes are free to reuse. Our own go in `GUI/Brushes/*.xml`. |

**The data-context trap** (DI already hit it once): an attribute resolves against whatever
`DataSource` is in effect *at that widget*. `IsAcceptableItemSelected` lives on
`KingdomDiplomacyVM`; inside `DataSource="{CurrentSelectedDiplomacyItem}"` it does not exist
and the binding silently evaluates to false. Before binding anything, write down which VM is
the context at that point in the tree.

**Property change notification.** A value shown on screen only updates if the setter raises a
change. In a mixin use `SetField(ref _field, value, nameof(Prop))` (on `BaseViewModelMixin`)
or `ViewModel.OnPropertyChangedWithValue(value, nameof(Prop))`. **Assigning the backing field
directly shows nothing** unless the binding happens later. Diplomacy's
`EncyclopediaFactionPageVMMixin.OnRefresh` does `_allies = new()`; do not copy that.

Modules' `GUI/` folders are picked up by the game without any `SubModule.xml` entry:
vanilla modules and Diplomacy both rely on it. `deploy.ps1` copies `module/DiplomacyIntrigue/**`,
so a new `GUI/` folder ships automatically.

---

## 2. The two UIExtenderEx tools

Registration already exists in `UI/KingdomScreen/ModUI.cs`
(`UIExtender.Create(id)` → `Register(assembly)` → `Enable()`, called from `OnSubModuleLoad`).
Every mixin and prefab patch in the assembly is found by attribute. Nothing else to register.

### 2.1 `ViewModelMixin`: add properties and methods to a vanilla VM

```csharp
[ViewModelMixin("UpdateDiplomacyProperties")]      // the vanilla method after which OnRefresh runs
internal sealed class TruceItemMixin : BaseViewModelMixin<KingdomTruceItemVM>
{
    public TruceItemMixin(KingdomTruceItemVM vm) : base(vm) { ... }
    public override void OnRefresh() { ... }        // runs after the hooked method
    public override void OnFinalize() { ... }        // unsubscribe events here
    [DataSourceProperty] public string DiSomething { get => _x; set => SetField(ref _x, value, nameof(DiSomething)); }
    [DataSourceMethod]   public void DiExecuteSomething() { ... }
}
```

Constructor overloads, **verified** on v2.13.2: `()`, `(string refreshMethodName)`,
`(bool handleDerived)`, `(string refreshMethodName, bool handleDerived)`.
`handleDerived: true` also attaches to subclasses of the VM.

Bindings from a mixin appear **on the vanilla VM**, so prefab XML binds `@DiSomething` as if
TaleWorlds had written it. Keep the `Di` prefix: another mod mixing into the same VM must not
collide with our names.

### 2.2 `PrefabExtension`: change vanilla XML

```csharp
[PrefabExtension("DiplomacyPanel", "descendant::Widget[@Id='Something']")]
internal sealed class MyPatch : PrefabExtensionInsertPatch   // Bannerlord.UIExtenderEx.Prefabs2
{
    public override InsertType Type => InsertType.Child;
    ...
}
```

First argument: the vanilla prefab name (file name without `.xml`). Second: an XPath into
it, evaluated against that file. A miss is logged by UIExtenderEx and the screen renders as
vanilla, so **small, independent patches fail gracefully; one big replacement does not.**

---

## 3. Picking the refresh hook: the rule that matters most

**Hook the method that rebuilds the data you touch, never a method that runs before it.**
Find it in the IL, not by guessing from the name.

### 3.1 Verified call graph, v1.4.8 (Kingdom → Diplomacy tab)

| Method | Visibility | What it does |
|---|---|---|
| `KingdomDiplomacyItemVM.UpdateDiplomacyProperties` | protected virtual | **`Stats.Clear()`**, then adds the base rows, sets faction names, leaders, clans, other wars/alliances/trade agreements |
| `KingdomWarItemVM.UpdateDiplomacyProperties` | protected override | calls base, then adds war rows (`Score`, casualties, days since war began) |
| `KingdomTruceItemVM.UpdateDiplomacyProperties` | protected override | calls base, then adds truce rows, `HasAlliance`, `HasTradeAgreement`, end times |
| `KingdomWarItemVM.RefreshValues` | public override | calls `UpdateDiplomacyProperties` |
| `KingdomTruceItemVM.RefreshValues` | *not overridden* | the base `ViewModel.RefreshValues`, which does nothing for these rows |
| `KingdomWarItemVM.OnSelect` / `KingdomTruceItemVM.OnSelect` | protected override | call `UpdateDiplomacyProperties` **on every selection** |
| `KingdomTruceItemVM..ctor` | public | calls `UpdateDiplomacyProperties` (before any mixin exists) |
| `KingdomDiplomacyVM.OnDiplomacyItemSelection` | private | sets `CurrentSelectedDiplomacyItem` and `IsAcceptableItemSelected`, **then** calls `OnSetCurrentDiplomacyItem` |
| `KingdomDiplomacyVM.OnSetCurrentDiplomacyItem` | private | rebuilds the panel-level **`Actions`** list (via `OnSetWarItem` / `OnSetPeaceItem`) |
| `KingdomDiplomacyVM.RefreshDiplomacyList` | public | rebuilds `PlayerWars` / `PlayerTruces` |

Consequences:

- **Adding a row to the vanilla comparison bars** (`CurrentSelectedDiplomacyItem\Stats`, bound at
  `DiplomacyPanel.xml` line 396): hook `UpdateDiplomacyProperties` on **both** `KingdomWarItemVM`
  and `KingdomTruceItemVM`. That is what Diplomacy does. Our row then goes into TaleWorlds' own
  list and is drawn by TaleWorlds' own template; no copied bar markup is needed.
- **Adding to the panel's `Actions` list** (the proposal buttons, line 466): it is rebuilt inside
  the *private* `OnSetCurrentDiplomacyItem`. A `PropertyChanged` listener on
  `CurrentSelectedDiplomacyItem` fires **before** that rebuild, so it has the same vanishing
  problem. Whether UIExtenderEx accepts a private method name as the refresh hook is **not
  verified**. Until it is, keep our buttons in our own `DiActions` list (what DI does now,
  and what Diplomacy does with its own properties); that part of the current design is right.

### 3.2 Recipe: our row in the game's own comparison bars

The vanilla row type is `KingdomWarComparableStatVM`. Diplomacy constructs it as
`(int value1, int value2, TextObject name, string color1, string color2, int max)`: confirm the
v1.4.8 signature with `tools/ApiDump` before writing it.

```csharp
[ViewModelMixin("UpdateDiplomacyProperties")]
internal sealed class TruceItemStatsMixin : BaseViewModelMixin<KingdomTruceItemVM>
{
    public TruceItemStatsMixin(KingdomTruceItemVM vm) : base(vm)
    {
        // The VM's constructor already ran UpdateDiplomacyProperties before this mixin existed,
        // so the first population has to be done by hand.
        OnRefresh();
    }

    public override void OnRefresh()
    {
        try
        {
            var vm = ViewModel;
            if (vm == null || !SubModule.Healthy) return;
            // Stats was just cleared and refilled by vanilla, so appending cannot duplicate.
            // Insert at a fixed index if the order matters; Diplomacy uses index 1.
            vm.Stats.Add(BuildTrustRow(vm));
        }
        catch (Exception ex)
        {
            Log.Error("UI", "Comparison row failed; the panel stays vanilla.", ex);
        }
    }
}
```

Then `DiplomacyStatVM` and the copied `FillBarHorizontalWidget` markup in
`DiplomacyPanelExtension.cs` become unnecessary for anything that fits the vanilla row shape.
The band-not-figure rule for enemy exhaustion still holds: pass the band floor as `value2`.

The vanilla row has no per-side hint. Diplomacy's approach for an explanation is a separate
hint widget (a `BasicTooltipViewModel` built from an `ExplainedNumber`, see its
`KingdomTruceItemVMMixin.UpdateDiplomacyTooltip`). That fits "the number shown is the number
the AI used" well: the tooltip can list every term.

### 3.3 Constructor order, generally

UIExtenderEx creates the mixin **after** the vanilla constructor. If that constructor already
called the hooked method, `OnRefresh` ran with no mixin, or with the mixin's fields still
null. Two patterns both work:

- call `OnRefresh()` at the end of the mixin constructor (Diplomacy's item mixins), or
- call `vm.RefreshValues()` at the end of the mixin constructor, when the hook is
  `RefreshValues` (Diplomacy's Encyclopedia mixins).

Always null-guard fields in `OnRefresh`: Diplomacy's hero-page mixin carries the comment
"this is called before the constructor the first time" for exactly this reason.

---

## 4. Prefab patches with `Prefabs2`

### 4.1 Verified API surface, UIExtenderEx v2.13.2

| Type | Status |
|---|---|
| `Prefabs2.PrefabExtensionInsertPatch` | public, **not** obsolete. Overridable `Type` and `Index`. |
| `Prefabs2.InsertType` | `Prepend`, `ReplaceKeepChildren`, `Replace`, `Child`, `Append`, `Remove` |
| Content attributes nested in `PrefabExtensionInsertPatch`: `PrefabExtensionXmlDocument`, `PrefabExtensionXmlNode`, `PrefabExtensionXmlNodes`, `PrefabExtensionText`, `PrefabExtensionFileName` | **`protected internal`**: usable from any subclass. Each has a `(bool removeRootNode)` constructor. |
| `Prefabs2.PrefabExtensionSetAttributePatch` | public, not obsolete |
| `Prefabs.PrefabExtensionInsertPatch` (what DI uses now) | **obsolete** |

`InsertType` semantics, relative to the XPath target: `Prepend` / `Append` = sibling before /
after; `Child` = inside, at `Index`; `Replace` = swap the node; `ReplaceKeepChildren` = swap the
node but keep its children; `Remove` = delete it.

### 4.2 The shape Diplomacy uses (and that works on v2.13.2)

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

[PrefabExtension("EncyclopediaFactionPage", "descendant::NavigatableGridWidget[@Id='EnemiesGrid']")]
internal sealed class FactionPageTreatiesExtension : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    private readonly XmlDocument _document = new XmlDocument();

    public FactionPageTreatiesExtension()
    {
        // One tag, named after our own prefab file in module/DiplomacyIntrigue/GUI/Prefabs.
        _document.LoadXml("<DiFactionPageTreaties />");
    }

    [PrefabExtensionXmlDocument]
    public XmlDocument GetPrefabExtension() => _document;
}
```

### 4.3 Put the markup in files, not strings

```
module/DiplomacyIntrigue/GUI/
  Prefabs/
    KingdomManagement/DiDiplomacyHeadline.xml
    KingdomManagement/DiDiplomacyActions.xml
    Encyclopedia/DiFactionPageTreaties.xml
  Brushes/
    DiplomacyIntrigue.xml          (only if a vanilla brush truly will not do)
```

Each file is a normal `<Prefab><Constants/><Window>...</Window></Prefab>`. The C# patch
injects `<DiDiplomacyHeadline />`. Benefits: the XML is diffable against the vanilla file
it was copied from, editors validate it, and a layout change does not need a rebuild of
string literals.

**Name every prefab file with the `Di` prefix.** Prefab names are global across all loaded
modules, and a clash with another mod's file is silent.

**Unverified; check once:** whether a prefab file change needs a game restart or is picked
up on reopening the screen. Assume a restart until someone has seen otherwise.

### 4.4 Changing one attribute

```csharp
[PrefabExtension("KingdomManagement", "descendant::Constant[@Name='Header.Tab.Center.Width.Scaled']")]
internal sealed class DiHeaderTabWidthPatch : Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes => new List<Attribute>
    {
        new Attribute("MultiplyResult", "0.60"),
    };
}
```

This is how Diplomacy narrows the Kingdom screen's header tabs to make room for one more.
`DiplomacyBarsMarginPatch` (MarginBottom 470) is the same kind of patch; after §3.2 moves our
rows into the vanilla list it may no longer be needed at all.

### 4.5 XPath anchors, verified present in v1.4.8

Prefer anchors on `Id` or a **binding** (`@Text='@Something'`, `@DataSource='{X}'`), which
TaleWorlds rarely renames, over position or layout attributes.

| Prefab (in `Modules/SandBox/GUI/Prefabs/`) | Anchor | Line | Used for |
|---|---|---|---|
| `KingdomManagement/KingdomManagement.xml` | `ButtonWidget[@Id='FiefsTabButton']` (also `ClanTabButton`, `PoliciesTabButton`, `ArmiesTabButton`, `DiplomacyTabButton`) | 123-151 | header tabs |
| same | `Constant[@Name='Header.Tab.Center.Width.Scaled']` | 15 | tab width |
| same | `DiplomacyPanel[@Id='DiplomacyPanel']` (also `ClansPanel`, `FiefsPanel`, ...) | 169-173 | whole tab panels |
| `KingdomManagement/Diplomacy/DiplomacyPanel.xml` | `Widget[@IsVisible='@Show']` | 29 | panel root (Diplomacy **replaces** this, see §6) |
| same | `NavigatableListPanel[@Id='PlayerWarsList']` / `[@Id='OtherWarsList']` | 71 / 87 | left list |
| same | `ListPanel[@IsVisible='@IsAcceptableItemSelected']` | 144 | right pane stack |
| same | `DataSource='{CurrentSelectedDiplomacyItem\Stats}'` | 396 | comparison bars |
| same | `Widget[@IsHidden='@IsDisplayingWarLogs']` | 386 | bars container |
| same | `TextWidget[@Text='@NoItemSelectedText']` | 461 | right pane bottom |
| same | `DataSource='{Actions}'` | 466 | proposal buttons |
| `KingdomManagement/Diplomacy/WarTuple.xml`, `TruceTuple.xml` | (see file) | | list rows |
| `Encyclopedia/EncyclopediaSubPages/EncyclopediaFactionPage.xml` | `NavigatableGridWidget[@Id='EnemiesGrid']` (also `ClansGrid`, `SettlementsGrid`) | 83-120 | faction page sections |
| same | `@Text='@InformationText'` | 68 | faction description |
| same | `MaskedTextureWidget[@Brush='Encyclopedia.Faction.Banner']` | 33 | under the big banner |
| `Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` | `RichTextWidget[@Text='@InformationText']` | 125 | under the hero description |
| same | `DataSource='{Stats}'` / `@Id='EnemiesGrid'` | 134 / 203 | hero stats, enemies |

**Before writing a patch, open the vanilla file and read the target in context.** Line numbers
above are for orientation only.

---

## 5. Recipes

### 5.1 Kingdom screen, Diplomacy tab

| Want | How |
|---|---|
| A number comparing the two kingdoms | §3.2: add a `KingdomWarComparableStatVM` to `Stats` in an `UpdateDiplomacyProperties` mixin, on both item VMs |
| A line of text under the banners | Mixin property on both item VMs + a `Child` patch into the right pane stack, bound through `DataSource="{CurrentSelectedDiplomacyItem}"`. What DI has now is the right shape; move its XML to a file |
| Our own action buttons | Own `MBBindingList<DiActionVM>` on the item mixin, drawn by a prefab copied from the vanilla proposal template (`DiplomacyPanel.xml` ~466). Diplomacy's `DiplomacyPanelButtons.xml` shows the button, influence and gold cost icons, and hint in one block |
| Enable/disable reason on hover | `HintWidget DataSource="{DiHint}"` over the button with `IsEnabled="false"`; the VM exposes a `HintViewModel` or `BasicTooltipViewModel` |
| Something in each list row | Patch `WarTuple` / `TruceTuple`; property on the item mixin. Fixed width, as DI already learned |
| Hide rows (e.g. a kingdom that should not appear) | Mixin on `KingdomDiplomacyVM` hooked on `RefreshValues`; remove from `PlayerWars` / `PlayerTruces` and refresh `NumOfPlayerWarsText`. See Diplomacy's `KingdomDiplomacyVMMixin.RemoveRebelKingdoms` |
| Refresh after a war or peace | In the `KingdomDiplomacyVM` mixin constructor, `CampaignEvents.WarDeclared` / `MakePeace` `.AddNonSerializedListener(this, ...)` → `ViewModel.RefreshValues()`; in `OnFinalize`, `CampaignEventDispatcher.Instance.RemoveListeners(this)`. **Forgetting the removal leaks the closed screen's VM** |

### 5.2 Kingdom screen, a new header tab or a Clans tab button

- **Header tab** (Diplomacy's "Factions"): an `Append` patch after `FiefsTabButton` copying its
  `ButtonWidget` markup with `Command.Click` bound to a `[DataSourceMethod]` on a
  `KingdomManagementVM` mixin, plus the width patch in §4.4 so six tabs fit. Diplomacy's button
  opens a separate Gauntlet layer, which this project has decided against. For us the click
  would open a native inquiry, or switch what an existing panel shows.
- **Clans tab button acting on the selected clan**: Diplomacy's `KingdomClanVMMixin` subscribes
  to `ViewModel.PropertyChangedWithValue` and reacts when `CurrentSelectedClan` changes, and
  unsubscribes in `OnFinalize`. That is safe *there* because nothing rebuilds its data after the
  property is set; check the IL before reusing the trick elsewhere (§3.1 shows where it fails).

### 5.3 Encyclopedia, kingdom page

Target for a new section: `Append` after `EnemiesGrid`. The injected prefab repeats vanilla's
own section pattern:

```xml
<EncyclopediaDivider MarginTop="35" MarginBottom="10" Parameter.Title="@DiTreatiesText" Parameter.ItemList="..\DiTreatiesGrid" />
<GridWidget Id="DiTreatiesGrid" DataSource="{DiTreatyPartners}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren"
            DefaultCellWidth="100" DefaultCellHeight="100" HorizontalAlignment="Left" MarginLeft="25" ColumnCount="7">
  <ItemTemplate>
    <EncyclopediaClanSubPageElement />
  </ItemTemplate>
</GridWidget>
```

Mixin on `EncyclopediaFactionPageVM`, hooked on `RefreshValues`; `vm.Obj` is the `Kingdom`;
the list items are vanilla `EncyclopediaFactionVM(kingdom)`, which gives the banner, name,
link and hover for free. Skip kingdoms the page manager says are not valid encyclopedia items:
`Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Kingdom)).IsValidEncyclopediaItem(k)`.

Good fits for this project: treaty partners by type, vassals and patron (hegemony sphere),
standing claims, and the trust ledger shown as a band.

**Built and verified, 2026-09-23 (Phase 2.7 rival court):** anchored with `Append` on
`descendant::EncyclopediaSubPageElement[@Id='Leader']` so the section sits beside the ruler
whatever other mods add at the bottom. One wrapper `ListPanel` carries `DataSource`, so the
vanilla `EncyclopediaDivider` inside it binds `Parameter.Title` to our VM and collapses our
body through `Parameter.ItemList="..\DiCourtBody"` exactly like a vanilla section (clicked
live). The page's constructor calls `RefreshValues` before any mixin exists, so the mixin
composes once in its own constructor as well as in `OnRefresh`. Code:
`UI/EncyclopediaPages/`, prefab `Encyclopedia/DiEncyclopediaCourt.xml`.

**v1.4.8 note, verified:** `EncyclopediaFactionPageVM.Refresh()` (public, virtual) is what fills
leader, clans, settlements; `RefreshValues()` only sets texts. If a section must update when the
page's own lists do, `Refresh` may be the better hook. Try `RefreshValues` first, as Diplomacy
does, and check on screen.

### 5.4 Encyclopedia, hero page

Target: `Append` after `RichTextWidget[@Text='@InformationText']`. Mixin on
`EncyclopediaHeroPageVM`, `vm.Obj` is the `Hero`. Diplomacy puts "Send Messenger" and "Grant
Fief" buttons here (`EncyclopediaHeroPageInject.xml`). For this project that is where Phase 2
(a lord's grievances, loyalty, bloc) and Phase 3 (recruit or read an agent) belong.

---

## 6. What not to copy from Diplomacy

| Diplomacy does | Why not here |
|---|---|
| Replaces the **whole** Diplomacy tab (`InsertType.Replace` on `Widget[@IsVisible='@Show']` with a 465-line copy, `DiplomacyPanelCustom.xml`) | Any game update to that panel is lost, and any other mod patching it breaks. Small patches that miss one at a time are this project's rule (`ModUI.cs` says why). Use it only to *read* how they laid out the Overview and Stats sub-tabs. |
| Sets backing fields directly in `OnRefresh` (`_allies = new()`) | No change notification; works only by luck of timing (§1) |
| No `try/catch` in mixins or button handlers | A throw in `OnRefresh` or a click comes out inside Gauntlet and takes the game down (CLAUDE.md §3) |
| Opens its own Gauntlet layers (`GauntletInterfaces/`) and a map overlay (`Views/GauntletWarExhaustionIndicator.cs`, `[ViewCreatorModule] MapView`) | Lead's decision: native dialogs, no screen of our own. Also needs extra references (`SandBox.View`, `TaleWorlds.MountAndBlade.View`) that this project does not have. Know the pattern exists; do not start using it without the lead. |
| Builds `net6` too, and multiple game versions | Out of scope; `net472` and v1.4.8 only |

Worth copying as ideas: the tooltip-from-`ExplainedNumber` pattern, reusable parameterised
button prefabs (`Standard/BasicDiplomacyButton.xml`), gold/influence cost rows under a
button, and the Encyclopedia sections.

---

## 7. Workflow: how to do one UI change without guessing

1. **Find the VM and the rebuild point.** `dotnet run --project tools/ApiDump -- <TypeName>`
   for the public surface. For *which method rebuilds what*, the IL call list is needed, and
   ApiDump does not print it today. A Cecil probe that lists, per method, the `call`/`callvirt`
   targets (and `Collection.Clear/Add`) answered §3.1 in one run; add that as an `--calls` mode
   to ApiDump rather than writing a throwaway each time.
2. **Read the vanilla prefab** in `<game>/Modules/SandBox/GUI/Prefabs/...`. Note the data context
   at your insertion point, and the brushes and sprites the neighbours use.
3. **Write the mixin**: `Di` prefix, `try/catch` in `OnRefresh` and every
   `[DataSourceMethod]`, setters that notify, null guards, event cleanup in `OnFinalize`.
4. **Write the prefab file** by copying vanilla markup and changing only bindings. Then a small
   `Prefabs2` patch that injects it.
5. `pwsh ./scripts/build.ps1`, then stop the game (`games_stop`), `pwsh ./scripts/deploy.ps1`,
   `games_start`, load `di_phase1_full`.
6. **Verify on screen** with `ui.take_screenshot` on the Kingdom → Diplomacy tab, **and click a
   different kingdom in the list, then back**. The vanishing-row bug only shows after a
   re-selection; a first screenshot would have passed.
7. If nothing appears: check the UIExtenderEx and ButterLib output for "XPath did not match" or
   a mixin error, then the mod log (`UI` category).

Checklist before calling a UI change done:

- [ ] Survives re-selecting items, switching tabs, closing and reopening the screen
- [ ] Survives save → load
- [ ] Renders for a player with no kingdom, as a vassal, and as a ruler
- [ ] Nothing thrown: mod log clean after the steps above
- [ ] Screenshot attached to the report, and anything not exercised is stated as unverified

---

## 8. Diplomacy file index (for reading, not pasting)

| Topic | File in `src/Bannerlord.Diplomacy/` |
|---|---|
| Registration | `SubModule.cs` (`UIExtender.Create/Register/Enable`) |
| Rows in the vanilla comparison bars | `ViewModelMixin/KingdomWarItemVMMixin.cs`, `KingdomTruceItemVmMixin.cs` |
| Tab-level mixin, list filtering, event refresh | `ViewModelMixin/KingdomDiplomacyVMMixin.cs` |
| Full panel replacement (read only) | `ViewModelMixin/DiplomacyPanelPrefabExtension.cs`, `_Module/GUI/Prefabs/KingdomManagement/Diplomacy/*.xml` |
| Header tab + width patch | `ViewModelMixin/KingdomManagementPrefabExtension.cs`, `KingdomManagementVMMixin.cs` |
| Clans tab buttons | `ViewModelMixin/KingdomClanVMMixin.cs`, `_Module/GUI/Prefabs/KingdomManagement/Clan/ClansPanel.xml` |
| Encyclopedia kingdom page | `ViewModelMixin/EncyclopediaFactionPage*.cs`, `_Module/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaFactionPageInject.xml`, `FactionButtonInject.xml` |
| Encyclopedia hero page | `ViewModelMixin/EncyclopediaHeroPage*.cs`, `.../EncyclopediaHeroPageInject.xml` |
| Tooltip from `ExplainedNumber` | `KingdomTruceItemVmMixin.UpdateDiplomacyTooltip` |
| Reusable button prefab with parameters | `_Module/GUI/Prefabs/Standard/BasicDiplomacyButton.xml` |
| Custom brushes | `_Module/GUI/Brushes/Diplomacy.xml` |
| Own layers and map overlay (not for this project) | `GauntletInterfaces/*.cs`, `Views/GauntletWarExhaustionIndicator.cs` |
