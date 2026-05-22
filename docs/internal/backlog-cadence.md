# Backlog Cadence

Use this cadence once per week to keep the SDK backlog actionable and aligned
with the current release phase.

## Weekly Review

1. Triage new issues with `area/*`, `priority/*`, `effort/*`, `phase/*`, an
   assignee when ownership is clear, and a milestone when the work is release
   scoped.
2. Confirm the next two weeks have enough `ready-to-start` work. An issue is
   ready when it has a clear outcome, acceptance criteria, owner or owner group,
   and no unresolved dependency that blocks first implementation.
3. Add dependency notes to blocked issues. Link cross-repo blockers directly to
   `honua-server` or `honua-devops`.
4. Apply the scope gate to new work: record what is deferred or removed when
   new scope is accepted.
5. Review phase mix so MVP, Beta, and GA labels still match the current release
   goal.
6. Split oversized work before scheduling it unless the large ticket is
   intentionally accepted as a tracked epic.
7. Close completed work within 24 hours of merge. For partial work, leave a
   comment with the exact remaining tasks.

## Weekly Comment Template

Post this as a dated comment on the backlog cadence issue:

```markdown
## Weekly backlog review - YYYY-MM-DD

- New issues triaged:
- Ready-to-start for next two weeks:
- Blocked issues and dependency owner:
- Scope tradeoffs accepted:
- MVP/Beta/GA phase changes:
- Oversized tickets split or explicitly accepted:
- Done/close hygiene:
- Escalations:
```

## Escalation Rules

Escalate server protocol blockers to `honua-server`. Escalate release,
publishing, package feed, or workflow infrastructure blockers to `honua-devops`.
Keep the SDK issue open until the SDK-side remaining work is explicit and
actionable.
