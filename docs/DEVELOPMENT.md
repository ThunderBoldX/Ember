# Development / Розробка

[English overview](../README.md) · [Огляд українською](../README_UK.md)

## Layout

```text
windows/Ember/        WPF application, BLE bridge, audio and effect engines
windows/Tests/        Protocol, persistence, timing and bridge tests
windows/Examples/     Portable effect packs
android/src/          Native BLE/audio/service code
android/assets/       Actual WebView UI and effect-pack parser
android/tests/        Java, GATT-fixture and browser tests
docs/                 Installation, screenshots and retained third-party notices
tools/Screenshots/    Isolated native-window documentation renderer
scripts/              Documentation rendering and publication checks
.github/workflows/    Automatic build/test checks
```

## Windows build and run

Prerequisites: Windows x64 and .NET 10 SDK. BLE uses built-in Windows PowerShell 5.1/WinRT. The source has no third-party NuGet package dependency.

```powershell
./windows/Build.ps1
dotnet run --project windows/Ember/Ember.csproj -c Release
```

`global.json` accepts the latest installed .NET 10 SDK feature band. `Directory.Build.props` removes local checkout paths from debug-path mappings.

Create the offline installer:

```powershell
./windows/Build.ps1 -Setup
```

When `windows/runtime` is absent, `windows/Get-Runtime.ps1` copies the Core/Desktop runtimes and host already installed with the Windows x64 .NET 10 SDK, retaining their original license notices. It selects installed .NET 10 servicing versions and leaves the SDK untouched. No extra runtime download is needed for this step. You may instead pass a complete runtime directory:

```powershell
./windows/Build.ps1 -Setup -RuntimeDirectory C:\path\to\complete-runtime
```

Output: `artifacts/releases/Ember-Windows-v0.5.3-Setup.exe`. Packaging uses the built-in .NET Framework C# compiler for the small launcher/installer. The runtime version used for the published artifact is recorded in its release validation report; a fresh default packaging run uses the available installed .NET 10 servicing runtimes.

Українською: для запуску з коду потрібен SDK. Для готового Setup SDK не потрібний. `-Setup` додає runtime та створює один файл інсталятора; runtime і результати збірки ігноруються Git.

## Android build and private signing

Prerequisites: Windows/PowerShell, JDK 17+ on `PATH`, Android SDK **platform 35** and **build-tools 35.0.0**. Set `ANDROID_SDK_ROOT` or pass `-SdkRoot`. The direct build uses aapt2, javac, D8, zipalign and apksigner; it does not require Gradle.

Compile without signing credentials:

```powershell
./android/Build-Android.ps1 -SdkRoot $env:ANDROID_SDK_ROOT -Unsigned
```

This creates `artifacts/releases/Ember-Android-v0.5.3-unsigned.apk`. It is not installable until signed.

To build an installable APK, create your **own private keystore** using `keytool -genkeypair` and keep it outside version control. Set `EMBER_KEYSTORE_PASSWORD` and, if different, `EMBER_KEY_PASSWORD` privately in your terminal/CI environment. Then run:

```powershell
./android/Build-Android.ps1 -SdkRoot $env:ANDROID_SDK_ROOT -KeystorePath C:\private\ember-release.jks -KeyAlias ember
```

The script passes environment-variable names to apksigner, not password values in command arguments. It never generates or commits a release key. Own-key builds cannot update the official differently signed installation. Export effects before changing installations. The public CI compiles an unsigned APK and has no signing secret.

Українською: приватний ключ релізів не входить до репозиторію. Для власного APK використовуй свій keystore й локальні змінні середовища. Не додавай ключі, паролі або `.env` до Git.

## Tests

Run on Windows:

```powershell
dotnet run --project windows/Tests/Tests.csproj -c Release --configfile windows/NuGet.Config
./android/Test-Core.ps1
python android/tests/BleDeliveryTest.py
npm ci
npx playwright install chromium
npm run test:ui
python scripts/check-publication.py
```

The Java core/queue fixtures and Node parser tests also run without a physical phone. The C# default suite includes a native WinRT buffer check and process-backed Bluetooth fixtures, but does not scan devices or play audio. `--audio` and `--ble` are **optional local hardware diagnostics**, not CI steps.

Run the WPF single-click regression fixture separately:

```powershell
$env:EMBER_TEST_BRIDGE = (Resolve-Path windows/Tests/SingleClickFixture.ps1).Path
dotnet windows/Ember/bin/Release/net10.0-windows/Ember.dll --verify-effects
```

The result is written beside the build as `single-click-validation.txt`. The fixture acknowledges transport while dropping too-fast controls, so it checks a controller-processing failure that ordinary API-success tests miss. It uses no actual Bluetooth device.

<a id="screenshots"></a>
## Screenshots and banner

```powershell
dotnet run --project tools/Screenshots/Screenshots.csproj -c Release -- --preview docs/screenshots
npm run screenshots:android
node scripts/render-banner.cjs
node scripts/preview-readme.cjs
```

Windows captures are real WPF controls with isolated in-memory demonstration state. The tool requires `--preview`, stops polling and never starts a real BLE session. It does not load/save the user's preferences. The additional demo state is confined to this documentation tool, not the application's normal startup.

Android captures render the actual `android/assets` UI in Chromium, with a no-op native bridge and fictional data. They are not physical-device screenshots. Every capture carries demo labels; device addresses are examples, and spectrum/BPM values are not measurements. The PNG images used by GitHub and the editable SVG banner are stored in the repository.

`preview-readme.cjs` renders both languages at wide and narrow widths, checks image loading/overflow and writes review artifacts under the ignored `artifacts/readme` directory.

## Effect packs

Use the examples under `windows/Examples` or `android/Examples`. The apps accept Ember v1/v2 packs and export v2. The pack parser validates fields instead of accepting executable scripts. Limits include 1 MiB per imported pack, 1–128 steps per effect, 60 UTF-16 code units in a name, and 125–60000 ms minimum duration per step. Current v2 conditions are documented by the shipped examples and [JSON schema](ember-effects.schema.json).

Timing/audio triggers need the application and Bluetooth connection. A legacy microphone-input condition must be edited before playback; the controller's microphone signal is not accessible as an input to the software sequence engine.

## Contributing

Keep actual controller behavior separate from transport success. Include the controller family/firmware and a minimal reproduction for a BLE fix; redact addresses and personal paths before sharing diagnostics. Do not commit APKs, runtime downloads, private signing material or user settings. New platform/controller support should be documented as tested only after appropriate verification.
