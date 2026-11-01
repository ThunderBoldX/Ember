# Publication validation — 0.5.3

The release contains the existing Ember 0.5.3 application behavior. Publication work reorganizes source, adds documentation/tooling/notices, removes a real address from a test fixture and makes build scripts independent of local developer paths and signing passwords.

## Checks

- Windows application and native screenshot-tool builds.
- Windows unit suite: 146 checks, including protocol packets, FFT/audio analysis, effect timing, import validation, persistence and native WinRT buffer conversion.
- Real WPF routed-button regression fixture: 12 checks. Covers a single autonomous-effect click, controller-microphone exit, clicks during another action, the latest of rapid selections and switching from fast music frames to paced controls.
- Android Java/pack core checks: 79.
- Production Android BLE queue against a deterministic GATT fixture: 5 checks, including paced controls, fast music frames and cancellation after close.
- Android browser UI: 16 checks, including one-click command dispatch, saved devices, 16 tiles per effect category and narrow layout.
- Windows installer extraction, progress trace, packaged application startup/close and payload/source build matching.
- APK signature, manifest/version and packaged UI assets.
- README images and local links, both languages at wide/narrow widths, public repository/release/download links after publication.

Local output and machine-readable checksums/validation are attached to the release. GitHub Actions runs the build/test/documentation checks on pushes and pull requests. Its actual run result is visible in the Actions tab; a workflow file alone is not a claim that a remote run passed.

## What this does not establish

A physical RGB strip and a physical Android phone were not used for this publication build. Earlier local work established a GATT connection to one ELK-BLEDDM, but it is not a certification of every controller family or firmware. No end-to-end light-latency benchmark is claimed.

Screenshot data is fictional. Windows captures use actual WPF windows; Android captures use the actual WebView web assets rendered in Chromium. The demonstration data and capture provenance are disclosed in both READMEs.

## Українською

Перевірки підтверджують збірку, програмні протоколи, черги команд, натискання кнопок, формат ефектів, пакування та документацію. Вони не замінюють перевірку конкретної стрічки та телефона. Виміряної затримки «звук → світло» немає; числа на скриншотах є демонстраційними.
