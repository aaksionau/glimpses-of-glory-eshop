# E-shop UX checklist

Each item: the practice, and why it matters. Use as an audit checklist, not a spec to blindly implement.

## Navigation & search

- Persistent header with logo (→ home), category/product nav, and cart access on every page.
- Cart icon/link shows item count or a visible indicator when non-empty — users shouldn't have to open the cart to know it has items.
- Breadcrumbs on product listing and detail pages when categories exist, so users can navigate up without the back button.
- Search (if present) tolerates typos/partial matches and shows "no results" with a recovery action, not a blank page.
- Active nav state is visible (current page/section highlighted).

## Product listing (PLP)

- Each card shows: image, name, price, and enough info to decide whether to click in (no "click to find out the price").
- Out-of-stock items are marked as such in the grid, not just discovered on the detail page.
- Grid is responsive: 1 column on mobile, scaling up on wider viewports; images maintain aspect ratio (no squish/crop surprises).
- Empty state ("no products available") has explanatory text, not just a blank area.
- If filters/sort exist, the active filter/sort state is visible and clearable.

## Product detail (PDP)

- Primary image is large and clear; multiple photos are supported with an obvious way to browse them (thumbnails, arrows, or swipe).
- Zoom or lightbox for images is expected for anything where texture/detail matters.
- Price, stock status, and Add-to-cart are above the fold — no scrolling required to make the core decision.
- Quantity selector defaults to 1, has visible min/max bounds tied to actual stock, not a silent server-side clamp.
- Add-to-cart gives immediate feedback (redirect to cart, toast, or updated cart indicator) — a silent POST leaves the user unsure it worked.
- Out-of-stock state disables/replaces the CTA rather than allowing a submit that fails server-side.
- Description supports the buying decision (specs, materials, sizing) — not just marketing copy.

## Cart

- Every line shows: image, name, unit price, quantity (editable inline), and line total.
- Quantity changes and removals give immediate visual feedback and don't require a full page reload.
- Subtotal, shipping, and total are all shown before checkout — shipping cost must not first appear at the final step.
- Empty cart state has a clear CTA back to shopping, not a dead end.
- Removing an item doesn't require confirmation friction for a low-stakes, reversible action (undo is better than "are you sure?").

## Checkout

- Guest checkout is available — mandatory account creation is one of the biggest documented drop-off causes.
- Number of steps/fields is minimal; group related fields (shipping vs. payment) and show progress if multi-step.
- Costs shown in cart carry through unchanged — no new fees introduced at the last step.
- Inline validation on blur/submit with specific error messages next to the offending field, not a generic banner at the top.
- Form fields use correct `type`/`autocomplete` attributes so browsers/password managers can autofill (email, name, address, cc-number, etc).
- Order confirmation clearly states what happens next (email confirmation, order number, estimated delivery).

## Trust & credibility

- Return/refund policy and shipping timeframes are discoverable from product and cart pages, not buried.
- Contact information or support channel is reachable within a click or two from anywhere.
- If reviews/ratings exist, they're visible on both PLP and PDP, not just PDP.
- Pricing is transparent — no asterisks resolved only at checkout.

## Feedback states

- Every async action (add to cart, update quantity, remove, submit order) has a visible pending/loading indicator if it can take >~200ms.
- Empty states (empty cart, no products, no search results) are designed, not blank.
- Error states (failed submit, out of stock at checkout, network error) tell the user what happened and what to do next.
- Success states (added to cart, order placed) are confirmed visibly, not just inferred from a page navigation.

## Accessibility

- All images have meaningful `alt` text (product name at minimum); decorative images use empty `alt=""`.
- Every form input has an associated `<label>` (visually hidden via sr-only is fine for icon-only or self-evident fields like a quantity stepper next to a product name).
- Interactive elements are reachable and operable via keyboard alone (Tab/Enter/Space); custom widgets (lightboxes, dropdowns) trap/restore focus correctly and close on Escape.
- Color contrast meets WCAG AA (4.5:1 for body text) — check any gray-on-white or colored-link text.
- Focus states are visible (don't rely on `outline: none` without a replacement).
- Buttons vs. links are used semantically (`<button>` for actions/state changes, `<a>` for navigation).

## Performance & responsiveness

- Images are appropriately sized/compressed for their display size — no full-resolution originals served into a 200px thumbnail.
- Touch targets are at least ~44×44px on mobile (quantity inputs, remove links, nav items).
- Layout doesn't shift as images/content load (reserve space via aspect-ratio or explicit dimensions).
- Critical CSS/JS is minimal; avoid blocking render on non-essential scripts.

## Visual design & typography

- Consistent spacing scale and type scale across pages (don't hand-tune one-off pixel values per page).
- Clear visual hierarchy: page title > section headers > body text > metadata, distinguishable by size/weight, not color alone.
- Primary CTA has one consistent visual treatment across the whole site (same "Add to cart" look everywhere).
- Sufficient whitespace around interactive elements so adjacent controls (e.g., quantity input and remove button) aren't misclicked.
