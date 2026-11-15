# Security Policy

## Reporting a vulnerability

Please **do not** open a public issue for a security problem.

Use GitHub's private vulnerability reporting:

1. Open the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Include affected version, input, impact, and a minimal reproduction.

If private reporting is unavailable, contact the maintainer listed in the repository profile.

## Scope

This library parses untrusted Markdown but does not execute HTML, JavaScript, or embedded content. Relevant risks include:

- excessive CPU or memory from pathological input;
- parser exceptions on malformed input;
- incorrect escaping guidance for downstream renderers;
- dependency or build supply-chain issues.

## Response

Maintainers will acknowledge a report as soon as practical, validate it, prepare a fix and regression test, and publish a release note without disclosing exploit details before a fix is available.

## Downstream responsibility

A renderer must escape text, validate URLs, and decide whether remote images are allowed. Those policies are outside this library.
