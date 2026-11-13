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

**Option**:
A named choice (display text plus the int it stands for) in the **Options** of an **Exposed Entry** of type Int. **Options** tell the **Runtime Panel** to present the **Entry** as a dropdown; like a **Range**, they are a presentation hint only and never clamp the **Value**. Each **Option** keeps its own int, so reordering **Options** never changes what was saved.
_Avoid_: choice, item, enum value

**Header**:
An optional title on an **Exposed Entry**, shown on the **Runtime Panel** directly above that **Entry**'s row to start a visual group. Presentation only; it has no effect on the **Value**.
_Avoid_: section, category, group

**Audience**:
A named kind of operator (e.g. Technician, Admin) that an **Exposed Entry** is meant for. An **Entry** may name several **Audiences**; one naming none is meant for every **Audience**. Every **Audience** is declared once in the **Config**.
_Avoid_: tag, group, app, category

**Presentation Hint**:
A **Range**, set of **Options**, **Header**, or set of **Audiences** on an **Exposed Entry**. It changes how and where the **Runtime Panel** shows the **Entry**, never its **Value**.
_Avoid_: widget setting, UI option

**Runtime Panel**:
The in-game UI where an operator views and edits **Exposed Entries** while the app runs. An app may have more than one, each serving its own **Audiences**.
_Avoid_: config canvas

**Watch**:
Subscribing to an **Entry** to receive its **Value** now and every time it changes, whatever the cause (set, load, reset).

**Reset**:
Removing **Overrides** so **Values** fall back to their **Defaults**.

**Binding**:
A component that pushes one **Entry**'s **Value**, live and one way, into members of other components on the same GameObject.
_Avoid_: data binding, sync

**Link**:
One target member of a **Binding**. When the member takes text and the **Entry** is not a String, the **Value** goes through the **Link**'s **Format** first.
_Avoid_: target, connection

**Format**:
The .NET composite format string on a **Link** (e.g. `Speed: {0:0.0}`) that turns a non-text **Value** into text.

## Relationships

- The **Config** holds zero or more **Entry Definitions**; each defines exactly one **Entry**.
- The **Save File** stores **Values** by **Key**; a **Key** in the **Save File** with no **Entry Definition** is an _orphan_.
- An **Exposed Entry** is an **Entry** — not a separate kind of data.
- An **Exposed Entry** has at most one **Range**; with a **Range** it shows as a slider, without one as a plain input. A **Value** outside its **Range** is kept as-is.
- An **Exposed Entry** of type Int has at most one set of **Options**; with **Options** it shows as a dropdown. **Options** and a **Range** are mutually exclusive (**Options** win). A **Value** matching no **Option** is kept as-is and the dropdown shows it as unrecognised; nothing is written until the operator picks an **Option**.
- The **Default** of an **Entry** with **Options** is chosen from its **Options** at authoring time; at runtime it is not enforced.
- An **Exposed Entry** has at most one **Header**; an **Entry** that is not exposed shows no **Header**.
- An **Entry** that is not exposed is shown to no **Audience**, whatever **Audiences** it names.
- An **Audience** cannot be removed from the **Config** while any **Entry** or **Runtime Panel** still names it. Renaming an **Audience** changes no **Entry** or **Runtime Panel**.
- Which **Presentation Hints** an **Entry** may carry depends on its **Data Type**: a **Range** needs Int or Float, **Options** need Int, a **Header** fits any type.
- A **Runtime Panel** serves zero or more **Audiences**. It shows an **Exposed Entry** when the **Entry** names no **Audience**, or shares at least one **Audience** with the **Runtime Panel**. A **Runtime Panel** serving no **Audience** shows every **Exposed Entry**.
- **Audiences** decide only where an **Entry** is shown. An **Entry** a **Runtime Panel** does not show still exists in that app: code can read and set it, and **Bindings** still follow it.
- Apps built from one **Config** share **Entry Definitions**, not **Values**: each app has its own **Save File**, so the same **Entry** can hold a different **Value** in each app.
- A **Reset** from a **Runtime Panel** covers only the **Exposed Entries** that **Runtime Panel** shows, and only after the operator confirms; **Overrides** of every other **Entry** and of _ad-hoc keys_ survive it.
- A **Binding** has one or more **Links**. A **Link** whose member takes the **Entry**'s own **Data Type** receives the **Value** as-is; only when no such member exists does a text member receive it through the **Format**.
- A **Key** used in code without an **Entry Definition** is an _ad-hoc key_: it works and persists like any **Override**, but is never added to the **Config** automatically.

## Flagged ambiguities

- "Slot" used to mean one of several named saves — resolved: slots are removed; there is one **Save File**.
- "Constant" (`DSMConstant`) used to mean both the default value and the key name — resolved: defaults live in **Entry Definitions**; there is no generated constant class.
