# B2S Project Working Rules

These rules apply to every task in this workspace.

1. Do not guess or apply speculative fixes.
2. Before changing anything, trace the complete relevant path from start to finish. For animation work, this normally means:
   - Designer/editor input and UI state
   - In-memory model values
   - Saved directB2S XML attributes
   - Server XML loading and mapping
   - ROM/lamp/solenoid trigger handling
   - Runtime animation and stop behavior
3. Establish the exact failure with concrete evidence before editing code or files.
4. Explain what was proven, what will change, and why before making a material change.
5. Change only the proven cause. Do not alter unrelated settings or behavior.
6. Build and verify every code change before installation.
7. Back up every installed or user test file before replacing or modifying it.
8. Do not access or modify the Windows registry.
9. Do not modify ScreenRes files, display configuration, or unrelated system settings.
10. Preserve backwards compatibility for legacy backglasses unless the user explicitly approves a compatibility change.
11. Validate both the new one-image format and applicable legacy behavior after changes.
12. If the evidence is incomplete, continue tracing or ask the user for the missing test result; do not fill gaps with assumptions.
13. The B2S Git repository is public-release-only. Track only source code, public assets, public documentation, licenses, and build or release automation that must be included in or used for the public release.
14. Never place tests, diagnostics, traces, logs, experiments, prototypes, recovery files, backups, quarantines, temporary scripts, build staging, generated build output, installer staging, or private tester working files anywhere inside the Git repository. A `.gitignore` entry is not permission to store these items inside the repository.
15. Put every local-only working item in one dedicated folder outside the Git repository: `B2S-Local-Work` in the workspace directory alongside this repository. Create organized subfolders there when needed; do not create additional temporary working roots.
16. Keep only the `main` branch unless the user explicitly authorizes another branch. Delete temporary local branches after integration, and never push them to GitHub.
17. The GitHub repository is public, as explicitly confirmed by the user on September 20, 2026. Keep it public so installed applications can access releases and updates. Do not change repository visibility without an explicit user instruction.
18. Before every commit, push, tag update, or release upload, audit the repository and every package to prove that no local-only material is tracked, stored in the repository tree, or included in an archive.
19. Every GitHub release must contain exactly six uploaded assets and no others: `B2SProSetup.exe`, `B2SServerSetup.exe`, the versioned B2S Pro Designer runtime ZIP and its `.sha256` sidecar, and the versioned B2S Pro Server runtime ZIP and its `.sha256` sidecar. The complete setup must use those same Designer and Server ZIPs; do not publish complete-build bundles, custom source archives, standalone testers, or aggregate checksum lists.

## Standing programming and verification rules

The project owner adopted these standing programming rules on September 20, 2026. These requirements supplement the rules above and apply to every future task in this workspace.

20. Trace project/autosave data and final behavior inside VPX as part of the complete animation path, in addition to editor input, in-memory values, exported DirectB2S XML, server loading, triggers, timing, rendering, and stopping behavior.
21. Establish the exact failing line or saved value using concrete evidence. Inspect relevant saved values, code paths, runtime behavior, and recordings when applicable. Do not treat symptoms as causes.
22. Before every material change, explain what was checked, what was proven, what needs to change, and why that specific change is necessary.
23. Test one variable at a time. Keep known-good settings unchanged while testing a direction, speed, trigger, stop behavior, or other feature.
24. After every editor test, inspect the actual DirectB2S XML and confirm the intended values before running VPX.
25. Distinguish recovery projects/autosaves from DirectB2S exports. Auto Save does not necessarily update the DirectB2S file VPX loads; use Step 1 – Create DirectB2S File for the actual export.
26. Every code build must complete with zero errors before installation. Compare installed-file hashes with the tested build to verify the installed DLL and EXE.
27. Back up every installed file, test DirectB2S file, and other user file before replacing or materially modifying it.
28. Keep the animation formats separate: legacy files remain on their original animation system; new one-image animations use native rotation. Never automatically convert legacy files. An intentional compatibility change requires explicit user approval.
29. After relevant changes, test both the new one-image behavior and an untouched known-good legacy backglass. Clearly distinguish component checks from full backglass/VPX validation; do not claim the latter passed when it remains untested.
30. After a test passes, establish the next test's purpose before making additional changes.
31. Report what passed, what failed, what remains untested, and what the next isolated test will prove. If evidence is incomplete, continue tracing or ask for the missing test result; never substitute assumptions.
32. Stop immediately when requested. Do not continue editing, building, installing, or changing files after the user says to stop.
