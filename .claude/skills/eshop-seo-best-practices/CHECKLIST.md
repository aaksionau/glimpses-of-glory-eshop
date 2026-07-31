# E-shop SEO checklist

Each item: the practice, and why it matters. Use as an audit checklist, not a spec to blindly implement.

## Crawlability & indexation

- `robots.txt` exists, allows product/category/home pages, and disallows cart, checkout, account, and admin paths.
- An XML sitemap lists all indexable product and category URLs, is kept in sync as products are added/removed, and is referenced from `robots.txt`.
- Cart, checkout, login, and account pages carry `<meta name="robots" content="noindex">` (or an equivalent header) — they're user-flow pages, not search results.
- Out-of-stock or discontinued products stay indexable (with clear "out of stock" state) rather than 404ing or noindexing, unless permanently removed — a permanently removed product should 301 to its category or a close replacement, not dead-end in a 404/410.
- No accidental blanket `noindex` or `Disallow: /` left over from staging/development config.

## URL structure & site architecture

- URLs are human-readable and stable: `/products/product-slug`, not `/products?id=1234` or a slug that changes when the name is edited.
- Category/listing URLs reflect the site's hierarchy (`/products/category-slug`) so breadcrumbs and URLs agree.
- Every product is reachable within a small number of clicks from the homepage via category navigation — no orphan pages reachable only via direct link or search.
- Slugs are lowercase, hyphenated, and free of session IDs, tracking parameters, or internal database keys.
- Redirects (301) are used when a product/category URL changes, so existing inbound links and search-engine index entries aren't lost.

## On-page elements

- Every indexable page has a unique `<title>` — product pages include product name (and ideally brand/category), not a generic "Products - Site Name" repeated everywhere.
- Every indexable page has a unique, accurate `<meta name="description">` summarizing that specific page's content — not a copy-pasted template or the site's tagline on every product.
- Exactly one `<h1>` per page, matching the page's actual subject (product name on PDP, category name on PLP).
- Heading hierarchy is logical (`h1` → `h2` → `h3`, no skipped levels used purely for styling).
- Title/meta description length is reasonable for search-result display (title ~50–60 chars, description ~150–160 chars) so they aren't truncated mid-thought.

## Structured data (schema.org)

- Product pages include `Product` JSON-LD with at minimum: `name`, `image`, `description`, `offers` (`price`, `priceCurrency`, `availability`), matching what's visibly rendered on the page.
- If reviews/ratings exist, `aggregateRating` and/or `review` are included in the `Product` schema — mismatched or fabricated ratings violate search-engine guidelines and can trigger manual penalties.
- Category/product pages include `BreadcrumbList` schema matching the visible breadcrumb trail.
- The homepage includes `Organization` and/or `WebSite` schema (enables sitelinks search box and knowledge panel eligibility).
- Structured data is validated (e.g., against Google's Rich Results Test) after any template change — silent schema breakage is easy to introduce and easy to miss visually.

## Images & alt text

- Every product image has descriptive `alt` text (product name, and variant/angle if multiple images) — this feeds both accessibility and image search.
- Image filenames are descriptive where practical, not opaque hashes/GUIDs, since some search engines use filename as a weak signal.
- Images are served at appropriately compressed sizes for their display context — large unoptimized images hurt Core Web Vitals (LCP), which is a ranking factor.
- Product images are included in an image sitemap or the main sitemap's image extension, if the platform supports it.

## Duplicate content & canonicalization

- Every indexable page has a self-referential `<link rel="canonical">` pointing to its preferred URL.
- Filter/sort/pagination query parameters (`?sort=price&color=red`) either canonicalize back to the base listing URL or are deliberately excluded from indexation — they must not produce dozens of near-duplicate indexable URLs for the same product set.
- The site is reachable at exactly one canonical host/scheme (e.g., `https://example.com`, not both `www` and non-`www`, or both `http` and `https`) with redirects enforcing the rest.
- If the same product appears under multiple categories, one canonical URL is designated rather than indexing multiple duplicate paths for it.

## Content quality

- Product descriptions are original, not copy-pasted manufacturer boilerplate duplicated across every retailer selling the same item — thin/duplicate content ranks poorly.
- Category pages have enough unique on-page content (intro text, curated grouping) to be distinguishable from one another, not just an interchangeable product grid.
- Content answers real buying questions (materials, sizing, compatibility) rather than being purely promotional copy, which also improves ranking for long-tail, intent-driven queries.

## Performance & Core Web Vitals

- Core content (product name, price, description, availability) is present in the server-rendered HTML response, not injected only by client-side JavaScript after load — crawlers must not need to execute JS to see it.
- Largest Contentful Paint (LCP) element (usually the main product image) loads quickly — appropriately sized/compressed images, no render-blocking resources ahead of it.
- Cumulative Layout Shift (CLS) is minimized — images and dynamic content (e.g., htmx-swapped fragments) reserve their layout space instead of shifting content around them.
- Interaction to Next Paint (INP) stays low — avoid heavy blocking JS on pages with frequent user interaction (cart quantity changes, filters).

## Social/sharing metadata

- Open Graph tags (`og:title`, `og:description`, `og:image`, `og:type=product`) are present on product pages so shared links render correctly with a relevant image and price.
- Twitter Card tags are present if the audience shares on that platform.
- The `og:image` is a real, appropriately sized product photo — not a placeholder or the site logo on every page.

## Monitoring

- The site is verified in a search console tool (e.g., Google Search Console) so indexing errors, manual actions, and Core Web Vitals field data are visible.
- 404s and redirect chains are monitored periodically — broken internal links (e.g., a discontinued product still linked from a category page) waste crawl budget and hurt UX.
