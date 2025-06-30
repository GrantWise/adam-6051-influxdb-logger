# CLAUDE.md - AI Assistant Guide

This document provides guidance for the Claude AI assistant when working with the ServiceBridge repository. Its purpose is to ensure all contributions align with the project's architectural principles and quality standards.

## 1. Core Philosophy: Pragmatic Over Dogmatic

The primary goal is to write clean, maintainable, and understandable code. Guidelines serve this goal, not the other way around.

- **Prioritize Readability**: Choose the approach that is easiest for a human to understand.
- **Value Logical Cohesion**: Keep related functionality together. A 300-line class that tells a coherent story is better than three fragmented 100-line classes that scatter related logic.
- **Focus on Developer Experience**: Optimize for the next developer who will work on the code.

Before making a change, ask yourself:
1.  Does this make the code easier to understand?
2.  Would I want to maintain this code in six months?
3.  Can a new team member grasp this quickly?

## 2. Architectural & Development Standards

Refer to the official documentation for detailed standards. Your work must adhere to these principles.

- **Architecture**: See `docs/architecture_guide.md` for details on our Clean Architecture, CQRS, and DDD patterns.
- **Development**: See `docs/development_standards.md` for unified backend and frontend standards.
- **Technical Stack**: See `docs/technical_specification.md` for the list of approved technologies.

## 3. Debugging & Test Fixing Process

When fixing failing tests or debugging bugs, you **MUST** follow this process:

1.  **Identify the centralized pattern** causing multiple failures. Do not patch failures individually.
2.  **Find the root architectural violation** (e.g., SRP, SoC, DRY).
3.  **Create or fix a centralized utility/service** to resolve the root cause.
4.  **Apply the centralized fix** across all affected code.
5.  **Update test expectations** to match the correct, centralized behavior.

### Anti-Patterns to Avoid

- Fixing test failures one by one.
- Duplicating fixes across multiple files.
- Mixing unrelated concerns in a single component or class.
- Hardcoding values that should come from configuration.
- Implementing inconsistent error handling.

## 4. Code Quality Quick Reference

- **Function Size**: Aim for 20-40 lines, but prioritize readability. A 60-line function with a clear, single purpose is acceptable.
- **Class/Component Size**: Aim for ~200 lines, but prioritize logical cohesion. Do not fragment tightly coupled logic just to meet a size guideline.
- **Comments**: Comment on the *why*, not the *what*. Avoid obvious comments.
- **Error Handling**: Use specific exception types and structured logging. Return meaningful error messages.
- **Testing**: Follow the Arrange, Act, Assert pattern. Ensure tests are independent and have descriptive names.
