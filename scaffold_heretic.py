#!/usr/bin/env python3
"""
Corvax-Wega :: Heretic feature scaffolder.

Run from the ROOT of the ss14-wega repo:
    python3 scaffold_heretic.py

Creates the full _Wega/Heretic skeleton (dirs + stub files), mirroring the
BloodCult layout. Never overwrites existing files. Stubs compile as-is; YAML /
locale files are comment-only so they don't trip prototype validation until we
fill them in by phase.

Convention (verified against BloodCult):
  - physical folder lives under _Wega/...
  - C# namespaces DO NOT contain _Wega (e.g. Content.Shared.Heretic)
  - rule component keeps Content.Server.GameTicking.Rules.Components
  - new files under _Wega need NO `// Corvax-Wega-` tag (tags are only for
    edits to code OUTSIDE _Wega). Tag those few touches when we wire things up.
"""

import os
import sys

# --- tiny helpers -----------------------------------------------------------

CREATED, SKIPPED = [], []

def w(path: str, content: str) -> None:
    """Write file if absent; create parent dirs."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        SKIPPED.append(path)
        return
    with open(path, "w", encoding="utf-8") as f:
        f.write(content.rstrip("\n") + "\n")
    CREATED.append(path)

def cs_component(ns: str, name: str, networked: bool, phase: str, todo: str) -> str:
    attrs = "[RegisterComponent, NetworkedComponent]" if networked else "[RegisterComponent]"
    using = "using Robust.Shared.GameStates;\n" if networked else ""
    return f"""// Corvax-Wega :: Heretic ({phase}) — stub, fill in by phase.
{using}
namespace {ns};

{attrs}
public sealed partial class {name} : Component
{{
    // TODO ({phase}):
{todo}
}}
"""

def cs_system(ns: str, name: str, phase: str, partial: bool = False, base: str = "EntitySystem") -> str:
    p = "partial " if partial else ""
    body = (
        "    public override void Initialize()\n"
        "    {\n"
        "        base.Initialize();\n"
        f"        // TODO ({phase}): subscribe events / wire abilities.\n"
        "    }\n"
        if not partial else
        f"    // TODO ({phase}): partial-class members for this concern.\n"
    )
    return f"""// Corvax-Wega :: Heretic ({phase}) — stub, fill in by phase.

namespace {ns};

public sealed {p}class {name} : {base}
{{
{body}}}
"""

# --- file map ---------------------------------------------------------------

def build():
    root = os.getcwd()

    # ---------- SHARED ----------
    w(f"{root}/Content.Shared/_Wega/Heretic/HereticComponent.cs",
      cs_component(
        "Content.Shared.Heretic.Components", "HereticComponent", True, "Phase 1",
        "    //   public List<ProtoId<HereticKnowledgePrototype>> KnownKnowledge = new();\n"
        "    //   public List<ProtoId<HereticRitualPrototype>> KnownRituals = new();\n"
        "    //   public string? CurrentPath;          // \"Ash\" / \"Rust\" / ...\n"
        "    //   public int PathStage;                // 0 = no path, 10 = ascension\n"
        "    //   public int KnowledgePoints;\n"
        "    //   public List<EntityUid> SacrificeTargets = new();  // 5 assigned, anti-RDM\n"
        "    //   public bool Ascended;"))

    w(f"{root}/Content.Shared/_Wega/Heretic/HereticEnums.cs",
      """// Corvax-Wega :: Heretic (Phase 1) — stub.

namespace Content.Shared.Heretic;

// TODO (Phase 1/6): paths the heretic can commit to.
public enum HereticPath : byte
{
    None = 0,
    Ash,
    Rust,
    Flesh,
    Void,
    Blade,
}
""")

    w(f"{root}/Content.Shared/_Wega/Heretic/HereticEvents.cs",
      """// Corvax-Wega :: Heretic (Phase 2) — stub.

namespace Content.Shared.Heretic;

// TODO (Phase 2): grasp-hit event that path systems listen to (modular grasp).
// Each path adds its effect to the target if the heretic owns that upgrade.
// public sealed class MansusGraspHitEvent : EntityEventArgs { ... }
""")

    w(f"{root}/Content.Shared/_Wega/Heretic/SharedHereticSystem.cs",
      cs_system("Content.Shared.Heretic", "SharedHereticSystem", "Phase 1"))

    w(f"{root}/Content.Shared/_Wega/Heretic/Prototypes/HereticKnowledgePrototype.cs",
      """// Corvax-Wega :: Heretic (Phase 3) — stub.
using Robust.Shared.Prototypes;

namespace Content.Shared.Heretic.Prototypes;

// One node of the knowledge tree. A node is just DATA that, when bought, grants
// a combination of: actions (spells), rituals (rune recipes), components
// (passives, e.g. a grasp-effect), and path/stage progression.
[Prototype("hereticKnowledge")]
public sealed partial class HereticKnowledgePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    // TODO (Phase 3):
    //   [DataField] public string? Path;
    //   [DataField] public int Stage;
    //   [DataField] public int Cost = 1;
    //   [DataField] public List<EntProtoId> Actions = new();
    //   [DataField] public List<ProtoId<HereticRitualPrototype>> Rituals = new();
    //   [DataField] public ComponentRegistry Components = new();
}
""")

    w(f"{root}/Content.Shared/_Wega/Heretic/Prototypes/HereticRitualPrototype.cs",
      """// Corvax-Wega :: Heretic (Phase 5) — stub.
using Robust.Shared.Prototypes;

namespace Content.Shared.Heretic.Prototypes;

// A transmutation recipe performed on a rune (blade, focus, robe, sacrifice...).
[Prototype("hereticRitual")]
public sealed partial class HereticRitualPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    // TODO (Phase 5):
    //   [DataField] public Dictionary<string, int> RequiredTags = new(); // ingredients
    //   [DataField] public List<EntProtoId> Output = new();
    //   [DataField] public string? CustomBehavior;  // e.g. Sacrifice
}
""")

    # ---------- SERVER ----------
    w(f"{root}/Content.Server/_Wega/Heretic/HereticSystem.cs",
      cs_system("Content.Server.Heretic", "HereticSystem", "Phase 1/3", partial=True))

    w(f"{root}/Content.Server/_Wega/Heretic/HereticSystem.Knowledge.cs",
      cs_system("Content.Server.Heretic", "HereticSystem", "Phase 3", partial=True))

    w(f"{root}/Content.Server/_Wega/Heretic/MansusGraspSystem.cs",
      cs_system("Content.Server.Heretic", "MansusGraspSystem", "Phase 2"))

    w(f"{root}/Content.Server/_Wega/Heretic/HereticRitualSystem.cs",
      cs_system("Content.Server.Heretic", "HereticRitualSystem", "Phase 5"))

    w(f"{root}/Content.Server/_Wega/Heretic/EldritchInfluenceSystem.cs",
      cs_system("Content.Server.Heretic", "EldritchInfluenceSystem", "Phase 4"))

    w(f"{root}/Content.Server/_Wega/Heretic/Abilities/HereticAbilitySystem.Ash.cs",
      cs_system("Content.Server.Heretic", "HereticAbilitySystem", "Phase 6", partial=True))

    # ---------- SERVER :: RULE ----------
    w(f"{root}/Content.Server/_Wega/GameTicking/Rules/Components/HereticRuleComponent.cs",
      cs_component(
        "Content.Server.GameTicking.Rules.Components", "HereticRuleComponent", False, "Phase 1",
        "    //   [DataField] public int SacrificeTargetCount = 5;   // anti-RDM: only these give points\n"
        "    //   [DataField] public EntProtoId ObjectivePrototype = \"HereticSacrificeObjective\";\n"
        "    //   public HashSet<EntityUid> Heretics = new();"))

    w(f"{root}/Content.Server/_Wega/GameTicking/Rules/HereticRuleSystem.cs",
      """// Corvax-Wega :: Heretic (Phase 1) — stub.
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;

namespace Content.Server.GameTicking.Rules;

// Mirrors BloodCultRuleSystem: hook AfterAntagEntitySelectedEvent -> MakeHeretic.
public sealed class HereticRuleSystem : GameRuleSystem<HereticRuleComponent>
{
    // TODO (Phase 1):
    //   [Dependency] private readonly AntagSelectionSystem _antag = default!;
    //   subscribe AfterAntagEntitySelectedEvent -> MakeHeretic(ent):
    //     EnsureComp<HereticComponent>, grant starting kit, assign 5 targets,
    //     give briefing.
    public override void Initialize()
    {
        base.Initialize();
        // TODO (Phase 1): wire antag selection.
    }
}
""")

    # ---------- CLIENT ----------
    w(f"{root}/Content.Client/_Wega/Heretic/HereticSystem.cs",
      cs_system("Content.Client.Heretic", "HereticSystem", "Phase 3"))

    # ---------- PROTOTYPES (comment-only until filled) ----------
    yml_note = lambda phase, what: (
        f"# Corvax-Wega :: Heretic ({phase}) — {what}\n"
        f"# Empty on purpose: filled in during {phase}. Keeping it comment-only\n"
        f"# avoids prototype validation errors before the C# types are wired.\n")

    w(f"{root}/Resources/Prototypes/_Wega/Roles/Antags/heretic.yml",
      yml_note("Phase 1", "antag + antagSpecifier + mindRole + startingGear (mirror bloodcultist.yml)"))
    w(f"{root}/Resources/Prototypes/_Wega/Actions/heretic.yml",
      yml_note("Phase 2", "grasp, jaunt, living heart, store, Ash spells"))
    w(f"{root}/Resources/Prototypes/_Wega/Objectives/heretic.yml",
      yml_note("Phase 1/5", "sacrifice-targets + ascension objectives"))
    w(f"{root}/Resources/Prototypes/_Wega/Entities/Structures/Specific/heretic_rune.yml",
      yml_note("Phase 5", "transmutation rune (the workbench)"))
    w(f"{root}/Resources/Prototypes/_Wega/Entities/Objects/Specific/Heretic/items.yml",
      yml_note("Phase 1/2/5", "codex, living heart, focus, Ash blade, robe"))
    w(f"{root}/Resources/Prototypes/_Wega/Heretic/knowledge.yml",
      yml_note("Phase 3/6", "hereticKnowledge nodes — base kit + Ash tree"))
    w(f"{root}/Resources/Prototypes/_Wega/Heretic/rituals.yml",
      yml_note("Phase 5", "hereticRitual recipes — blade, sacrifice, robe"))

    # ---------- LOCALE (comment-only) ----------
    for loc in ("ru-RU", "en-US"):
        w(f"{root}/Resources/Locale/{loc}/_wega/heretic/heretic.ftl",
          f"# Corvax-Wega :: Heretic — {loc} locale (no tag needed for locale).\n"
          f"# TODO: roles-antag-heretic-name, -objective, action names, briefing.\n")

    # ---------- TEXTURES ----------
    w(f"{root}/Resources/Textures/_Wega/Heretic/.gitkeep", "")
    w(f"{root}/Resources/Textures/_Wega/Heretic/README.md",
      """# Heretic textures (Corvax-Wega)

Sprites are taken from **tgstation** (icons are CC-BY-SA-3.0, unlike their AGPL
code). Cut each source `.dmi` into an SS14 `.rsi` folder here.

For v1 (base kit + Ash) we need RSIs for:
  - heretic_rune       (transmutation rune, 3x3 green)
  - mansus_grasp       (action icon + touch effect)
  - blade_ash          (Ash path blade)
  - codex_cicatrix     (book / focus)
  - living_heart       (heart item)
  - amber_focus        (necklace)
  - influence          (eldritch reality tear)
  - robe_ash           (Scorched Mantle)
  - actions            (jaunt, living heart, store, volcano blast icons)

Every `<name>.rsi/meta.json` MUST carry attribution — see meta.json.template.
""")
    w(f"{root}/Resources/Textures/_Wega/Heretic/meta.json.template",
      """{
  "version": 1,
  "license": "CC-BY-SA-3.0",
  "copyright": "Taken from tgstation at <commit-hash-or-url>",
  "size": { "x": 32, "y": 32 },
  "states": [
    { "name": "REPLACE_ME" }
  ]
}
""")

    # ---------- FEATURE README ----------
    w(f"{root}/Content.Shared/_Wega/Heretic/README_HERETIC.md",
      """# Heretic — Corvax-Wega

Design target: **/tg/station** balance (anti-RDM: only 5 assigned sacrifice
targets give points, sacrifice is non-lethal, influences are the quiet source).
NOT a port of Goob (AGPL + RDM-prone). We build clean from the /tg/ *design*.

## Phases
1. Skeleton: HereticRuleComponent/System + HereticComponent + heretic.yml role + briefing.
2. Mansus Grasp: action -> MansusGraspHitEvent -> effect list (knockdown + garble).
3. Knowledge tree + store: HereticKnowledgePrototype + points + buying nodes.
4. Influences: spawn + drain -> points.
5. Runes + Living Heart + restricted sacrifice (anti-RDM) + non-lethal stub.
6. Ash path: nodes + Ash abilities + blade + ascension.

## Tagging
New files under _Wega need NO `// Corvax-Wega-` tag. Only tag edits to code
OUTSIDE _Wega (e.g. registering a guidebook entry, faction, game preset).
""")


if __name__ == "__main__":
    build()
    print(f"\n  Heretic scaffold complete.")
    print(f"  created: {len(CREATED)}   skipped (already existed): {len(SKIPPED)}\n")
    for p in CREATED:
        print("  +", os.path.relpath(p))
    if SKIPPED:
        print("\n  untouched:")
        for p in SKIPPED:
            print("  =", os.path.relpath(p))
