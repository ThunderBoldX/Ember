# Third-party notices

Ember's own source is covered by the root MIT license. That license does not replace the terms of third-party components.

## Windows runtime

The Windows Setup bundles Microsoft .NET 10 Core and Desktop runtimes. Their original license and third-party notices are retained inside the installed `runtime` directory. The notices from the runtime used for this publication are also preserved here:

- [Microsoft .NET runtime license](docs/third-party/dotnet/LICENSE.txt)
- [Microsoft .NET third-party notices](docs/third-party/dotnet/ThirdPartyNotices.txt)

Runtime files are not checked into this source repository. `windows/Get-Runtime.ps1` downloads official Microsoft archives and verifies their published SHA-512 hashes when a runtime must be prepared for packaging.

## BLE protocol references

- [dave-code-ruiz/elkbledom](https://github.com/dave-code-ruiz/elkbledom): ELK packet formats, microphone commands and controller settling behavior. [Original MIT notice](docs/third-party/elkbledom-LICENSE.txt), copyright (c) 2019.
- [muntahics/ELK-BLEDOM](https://github.com/muntahics/ELK-BLEDOM): ELK-BLEDOM command/UUID and pattern references. [Original MIT notice](docs/third-party/elk-bledom-LICENSE.txt), copyright (c) 2026 Md Muntahi Hossain.
- [madhead/saberlight](https://github.com/madhead/saberlight/blob/master/protocols/Triones/protocol.md): reference for Triones packet layout. No files from that repository are bundled with Ember.

## Development tools and platform APIs

Playwright (Apache-2.0) and marked (MIT) are development-only dependencies pinned in `package-lock.json`; their upstream license files remain in the packages installed by `npm ci`. They are not included in the APK or Windows application. Java, Android SDK tools, Node.js, Python and the .NET SDK are build prerequisites and are not redistributed as project source.

Android uses platform Bluetooth, WebView and playback-capture APIs. Fonts are requested from the operating system; no Inter or Segoe UI font files are bundled. The Ember logo and generated documentation banner are included with the project's own assets.
