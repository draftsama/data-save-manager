# DataSaveManager — Project Instructions

## Comments
Default to no comments. Only add one when the WHY is non-obvious (hidden constraint, subtle invariant, workaround for a specific bug). Never explain WHAT the code does.

## Tests
Never run Unity tests (EditMode/PlayMode, batchmode, `-runTests`) yourself. Unity batchmode has caused deadlocks and Unix domain socket path failures in this project. Hand test execution to the human — describe what to run and what to expect, then wait for the result.
