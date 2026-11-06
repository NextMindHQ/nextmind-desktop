# NextMind Desktop V3 — GitHub Presentation Audit

Date: 2026-11-05. Scope: local presentation branch only. No merge, push, tag or GitHub Release was performed.

## Result summary

- Five V3 PNG assets were created from an isolated synthetic profile; no installed configuration was used.
- README, changelog and draft release notes now describe V3 and both package types.
- Apache-2.0 remains unchanged.
- A stale production subtitle (`Live Preview 2`) was removed and guarded by a release test.
- The current Setup and Portable archives are valid audit candidates, but must be rebuilt from the eventual integrated commit before publication.

## Authenticity and privacy

The demonstration used a temporary configuration root, a synthetic desktop root, managed moves disabled and autostart disabled. Names shown in product UI are generic demonstration labels (`Deep Work`, `Planning`, `Personal`, `Focus`, `Projects`, `Inbox`, `Quick Note`). Two Control Center views are direct captures of the isolated V3 process. Workspace components were rendered from the real V3 WPF zone/widget classes; the branded hero and social preview only frame those authentic UI pixels.

Rejected captures that contained another application's pixels were not saved or used. Final PNGs contain no text metadata chunks and no personal username or filesystem path.

## Functional comparison: public main vs local V3

| Capability | Public `main` (`22eef168`) | Local V3 lineage | Integration requirement |
|---|---|---|---|
| Base zones, references, guarded moves | Yes | Yes | Preserve tests and safety invariants |
| Search inside each zone | Yes | Not present in the V3 lineage | Port public search UI/model and tests |
| Free-slot placement and edge snapping | Yes | V2 assisted layout has related but different logic | Reconcile geometry behavior explicitly |
| Smart expand direction | Yes | Not equivalent | Preserve public algorithm and regression tests |
| Obstruction-aware menus and dismissal | Yes | Not equivalent | Port shell/menu helpers and tests |
| Workspace profiles | No | Yes | Integrate V3 schema and UI |
| Global local command search | No | Yes | Keep alongside per-zone search |
| Clock and Quick Note widgets | No | Yes | Integrate V3 widget persistence/UI |
| V3 theme and live personalization | No | Yes | Integrate design resources and schema V5 |
| Setup backup/rollback and Portable | No | Yes | Rebuild packages from final commit |
| Public CI, community docs and marketing | Yes | Partial/older | Start from public main versions |

V3 is therefore not a safe drop-in replacement for public main. Both lines contain unique features that must be reconciled and tested together.

## Git history finding and safe integration plan

`github/main` and the local V3 branch have no merge base. A direct merge with unrelated histories, rebasing public main onto V3, or replacing public main would risk losing public work and must not be used.

1. Create a new integration branch from the exact current public `github/main`.
2. Preserve its Apache-2.0 license, CI, community files, v0.2 functionality and tests as the baseline.
3. Import V2/V3 work in reviewed logical groups: configuration/schema; profiles; widgets; global search; theme/chrome; packaging; then presentation/docs.
4. Resolve overlapping zone geometry, window, menu and search code manually. Keep both per-zone search and V3 global search.
5. Add combined regression tests for public placement/menu behavior and V3 profile/widget/personalization behavior before each group is accepted.
6. Run the complete suite, Release build, isolated UI checks, Setup migration test and Portable isolation test on the integrated branch.
7. Build fresh archives from that exact commit, scan archive listings and content, calculate final SHA-256 values, then request publication approval.
8. Merge normally into public main without force-push. Tag only the verified main commit.

## License and dependency audit

The repository license is Apache-2.0 and was not changed. Production projects use project references and the Windows/.NET platform; test-only packages are Microsoft.NET.Test.Sdk, xUnit and its runner. The reviewed graphics are produced from original NextMind UI and repository branding. No third-party source, logo, stock image or GPL-licensed code was added.

Retaining Apache-2.0 avoids a relicensing event and remains compatible with the current contribution and distribution model. The self-contained Portable build includes Microsoft .NET runtime files under their applicable Microsoft distribution terms; this does not change the license of NextMind's own source. This is a repository audit, not legal advice.

## Publication gate

Do not publish the candidate archive hashes as final. After history reconciliation, rerun every control against packages produced from the final public-main commit and confirm green public CI, downloadable assets, tag/commit identity and final hashes.

## Verification evidence

- Release build: PASS using a single MSBuild node, 0 warnings and 0 errors. Parallel MSBuild in the restricted session lost its compiler-server connection; the serialized build completed normally.
- Automated tests: PASS, 333/333, 0 skipped.
- Release-package hygiene script: PASS for manifest, production labels, version and Portable marker.
- Isolated Setup execution: PASS; application payload installed, old payload and complete data backup recorded, and synthetic config/managed item/log hashes remained unchanged.
- Portable archive: PASS for executable, marker, self-contained runtime, absence of configuration/log files, and distinct data-root behavior covered by `AppPathsTests`.
- Candidate archive hashes (not publication hashes): Setup `EE7AF4212647EAD8C6DBCAD6918327FBCCFE6E5B12D3C33CF9F90D335151B1E4`; Portable `87313FB1827C105E25E49078BB0DC415E522C55BF8FD1767D8A3395BCA2A3D20`.
- README preview: inspected at 1440 px and 390 px. Images remain legible, the mobile layout has no horizontal overflow, and local media paths resolve.
