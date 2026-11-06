# V3 release implementation plan

1. Verify the full pre-V3 backup and record its manifest.
2. Add failing tests for version, portable path isolation/autostart exclusion, production marker hygiene and schema-3 data preservation.
3. Implement version 3.0.0, portable path resolution and conditional test marker.
4. Add guarded install, rollback, uninstall and release-packaging scripts with isolated test parameters.
5. Test migration on a copy of the real config and compare semantic snapshots.
6. Run all tests, Release build, package construction and content audit.
7. Exercise installer and rollback against an isolated fake installation; do not touch the real installation.
8. Compute SHA-256, write release documentation and test report.
9. Ask the owner to close NextMind Desktop normally. Only then run the verified installer against the real per-user installation and validate data/autostart/startup.
