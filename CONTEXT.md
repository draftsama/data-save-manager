# DataSaveManager

A small persistence layer for tweakable settings of an installed app (e.g. values an on-site
technician adjusts), plus the on-site actions that technician runs. One save per app;
human-readable and hand-editable.

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
A named kind of operator (e.g. Technician, Admin) that an **Exposed Entry** or a **Command** is meant for. An **Entry** may name several **Audiences**; one naming none is meant for every **Audience**. Every **Audience** is declared once in the **Config**.
_Avoid_: tag, group, app, category

**Presentation Hint**:
A **Range**, set of **Options**, **Header**, or set of **Audiences** on an **Exposed Entry**. It changes how and where the **Runtime Panel** shows the **Entry**, never its **Value**.
_Avoid_: widget setting, UI option

**Runtime Panel**:
The in-game UI where an operator views and edits **Exposed Entries** and runs **Commands** and **Entry Actions** while the app runs. An app may have more than one, each serving its own **Audiences**.
_Avoid_: config canvas

**Panel Definition**:
The authored description of one **Runtime Panel** (its **Audiences**, **Open Triggers**, **Panel Lock** and layout), kept in the **Config**. The app creates each **Runtime Panel** from its **Panel Definition** when it starts.
_Avoid_: panel settings, panel profile

**Open Trigger**:
A way an operator opens a **Runtime Panel** by hand: a hotkey or a **Corner Tap**. Each **Runtime Panel** has its own **Open Triggers**; several can be on at once.
_Avoid_: toggle, shortcut, gesture

**Corner Tap**:
An **Open Trigger**: tapping or clicking one chosen corner of the screen a set number of times in quick succession. It only opens; it never closes the **Runtime Panel**.
_Avoid_: secret tap, hidden button

**Panel Lock**:
A **Runtime Panel** that asks for its **Passcode** every time an **Open Trigger** opens it.
_Avoid_: password protection, login

**Passcode**:
The numeric code of a **Panel Lock**, entered on an on-screen keypad. It belongs to one **Runtime Panel** and is set when the app is built; it is never an **Entry** and never in the **Save File**.
_Avoid_: password, PIN

**Lock Screen**:
The full-screen keypad a **Panel Lock** shows before its **Runtime Panel** opens. It checks the **Passcode** as soon as enough digits are entered; a wrong one is cleared for another try, and Cancel or the hotkey leaves the **Runtime Panel** closed.
_Avoid_: password dialog, login screen

**Command**:
A named action an operator runs from the **Runtime Panel**, with a **Title** and an optional **Description**. It takes no input; a **Command** that needs input reads it from **Entries**. It is code, not data: it is never saved and has no **Entry Definition**.
_Avoid_: action, button, function

**Registered Command**:
A **Command** that is live right now because the object that owns it has registered it. The **Runtime Panel** lists only **Registered Commands**; a **Command** whose owner is gone or disabled is not shown.
_Avoid_: available command

**Entry Action**:
A named action shown beside one **Exposed Entry** on the **Runtime Panel**, with a **Title**, whose result becomes that **Entry**'s **Value** (e.g. browse for a folder, fetch a token). Like a **Command** it is code, not data, and is live only while its owner has registered it; unlike a **Command** it belongs to exactly one **Entry** and exists to change its **Value**.
_Avoid_: entry command, value source, fill button

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
- The **Config** holds zero or more **Panel Definitions**, each with a unique name. The app creates one **Runtime Panel** per enabled **Panel Definition** when it starts, and that **Runtime Panel** lasts until the app quits, across scene loads. With no enabled **Panel Definition** there is no **Runtime Panel**.
- Code finds a **Runtime Panel** by its **Panel Definition**'s name, so renaming a **Panel Definition** breaks code that used the old name.
- Two **Panel Definitions** may share an **Open Trigger**; one use of it then opens both **Runtime Panels**.
- A **Panel Lock** guards only the **Open Triggers**; code that opens a **Runtime Panel** directly is not asked for the **Passcode**. A wrong **Passcode** can be retried without limit. A **Runtime Panel** with a **Panel Lock** but no **Lock Screen** refuses to open from its **Open Triggers**.
- While its **Lock Screen** is up, a **Runtime Panel** counts as open to the rest of the app, as it does once unlocked.
- A **Runtime Panel** with a **Panel Lock** and one without can serve different **Audiences** in the same app (e.g. a locked Admin panel beside an open Technician panel).
- A **Command** names zero or more **Audiences** and is shown by the same rule as an **Exposed Entry**. Unlike an **Entry**, it names them by name, so renaming an **Audience** drops it from **Commands** that named the old name.
- A **Command** may require confirmation; the operator then confirms before it runs.
- Each owner registers its own **Commands**, so two owners of the same kind give two **Registered Commands** with the same **Title**; the **Runtime Panel** tells them apart by owner.
- A **Command** keeps running when the **Runtime Panel** closes. It is stopped when its owner unregisters. While it runs it cannot be run again; other **Commands** can.
- A **Command**'s last outcome (running, succeeded, failed) belongs to the **Registered Command**, not to the **Runtime Panel**, and is shown again when the **Runtime Panel** reopens.
- A **Command** shows a **Header** above it to start a visual group, as an **Exposed Entry** does; when the **Command** listed just before it has the same **Header**, the **Header** is shown once for both.
- A **Runtime Panel** with no **Command** to show has no Commands tab and looks as it did before **Commands** existed.
- A **Command** never changes a **Value** by being run through the **Runtime Panel**; anything it changes, it changes through code like any other caller.
- A **Binding** has one or more **Links**. A **Link** whose member takes the **Entry**'s own **Data Type** receives the **Value** as-is; only when no such member exists does a text member receive it through the **Format**.
- A **Key** used in code without an **Entry Definition** is an _ad-hoc key_: it works and persists like any **Override**, but is never added to the **Config** automatically.
- An **Entry Action** names its **Entry** by **Key** and must produce a **Value** of that **Entry**'s **Data Type**. It is shown wherever its **Entry** is shown and nowhere else; it has no **Audiences** or **Header** of its own.
- An **Entry Action** is given the **Entry**'s current **Value** when it starts. When it finishes, its result is set exactly as if the operator had typed it: it becomes an **Override** at once, and saving follows the usual rules. A cancelled **Entry Action** leaves the **Value** unchanged. Its result is set even if the **Value** changed while it ran: the last write wins.
- One **Entry** may have several **Entry Actions**. While any of them runs, that **Entry**'s row on the **Runtime Panel** is locked: no other of its **Entry Actions** and no typing.
- An **Entry Action** may require confirmation, like a **Command**. It keeps running when the **Runtime Panel** closes and is stopped when its owner unregisters, like a **Command**. Two owners giving one **Entry** an **Entry Action** with the same **Title** are told apart by owner, like **Commands**.

## Flagged ambiguities

- "Slot" used to mean one of several named saves — resolved: slots are removed; there is one **Save File**.
- "Constant" (`DSMConstant`) used to mean both the default value and the key name — resolved: defaults live in **Entry Definitions**; there is no generated constant class.
