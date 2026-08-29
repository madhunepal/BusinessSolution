# Agent Operating Model

## Human Owns

- product decisions
- business rules
- module boundaries
- architecture approval
- security decisions
- UX direction
- final acceptance testing
- production approval

## Agent Owns

- implementation
- boilerplate
- repetitive CRUD
- tests
- refactoring within approved boundaries
- documentation updates
- build/test execution
- diagnosis of concrete bugs

## Rule

The agent proposes architecture; the human approves architecture.

## Preferred Task Size

One coherent vertical slice.

Good:
"Implement Customer CRUD including persistence, application logic, UI, validation and tests."

Too broad:
"Build CRM."

Too small:
"Create Customer.cs."

## Plan → Approve → Implement → Verify

For architectural or cross-module work:

1. Inspect
2. Plan
3. Human approval
4. Implement
5. Test
6. Review
7. Manual acceptance
8. Commit
