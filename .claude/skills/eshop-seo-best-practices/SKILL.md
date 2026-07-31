---
name: eshop-seo-best-practices
description: Best-practice checklist for e-commerce SEO — crawlability, URL structure, on-page metadata, structured data, duplicate content, and Core Web Vitals. Use when adding new storefront pages/routes, or when asked to evaluate/audit this project's SEO or search visibility.
---

# E-shop SEO best practices

Stack-agnostic heuristics for evaluating or designing a storefront's search-engine visibility. Grounded in established e-commerce SEO conventions (Google Search Central guidelines, schema.org), not this project's specific tech.

## Core principles

1. **Every indexable page needs unique, accurate metadata** — title and description that describe *that* page, not a copy-pasted template. Duplicate/boilerplate metadata across products is one of the most common e-commerce SEO failures.
2. **Crawlers should only find what should rank** — cart, checkout, account, and admin pages must be excluded from indexing; product and category pages must be reachable and indexable.
3. **One canonical URL per piece of content** — filters, sort params, and session/tracking query strings must not create duplicate indexable URLs for the same product/listing.
4. **Structured data describes what's already on the page** — schema.org markup (Product, BreadcrumbList) should mirror visible price/availability/name, never contradict or invent data.
5. **Server-rendered content is crawlable content** — anything a crawler needs to see (price, name, description, availability) must be present in the initial HTML response, not injected client-side only after JS runs.
6. **Site architecture should be shallow** — any product should be reachable within a few clicks from the homepage via category navigation, with URLs that reflect that hierarchy.
7. **Performance is a ranking factor** — Core Web Vitals (LCP, CLS, INP) affect both ranking and conversion; the same fixes serve both.

## Workflow: evaluating a storefront's SEO

When asked to review/audit/evaluate an eshop's SEO:

1. Identify the page types and their intended indexability (home, category/listing, product detail = should index; cart, checkout, admin, login = should not).
2. Read [CHECKLIST.md](CHECKLIST.md) and walk each page type against its relevant section — check actual rendered HTML (`<title>`, `<meta>`, headers, JSON-LD), not just the Razor/template source, since server-side data binding can silently produce empty or duplicate values.
3. For each finding, note: **what's missing/wrong → why it matters for crawling/ranking → concrete fix**. Point to the actual page/element, not a generic "add SEO" note.
4. Rank findings by visibility impact: missing/duplicate titles and unindexed product pages outrank cosmetic metadata tweaks.
5. Distinguish "blocks crawling or ranking" (e.g., product pages return content only via client-side JS, no canonical tag, cart page is indexable) from "incremental improvement" (e.g., could add FAQ schema) — don't flag both at the same severity.

## Quick reference

See [CHECKLIST.md](CHECKLIST.md) for the full checklist, organized by:
- Crawlability & indexation (robots.txt, sitemap, noindex)
- URL structure & site architecture
- On-page elements (titles, meta descriptions, headings)
- Structured data (schema.org)
- Images & alt text
- Duplicate content & canonicalization
- Content quality
- Performance & Core Web Vitals
- Social/sharing metadata
- Monitoring
