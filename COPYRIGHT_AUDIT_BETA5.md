# Copyright and historical-release audit — Beta 5

- Audit date: 12 November 2026. Local source repository:
  `https://github.com/yakupalazio/steelseries-dock-customiser`
- Inspected local tags: `v1.1.1-beta.1`, `v1.1.1-beta.2`.
  Neither contains a root `LICENSE`, `LICENSE.md`, `TERMS` or `COPYING`
  file, although both contain `THIRD_PARTY_NOTICES.md` and a separate
  Apache-2.0 text and NOTICE for HidSharp.
- This finding does not prove older GitHub releases, binaries, branches,
  collaborator contributions or prior license grants were absent. Review
  official release assets and all merged third-party contributions before
  any public relicensing.
- Source code may already be downloadable from a public GitHub repository;
  adding an all-rights-reserved license cannot make existing forks/copies
  disappear or revoke rights validly licensed before.
- Scope this proposed license to the owner's original work only. The
  documented external `ggoled` executable is not bundled in source; verify
  binaries before distributing. `PixelOperator.ttf` and the user's own
  SteelSeries product photograph are ignored by Git; confirm redistribution
  rights before inclusion in any release package.
- BQC/SteelClock source code was inspected as a reference only; no GPL
  source was copied or incorporated into Beta 5. Windows BLE GATT/SetupAPI
  implementation is an independent project implementation.
- Proposed distribution policy: keep downloadable official binaries under
  the owner's terms, keep issue tracker/docs public, keep future source
  private if source reuse is a major concern; do not rewrite Git history.
- No GitHub push, historical tag rewrite, release edit, license grant from
  collaborators, or public publication has been performed.
