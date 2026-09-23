# Release checklist

Copy this list into the release notes or tick it off for every release. Nothing in it publishes anything:
uploading to Steam is always a manual step (see [PUBLISHING.md](PUBLISHING.md)).

## Version and metadata
- [ ] `Mod/About/About.xml` `<modVersion>` bumped (semantic version: MAJOR.MINOR.PATCH)
- [ ] `<packageId>` is still `RimCore.DataCenters` (never change it)
- [ ] `<supportedVersions>` lists only versions that were tested
- [ ] Dependencies, `loadAfter`, `loadBefore`, `incompatibleWith` reviewed (currently: none required, loads after Core)
- [ ] `CHANGELOG.md` has an entry for this version, and `Workshop/changelog.txt` matches
- [ ] `Workshop/description.bbcode`, `short-description.txt` and `README.md` match the actual features and numbers
- [ ] `docs/BALANCE.md` matches `Defs/*.xml`

## Build and validation
- [ ] `Tools\Release.ps1` completes with "privacy and dependency scan clean"
- [ ] XML validation passed (`Tools\Validate-Xml.ps1`)
- [ ] The assembly version equals `modVersion` (the release script enforces this)
- [ ] Package contains exactly one DLL, no `.pdb`, no source, no logs or saves (the release script enforces this)
- [ ] `About/Preview.png` present, under 1 MB, shows the current content

## In-game verification of the exact release package
- [ ] `Tools\Install-ToGame.ps1 -Source .\Release\RimCoreDataCenters`
- [ ] `Tools\Test-InGame.ps1 -ExpectPackage .\Release\RimCoreDataCenters` prints "byte-identical" and `SELFTEST RESULT: N passed, 0 failed`
- [ ] `.testprofile\Player.log` has no red errors and no mod warnings (search for `RimCore`, `RCDC`, `Exception`)
- [ ] New game: research, build, operate (manual pass, at least once per release)
- [ ] Save, quit, reload with a running data center; values preserved
- [ ] Loaded into an existing save that had never seen the mod: no errors, buildings available after research
- [ ] Dependency check: mod loads with only Core enabled, and with Ideology enabled

## Content and legal review
- [ ] Every file in `Release/RimCoreDataCenters` reviewed (see `Release/*.sha256.txt`); no third-party assets
- [ ] `LICENSE.txt` and `NOTICE.md` are in the package
- [ ] No usernames, computer names, absolute paths, e-mail addresses or credentials anywhere (release script scans)
- [ ] Screenshots in `Workshop/Screenshots` are current and contain no other mods' content

## Publishing (manual, needs your Steam login)
- [ ] Steam Workshop page text and tags match `Workshop/`
- [ ] Visibility chosen deliberately (start Private/Friends-only; make Public after a final look)
- [ ] After the first upload: `About/PublishedFileId.txt` exists in the mod folder; keep it for every update
- [ ] After publishing: subscribe from a clean profile and confirm it loads and the self-test passes
