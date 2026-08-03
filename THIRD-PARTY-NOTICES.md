# Third-party notices

ARVREL is distributed under GPL-3.0-or-later, with an optional separately negotiated commercial license for ARVREL-owned code. Third-party components remain governed by their own licenses.

This notice is informational and does not replace the complete license text or package metadata supplied by each dependency.

## Major runtime and build dependencies

- **Microsoft .NET 8 / WPF** — runtime and framework components are distributed under their applicable Microsoft and open-source license terms.
- **ARIEC61850** — sibling IEC 61850 engine maintained by `masarray`; distributed under its repository license.
- **SharpPcap** — packet capture/injection library used by the ARIEC61850 Npcap transport; distributed under the license declared by the SharpPcap project/package.
- **Npcap** — Windows packet capture driver supplied separately by the Npcap project. The ARVREL release does not grant Npcap redistribution rights and does not silently install the Npcap driver.
- **MSTest / Microsoft.NET.Test.Sdk** — test-only dependencies used by continuous integration.
- **Inno Setup** — used by release automation to compile the Windows installer; the compiler is not part of ARVREL source licensing.

## Release manifests

Official release assets include or are generated alongside:

- a transitive NuGet dependency report;
- a CycloneDX software bill of materials when the release tool is available;
- SHA-256 checksums;
- the ARVREL GPL license and commercial-licensing notice.

Review the release manifest before redistribution. A commercial ARVREL agreement cannot sublicense or override a third-party component.

## Npcap deployment note

Live Sampled Values capture requires a compatible Npcap installation. Npcap licensing differs by deployment scenario and organization. Obtain Npcap directly from its publisher and review its current terms before enterprise or OEM deployment.

## Reporting omissions

Open a documentation issue when a distributed component or required notice is missing. Do not attach proprietary software or license files to a public issue.
