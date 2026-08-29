# Git Strategy

## Branches

main
feature/*
fix/*
chore/*

## Feature Size

One coherent feature per branch.

## Commit Style

Use conventional prefixes:

feat:
fix:
test:
refactor:
docs:
chore:

Examples:

feat: add customer management
test: add quote workflow tests
fix: prevent cross-business customer access

## Pull Request Expectations

A PR should explain:

- what changed
- why
- how it was tested
- known limitations
- database/migration impact

Avoid unrelated refactoring in feature PRs.
