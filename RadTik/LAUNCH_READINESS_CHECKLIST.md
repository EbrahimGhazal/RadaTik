# RadTik Launch Readiness Checklist

## 1) UI Consistency
- [ ] Verify card spacing consistency on core pages (System Admin, Company Admin, Employee, Client Portal)
- [ ] Verify dropdown style consistency (header user menu, notifications, form selects, Select2)
- [ ] Verify table density and readability in light/dark modes
- [ ] Verify alert and notification visual consistency

## 2) Accessibility (Light + Dark)
- [ ] Check text contrast for page titles, table headers, secondary text, and badges
- [ ] Check keyboard navigation for links/buttons/forms (`Tab`, `Shift+Tab`, `Enter`, `Space`)
- [ ] Check visible focus state for interactive elements
- [ ] Check color-only status indicators (use icon/label with color where possible)

## 3) Responsive Behavior
- [ ] Desktop: tables remain tabular and aligned
- [ ] Tablet/Mobile: tables render as cards with correct field labels
- [ ] Verify no horizontal overflow in key pages
- [ ] Verify sidebar/header behavior at breakpoints (`1024px`, `768px`, `576px`)

## 4) Critical Flow Smoke Test
- [ ] Login / logout
- [ ] Open notifications dropdown and navigate to target page
- [ ] Create / edit forms using Select2 inputs
- [ ] DataTables: filter, paginate, sort
- [ ] Open modal forms and submit/cancel

## 5) Pre-Release Technical Checks
- [ ] No console errors on main dashboards
- [ ] No missing CSS/JS assets in network tab
- [ ] Validate dark mode toggle persists between refreshes
- [ ] Validate unread counters update and render correctly

## 6) Final Sign-Off
- [ ] Product owner visual approval
- [ ] QA approval on desktop + tablet + mobile
- [ ] Release notes prepared
- [ ] Production deployment window confirmed

