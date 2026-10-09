# DataSaveManager

A small persistence layer for tweakable settings of an installed app (e.g. values an on-site
technician adjusts). One save per app; human-readable and hand-editable.

## Language

**Entry**:
A named, typed setting — identified by its **Key**, with a **Data Type**, a **Default**, and a display **Label**.
_Avoid_: field, variable, constant

**Entry Definition**:
The authored description of an **Entry** (key, data type, default, label, exposed flag), kept in the **Config**.
_Avoid_: DSMConstant, schema

**Config**:
The single asset holding every **Entry Definition** plus save behaviour (autosave, save location).

**Value**:
The current value of an **Entry** at runtime: its **Override** if one exists, otherwise its **Default**.

**Override**:
A **Value** explicitly set for an **Entry**. Only **Overrides** are persisted; an **Entry** never set keeps following its **Default**, even if the **Default** later changes.

**Save File**:
The one on-disk file holding the **Overrides** — a flat key→value document. There is exactly one per app.
_Avoid_: slot, save slot, envelope

**Exposed Entry**:
An **Entry** flagged to appear on the **Runtime Panel** for on-site adjustment.

**Range**:
An optional min/max on an **Exposed Entry** of type Int or Float. A **Range** tells the **Runtime Panel** to present the **Entry** as a slider; it is a presentation hint only and never clamps the **Value**.
_Avoid_: limit, bounds, constraint

**Header**:
An optional title on an **Exposed Entry**, shown on the **Runtime Panel** directly above that **Entry**'s row to start a visual group. Presentation only; it has no effect on the **Value**.
_Avoid_: section, category, group

**Runtime Panel**:
The in-game UI where an operator views and edits **Exposed Entries** while the app runs.
_Avoid_: config canvas

**Watch**:
Subscribing to an **Entry** to receive its **Value** now and every time it changes, whatever the cause (set, load, reset).

**Reset**:
Removing **Overrides** so **Values** fall back to their **Defaults**.

## Relationships

- The **Config** holds zero or more **Entry Definitions**; each defines exactly one **Entry**.
- The **Save File** stores **Values** by **Key**; a **Key** in the **Save File** with no **Entry Definition** is an _orphan_.
- An **Exposed Entry** is an **Entry** — not a separate kind of data.
- An **Exposed Entry** has at most one **Range**; with a **Range** it shows as a slider, without one as a plain input. A **Value** outside its **Range** is kept as-is.
- An **Exposed Entry** has at most one **Header**; an **Entry** that is not exposed shows no **Header**.
- A **Reset** from the **Runtime Panel** covers only **Exposed Entries**, and only after the operator confirms; **Overrides** of other **Entries** and of _ad-hoc keys_ survive it.
- A **Key** used in code without an **Entry Definition** is an _ad-hoc key_: it works and persists like any **Override**, but is never added to the **Config** automatically.

## Flagged ambiguities

- "Slot" used to mean one of several named saves — resolved: slots are removed; there is one **Save File**.
- "Constant" (`DSMConstant`) used to mean both the default value and the key name — resolved: defaults live in **Entry Definitions**; there is no generated constant class.
