# Contributing

Contributions should preserve ARVREL's deterministic protection boundary, vendor-neutral design, public-source provenance and restrained engineering UX.

Before opening a pull request:

1. use synthetic or contributor-owned fixtures only;
2. do not submit customer, employer, station or proprietary captures and SCL files;
3. keep protection logic independent from UI refresh;
4. add deterministic tests for protection or trust-policy changes;
5. avoid manufacturer-specific parser selection or cloned vendor UI;
6. run `./scripts/build.ps1` on Windows with .NET 8;
7. describe safety and claim-boundary effects in the pull request.

By contributing, you certify that you have the right to submit the material under the repository license.
