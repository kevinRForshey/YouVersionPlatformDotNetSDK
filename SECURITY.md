# Security Policy

## Supported versions

This is a pre-1.0, single-maintainer SDK. Only the latest published version of each package is
supported; fixes are not backported to older tags.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for a security vulnerability, especially one related
to OAuth/PKCE token handling, session storage, or credential exposure.

Instead, report it privately via
[GitHub Security Advisories](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/security/advisories/new)
for this repository. Include:

- A description of the vulnerability and its potential impact.
- Steps to reproduce, or a minimal repro project if applicable.
- The affected package(s) and version(s).

You should expect an initial response within a few days. Once a fix is available, it will be
released as a new patch version and noted in `CHANGELOG.md`; credit will be given in the release
notes unless you ask otherwise.
