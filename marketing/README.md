# Marketing kit: NextMind Desktop

Presentation material for NextMind Desktop (draft, **not published**). Direction: premium technology: deep navy, graphite, a restrained blue accent (taken from the app's own palette), clean typography, plenty of space.

- **Name:** NextMind Desktop (no new logo: the product uses its name as a wordmark)
- **Headline:** Your desktop. Your rules.
- **Signature:** Free. Open source. Made for Windows.

## Authenticity rules
- Every screenshot in these graphics is a **real screenshot** from `docs/images/` (taken from the running app with synthetic sample data). They are placed and scaled, never redrawn or edited, and the originals are untouched.
- Backgrounds, typography and decoration are generated; no fictional interface, no features the product does not have.
- No real desktop, wallpaper, private files or personal data appear anywhere. Every graphic except the 1280 × 640 GitHub social preview (no room) carries a small "synthetic sample data" note; the screenshots themselves are documented in `docs/SCREENSHOT-PLAN.md`.
- Features that are new in the next release are marked **(v0.2)** in the copy; publish this kit together with that release.

## Graphics (`marketing/images/`)
| File | Size | Use | Headline | Alt text |
|---|---|---|---|---|
| `hero.png` | 1920 × 1080 | Product page, README banner, release page | Your desktop. Your rules. | NextMind Desktop: several zones with files, folders and shortcuts on a dark background; headline "Your desktop. Your rules." |
| `zones.png` | 1600 × 900 | Product page, feature section | Organize without limits. | An expanded zone and four collapsed zones; text about expanding, collapsing and zones staying where you put them |
| `search.png` | 1600 × 900 | Feature section | Find it. Instantly. | A zone with the search box open and the word "list" typed; two matching items remain; keys ↓ ↑ Enter Esc |
| `organization.png` | 1600 × 900 | Feature section | Make space for what matters. | Eight zones (four expanded, four collapsed) with drag & drop, sorting and collapsing noted |
| `social-linkedin.png` | 1200 × 627 | LinkedIn, X, Facebook, Mastodon | Meet NextMind Desktop. | Headline "Meet NextMind Desktop." with a "Free & Open Source" label above the zones |
| `social-instagram.png` | 1080 × 1350 | Instagram (4:5) | Meet NextMind Desktop. | A tall dark card with the headline, the label and two screenshots: zones and search |
| `github-social-preview.png` | 1280 × 640 | GitHub social preview (repository settings) | Your desktop. Your rules. | Headline and signature line next to the zones; the repository address |

All files are PNG without metadata, 250–340 KB each (about 2 MB in total), which is fine for GitHub (README images, releases) and for social upload.

## Copy (`marketing/copy/`)
| File | Content |
|---|---|
| [`messaging.md`](copy/messaging.md) | Taglines, one-liners, true feature bullets, what we do not claim |
| [`github-about.md`](copy/github-about.md) | Proposed repository description, topics and social preview |
| [`readme-showcase.md`](copy/readme-showcase.md) | Proposed README presentation section (not applied) |
| [`announcement-post.md`](copy/announcement-post.md) | First announcement: LinkedIn, short post, Instagram, forum note; pre-post checklist |

## Sources and regeneration
`marketing/src/` holds the HTML/CSS the graphics are rendered from (`style.css` and one page per graphic; every page is a fixed-size canvas).
To regenerate: serve the repository root on `localhost` (for example any static file server), open a page at its canvas size in a Chromium browser and capture the `.canvas` element.
The type is the Windows system font *Segoe UI Variable*, with fallbacks; text is baked into the PNGs, so the images look the same everywhere. Rendering the sources on another system may use a different fallback font.

## License
The graphics, copy and sources in this folder are provided under the repository's [Apache-2.0 license](../LICENSE). That license does not grant any trademark rights to the NextMind name (section 6).
