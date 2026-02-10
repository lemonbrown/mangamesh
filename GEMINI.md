## Instruction Hierarchy

This document defines the default operating instructions for this project.

If additional instruction files from the `/ai` directory are present in the current context
(e.g. `ai/gemini.review.md`, `ai/gemini.strict.md`), you must:

1. Treat them as authoritative extensions or overrides
2. Apply the most specific instructions first
3. Resolve conflicts by favoring:
   - Task-specific instructions
   - Then role-specific instructions
   - Then this document

If no additional instruction files are provided, follow this document exclusively.
