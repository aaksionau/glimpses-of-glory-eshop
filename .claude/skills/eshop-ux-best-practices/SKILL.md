---
name: eshop-ux-best-practices
description: Best-practice checklist for e-commerce storefront UI/UX — navigation, product listing, product detail, cart, checkout, trust, accessibility, performance. Use when designing new storefront UI, or when asked to evaluate/audit/review this project's current styles, design, or UX.
---

# E-shop UX best practices

Stack-agnostic heuristics for evaluating or designing storefront UI. Grounded in well-established e-commerce UX research (Baymard Institute, Nielsen Norman Group conventions), not this project's specific tech.

## Core principles

1. **Reduce friction to purchase** — every extra click, field, or ambiguous state is a chance to lose the customer. Guest checkout, minimal required fields, visible progress.
2. **Show state honestly and early** — price, shipping cost, stock status, and totals should never surprise the user later in the flow.
3. **Make the primary action obvious** — one clear visual hierarchy per page: a single dominant CTA (Add to cart, Checkout), secondary actions visually subordinate.
4. **Never leave the user guessing** — loading, empty, error, and success states must all be designed, not just the happy path.
5. **Accessibility is not optional** — keyboard navigation, focus states, alt text, label associations, and color contrast are baseline, not enhancements.
6. **Mobile is the primary surface** — design and test touch targets, tap zones, and responsive breakpoints first; desktop is the enhancement.

## Workflow: evaluating a storefront

When asked to review/audit/evaluate an eshop's UI/UX:

1. Identify which page types exist (nav/search, product listing, product detail, cart, checkout, account/order pages).
2. Read [CHECKLIST.md](CHECKLIST.md) and walk each existing page type against its relevant section.
3. For each finding, note: **what's missing/wrong → why it matters (which principle above) → concrete fix**. Don't just list "best practices exist" — point to the actual file/element that falls short.
4. Rank findings by conversion/usability impact, not by how easy they are to fix. A missing shipping-cost disclosure in checkout outranks an inconsistent border-radius.
5. Distinguish "violates a hard best practice" (e.g., no keyboard access to Add to cart) from "stylistic preference" (e.g., could use more whitespace) — don't flag the latter with the same severity as the former.

## Quick reference

See [CHECKLIST.md](CHECKLIST.md) for the full checklist, organized by:
- Navigation & search
- Product listing (PLP)
- Product detail (PDP)
- Cart
- Checkout
- Trust & credibility
- Feedback states (loading/empty/error/success)
- Accessibility
- Performance & responsiveness
- Visual design & typography
