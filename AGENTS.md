## Superpowers routing

Use the lightest workflow that can reliably complete the task. The presence or
availability of Superpowers does not mean it must be used for every development
request.

### Explicit user request

If the user explicitly requests Superpowers, names a specific Superpowers
skill, or uses an invocation such as `@superpowers`, use the requested workflow
unless doing so conflicts with a higher-priority instruction.

### Task classification

Before automatically invoking Superpowers, classify the task as L1, L2, or L3.
Start at the lowest level that can safely and reliably complete the work.

#### L1 — Simple

Handle directly without invoking the full Superpowers workflow.

Typical L1 tasks include:

- Conceptual questions and explanations
- Syntax, annotation, API, or framework-usage questions
- Small, obvious, localized bug fixes
- One-line or small single-file edits with clear requirements
- Formatting, renaming, comments, imports, or mechanical cleanup
- Routine configuration-value changes
- Reading or summarizing code without requested implementation

For L1 tasks, answer directly or make the change and perform only
risk-proportionate verification. Do not create unnecessary plans, design
documents, branches, subagents, brainstorming sessions, or multi-stage
workflows.

#### L2 — Standard

Use the normal development workflow without automatically invoking the full
Superpowers workflow.

Typical L2 tasks include:

- A well-specified feature contained within one component
- A routine API endpoint following an existing project pattern
- Straightforward CRUD implementation
- A localized refactor with clear boundaries
- A bug whose cause is reasonably clear and can be verified directly

For L2 tasks, inspect the relevant code, implement the change, run appropriate
tests or checks, and report the result. Escalate to L3 only when newly discovered
complexity, risk, or ambiguity justifies it.

#### L3 — Complex

Automatically invoke the appropriate Superpowers workflow when one or more of
the following conditions apply:

- The change spans multiple modules, services, repositories, or architectural layers
- The task requires an architecture, technology, protocol, or data-model decision
- Requirements are materially ambiguous and different interpretations would produce different implementations
- The task involves concurrency, distributed systems, security, authentication, authorization, transactions, migrations, compatibility, or data-loss risk
- Debugging requires multiple hypotheses, reproduction work, instrumentation, or investigation across components
- The implementation contains several dependent stages that must be coordinated
- The task is a broad refactor or has significant production blast radius
- A mistake could create serious reliability, security, financial, privacy, or irreversible-data impact

Select only the Superpowers capabilities relevant to the task. Do not run every
available Superpowers workflow by default.

### Routing principles

1. Default to the lowest sufficient level; do not default to Superpowers merely because it is installed.
2. Do not classify complexity only by lines changed. A two-line security, payment, concurrency, or migration change may still be L3.
3. Loading, inspecting, or checking the relevance of a skill does not require running its full workflow.
4. When the classification is uncertain, begin with the normal lightweight workflow and escalate only after concrete complexity or risk is discovered.
5. Do not announce or simulate a Superpowers workflow when the task does not pass the L3 threshold.
6. Do not use Superpowers for ceremony. Every invoked workflow must materially improve correctness, safety, or decision quality for the current task.
7. Explicit user invocation overrides the automatic L1/L2/L3 threshold, subject to higher-priority instructions.
