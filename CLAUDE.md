# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

- **`AppCompatCache`**: a `netstandard2.0` NuGet library (package id `AppCompatCache`) that parses the AppCompatCache (ShimCache) value of a SYSTEM hive, or a raw value. It opens hives with the Registry package; fixes to hive or transaction log handling belong in Registry, followed by a package bump here.
- **`AppCompatCacheParser`**: the command line tool (`net472;net10.0`) that writes it to CSV. Keep its columns and their order stable; new columns go at the end (`FromSlack` is the last).

Input may be corrupt or hostile.

## Commands

The SDK is pinned in `global.json` (.NET 10). The library uses `LangVersion 10`. Tests target `net10.0` and use NUnit 5 + FluentAssertions 7 (pinned to `[7.2.2,8.0.0)`, since 8.x requires a commercial license).

Debug info: `Directory.Build.props` sets `DebugType embedded` (the pdbs are inside the assemblies: stack traces and Exceptionless reports have file and line numbers, and no .pdb file ships) and, for Release, `ContinuousIntegrationBuild` (source paths in the pdbs are `/_/...`, not this machine's folders). Don't add `DebugType` or `DebugSymbols` to the project files.

```bash
dotnet build -c Release                                          # builds AppCompatCacheParser.slnx
dotnet test -c Release                                           # CI; CorpusTests is [Explicit] and skipped
dotnet test -c Release --filter "FullyQualifiedName~CorpusTests" # every SYSTEM hive in ../Corpus/Registry (or ACC_CORPUS)
dotnet test --filter "FullyQualifiedName~FormatTests"            # one fixture
dotnet pack -c Release                                           # only the library is packable
```

## Formats

The layouts are libyal's winreg-kb (`Application-compatibility-cache.md`); the format is chosen in `AppCompatCache.Init`. All reads go through `Bytes`, which checks bounds and throws `AppCompatCacheParseException`.
- **Windows XP** (`WindowsXP`, 0xDEADBEEF): 96 slots of 552 bytes after a 400 byte header. Offset 4 is the number of slots and 8 the number of entries in the LRU array (16 on), which lists the used slots most recently updated first. Entries are returned in that order (the old code returned all 96 slots, including empty ones). 64 bit XP uses the Windows 2003 format.
- **Windows 2003/Vista/2008** (`VistaWin2k3Win2k8`, 0xBADC0FFE) and **Windows 7/2008 R2** (`Windows7`, 0xBADC0FEE): a table of fixed size entries pointing to paths elsewhere in the value (`EntryTable`). The 32 and 64 bit layouts differ; the hive's PROCESSOR_ARCHITECTURE is used when the first entry makes sense with it, else the other layout (`Is32Bit` on the cache). Executed is insertion flag 0x2, except on 32 bit Vista/2003 (NA; Windows 2003 has the file size there).
- **Windows 8.0/8.1** (`Windows8x`, "00ts"/"10ts" entries after a 128 byte header) and **Windows 10/11** (`Windows10`, "10ts" entries after a 0x30, or 0x34 from the Creators Update on, byte header): each entry has its size, which is what gets to the next one. Windows 8 has package data and insertion flags (executed = 0x2).
- **Windows 10/11 data** is 4 byte aligned (tag, size, value) records (`Windows10.DataTags`). Executed means tag 0x100 (always value 1) is there. The old rule, "the last 4 bytes are 1" (issue #6), only matched when 0x100 was the last record: it missed 157 and wrongly flagged 102 of 9,316 entries in the corpus. Tags 0x800 and 0x2000 hold the PE machine type (0x8664, 0x14C).
- Packaged (UWP) app entries have a tab separated record (flags, versions, architecture, package name, publisher) as their path and no time. They are kept as they are.
- **Slack** (`Slack.Recover`): entries left in the slack of the value's cell, for the signature formats (Windows 8 to 11). Only the ones not in the cache (by path and time) are kept as `SlackEntries`: in the corpus, every one found was an older copy of a live entry.

## Hives

`AppCompatCache(string, controlSet, noLogs)`: a hive that is in use is read raw (Windows, administrator), with its logs. A dirty hive gets its logs (SYSTEM.LOG, .LOG1, .LOG2 next to it, in any case) replayed, or throws ("Sequence numbers do not match...") unless `noLogs`. Control sets are the root's `ControlSetNNN` keys that have the AppCompatCache (or, on XP, AppCompatibility) key; one without the value is skipped. Deleted keys are not recovered (not needed, and faster).

## Error model

- `AppCompatCacheParseException` (`Offset`, `Field`): the value is too small or not a known format ("Unable to determine operating system!...").
- A damaged entry goes to the cache's `ParseErrors`. The variable size formats stop there (the entries before it are kept); the fixed size ones (XP, Vista, 7) go on with the next entry. AppCompatCacheParser writes what was read, lists the damaged entry and exits with 0. Exit codes (Eric, 2026-10-22, all CLI tools): 1 only when the tool fails (bad arguments or a -c ControlSet that is not there, a missing -f, a hive it can't access or read, the live Registry, a dirty hive without its logs, a CSV that can't be written); a damaged hive or cache and no cache exit 0.
- AppCompatCacheParser returns 1 for bad arguments, a hive that can't be read, no cache, or a CSV that can't be written, and 0 otherwise. No CSV is written when there is no cache.

## Tests

- `FormatTests` (with `CacheBuilder`, which builds values in each format): executed tags, entries with little data, truncation, layout detection, XP LRU order, bad input, random damage, slack. Add regression tests here.
- `SampleTests`: the raw values in `TestFiles/*.bin` (also in the DFIR Artifact Museum).
- `HiveTests`: the Museum's dirty Windows 11 SYSTEM hive with its logs (gzipped in `TestFiles/DFIRArtifactMuseum`, MIT; see the README there), through the library and AppCompatCacheParser.dll (exit codes, CSV, `-t`).
- `CorpusTests`: every SYSTEM hive (file type 0, so not the logs) in `Corpus/Registry` next to the repo, or `ACC_CORPUS`. Never copy those files into the repo. `!!!BrokenOrToVerify/SYSTEM_BROKEN_Blanco` (opened since Registry 2026.10.0) has " END OF IMG!PAST" filler as its value, which is reported as an unknown format.
- Validated against regipy's ShimCacheParser (Mandiant's) over 36 SYSTEM hives (29 corpus, 7 Museum): every path and time agrees, apart from packaged app entries (it skips them) and XP order (it uses slot order). It can't read the original Windows 10 header or Windows 8.0.

## Versioning and release

- The library's version comes from the git tag through MinVer (bare tags like `2026.10.0`). There is no `<Version>` in its csproj.
- AppCompatCacheParser has `<Version>` in its csproj, bumped by hand. It is not packed.
- To release the library: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`.
- `.github/workflows/publish.yml` is identical to GuidMapping's. It packs, validates and tests on PRs and on pushes to `master`. When a GitHub Release is published, it pushes to NuGet.org with trusted publishing (`NuGet/login` with the `NUGET_USER` repo variable, `EricRZimmerman`).
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
- Release builds are staged, signed and zipped by `Build-ZimmermanTools.ps1` in the get-zimmermantools repository (cloned next to this one). Its program table says what goes in this tool's download zip (the files taken from the build, folders copied from the repositories, files kept from the current release zip); a new output file, a content folder or a change in how the tool is built (single file, another framework) needs that entry updated.
