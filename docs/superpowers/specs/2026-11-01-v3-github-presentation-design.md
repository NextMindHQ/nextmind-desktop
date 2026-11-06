# NextMind Desktop V3 GitHub presentation design

## Intent

Present NextMind Desktop V3 as a polished Windows product while preserving the public repository's established identity and its truthful, safety-first positioning. The presentation must lead with the real V3 Future Workspace, use only synthetic demonstration data, and remain suitable for later integration on top of public `main` commit `22eef1686888cf0e0df2290931fa8289c663616c`.

## Goals

- Keep the public tagline **“Your desktop. Your rules.”**, existing NextMind branding, useful badges, safety language, and good public documentation.
- Replace V2-era product presentation with an English V3 README covering the Future Workspace, gallery, installation, updates, Portable edition, requirements, privacy/safety, FAQ, development, and GitHub Releases.
- Capture authentic V3 screens for the desktop workspace, Control Center, zones/widgets, and appearance personalization.
- Produce a 1280×640 Social Preview composed from authentic V3 UI rather than invented screens.
- Update the V3 changelog and release notes without publishing them.
- Produce an audit report and a non-destructive reconciliation plan for the divergent Git histories.

## Non-goals and anti-goals

- No merge, push, tag, force-push, GitHub Release, repository-setting change, or license change.
- No modification, stop, restart, inspection, or reuse of the user's installed V3 process or live `%LOCALAPPDATA%\NextMind\Desktop` data.
- No private names, paths, files, notes, configuration, logs, backups, or production data in screenshots or tracked assets.
- No AI-generated or composited fake product controls. Decorative framing may be added around a genuine capture, but the application UI itself remains unaltered.
- No replacement of public material that is still accurate and useful merely for novelty.

## Approaches considered

1. **Product-first:** plain authentic captures with minimal visual framing. Most literal, but undersells the authored Future Workspace design.
2. **Brand-first:** campaign-style artwork with the product as a secondary element. Strong visual impact, but risks looking less authentic.
3. **Hybrid (selected):** authentic product UI is the main subject, framed with the existing brand language and restrained Future Workspace lighting. This preserves trust while giving the repository a polished product-page feel.

## Source-of-truth strategy

- Public GitHub `main` at `22eef168` is the reference for current branding, Apache-2.0 licensing, public documentation, CI conventions, and existing marketing assets.
- Local V3 commit `9ca1b63` is the reference for V3 behavior, UI, packaging scripts, and release documentation.
- Presentation work is prepared locally for review. It must not imply that the local branch can replace public `main` wholesale.
- Assets retained from public `main` must keep their original paths where practical so future integration produces a focused diff.

## Screenshot environment

- Build V3 from source in Release configuration to a repository-local artifact directory.
- Launch only an isolated test instance with `NEXTMIND_DESKTOP_CONFIG_DIR` pointing to a fresh temporary directory and the existing V3 test-instance marker/mutex enabled.
- Populate the isolated config with clearly synthetic English demo content such as “Focus”, “Projects”, “Inbox”, “Design brief”, and “Launch checklist”. Any referenced demo files live in a fresh temporary sandbox created solely for the capture.
- Keep autostart disabled and managed moves disabled. Never point the demo at Desktop, Documents, OneDrive, or the installed application's data root.
- Before capture, inspect every visible string and window area. After capture, inspect the resulting pixels and metadata for names, paths, notifications, account details, taskbar content, and other identifiers.
- Capture application windows or a controlled synthetic wallpaper area rather than the user's real desktop. Close the isolated process normally after capture; leave the installed process untouched.

## Visual deliverables

- `marketing/images/hero-v3.png`: wide hero featuring the real Future Workspace with zones and widgets.
- `marketing/images/control-center-v3.png`: Control Center overview.
- `marketing/images/workspace-v3.png`: desktop zones and widgets with synthetic content.
- `marketing/images/personalization-v3.png`: appearance controls and visible live-preview result.
- `marketing/images/social-preview-v3.png`: exactly 1280×640, tagline plus authentic V3 UI crop and restrained brand framing.

PNG is preferred for UI legibility. Captures should remain readable on GitHub at common README widths, avoid unnecessary taskbar/browser chrome, and use meaningful alt text in README.

## README structure

1. Centered hero, product name, tagline, status badges, release link, and local-only trust statement.
2. Concise explanation of why the product exists.
3. V3 Future Workspace highlights: zones, workspace profiles, local command search, widgets, assisted layout, personalization/resource saver, and local-only safety model.
4. Authentic gallery using the four approved product images.
5. Installation and in-place update instructions, including SmartScreen/code-signing status if still applicable.
6. Portable edition behavior and its isolated `%LOCALAPPDATA%\NextMind\Desktop-Portable` data root.
7. Data safety, managed-move warning, requirements, FAQ, development, license, and Releases/Issues links.

Claims must match implemented and verified behavior. The README must distinguish installer and Portable packaging accurately and must not call the Setup ZIP an MSI or signed installer.

## Release documentation

- Expand `docs/CHANGELOG-V3.md` into a user-facing V3 change summary grouped by workspace, appearance, safety/data compatibility, packaging, and known limitations.
- Expand `docs/RELEASE-NOTES-V3.md` into draft GitHub Release notes with installation/update/Portable guidance and placeholders for final artifact hashes.
- Keep Apache-2.0 unchanged. Add a concise dependency/asset attribution assessment to the audit report; introduce `NOTICE` or third-party notices only if evidence shows they are required.

## Verification and review

- Render README locally or through a browser-compatible Markdown preview and provide screenshots or a directly viewable preview artifact.
- Verify every referenced local image exists and its dimensions are correct.
- Run repository tests and the Release build without touching the installed app.
- Audit tracked files and candidate release archives for secrets, private paths/data, backups, logs, configs, test-only markers, and unexpected binaries.
- Inspect the two release ZIPs and record their SHA-256 values, but do not publish them.
- Report any claim that remains manual or unverified rather than presenting it as complete.

## Safe Git reconciliation plan

1. Add a real GitHub remote without replacing or deleting the current local-path remote; fetch public `main` and verify it resolves to `22eef168` or a newer explicitly reviewed commit.
2. Create a new integration branch from the fetched public `main`, never from the older local `main`.
3. Reapply V2/V3 work as reviewed commits or logically grouped patches onto that branch. Preserve public README, Apache license, CI, marketing, and documentation changes unless a V3 change intentionally supersedes them.
4. Resolve conflicts file by file, with special scrutiny for README, packaging, version metadata, configuration schema, installer behavior, and safety documentation.
5. Remove or sanitize current-tree private paths before any push. Separately report historical privacy findings; do not rewrite public history without a new explicit decision.
6. Run the full test/build/package/privacy audit on the integration branch, then compare it with both public `main` and `9ca1b63` to prove that public features and V3 functionality are retained.
7. Present the integration diff and evidence for approval before any push, merge, tag, or release.

## Builder decisions still allowed

- Exact crop, window arrangement, and restrained decorative framing may be adjusted for legibility.
- Existing public hero/gallery assets may be retained where they remain accurate, but V3-specific hero and gallery images take priority when showing current functionality.
- Synthetic demo labels may be refined as long as they remain generic, English, and clearly non-private.
