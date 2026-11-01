# Install Ember 0.5.3 / Встановлення Ember 0.5.3

[English README](../README.md) · [Українська інструкція](../README_UK.md#install) · [Release v0.5.3](https://github.com/ThunderBoldX/Ember/releases/tag/v0.5.3)

## Windows

1. Download **[Ember-Windows-v0.5.3-Setup.exe](https://github.com/ThunderBoldX/Ember/releases/download/v0.5.3/Ember-Windows-v0.5.3-Setup.exe)**.
2. Run Setup → **Встановити**. The desktop runtime is included. Setup installs for the current user and can create shortcuts.
3. Power the strip and turn Bluetooth on. Open Ember → **Пошук Bluetooth** → select your strip → **Підключити**.
4. Choose a color or an **Автономні** effect. A successful connection saves the device.

Українською: завантаж Setup, запусти встановлення й відкрий Ember. Увімкни фізичне живлення стрічки та Bluetooth, знайди пристрій і підключись. Папки вручну розпаковувати не потрібно.

## Android

1. Download **[Ember-Android-v0.5.3.apk](https://github.com/ThunderBoldX/Ember/releases/download/v0.5.3/Ember-Android-v0.5.3.apk)** and open it.
2. Allow your browser/file manager to install this APK when prompted. Grant Bluetooth/Nearby devices permission; older Android versions also require location permission for BLE scanning.
3. Power the strip → **Знайти стрічку** → select the device.
4. Phone music: Android 10+ → **Музика телефона** → grant audio/capture consent → choose an effect. Some source apps block capture.

Українською: відкрий APK, дозволь встановлення з джерела й доступ до Bluetooth. Знайди ввімкнену стрічку. Для музики телефона потрібні Android 10+ та окремий дозвіл на захоплення аудіо.

The release APK is signed with the existing Ember certificate. Updating over an earlier matching release retains its local settings/effects. Source builds signed with a different key cannot replace it directly.

## Verify the download / Перевірка файла

Download `SHA256SUMS.txt` from the same release and compare the relevant line with:

```powershell
Get-FileHash .\Ember-Windows-v0.5.3-Setup.exe -Algorithm SHA256
Get-FileHash .\Ember-Android-v0.5.3.apk -Algorithm SHA256
```

The Windows Setup has no Authenticode signature; its publisher may appear unknown. Download only the intended release and verify its checksum.

<a id="data-and-removal"></a>
## Data and removal / Дані та видалення

- Windows installation: `%LocalAppData%\Ember\versions\0.5.3`. Shortcuts target `Ember.exe` inside that folder.
- Windows preferences and sequences: `%LocalAppData%\Ember\settings.json` and `effects.json`. They are not part of the public repository or release payload.
- Windows removal: close Ember (including the tray instance), remove that version directory and its desktop/Start menu shortcuts. There is currently no uninstall wizard. Keep or remove the preference files separately as needed.
- Android data stays in the application's private storage. Export effect packs before uninstalling; uninstalling removes local app data.

Українською: налаштування не публікуються разом із програмою. На Windows видалення папки конкретної версії не вимагає видаляти свої ефекти. На Android перед видаленням програми експортуй потрібні послідовності.

## If connection fails / Якщо підключення не вдалося

Check physical power first. A controller saved in Windows can remain listed even when unplugged. Move closer and close other apps using the strip. Another phone may hold its BLE connection. `Unreachable` is a connection failure, not proof of an unsupported firmware protocol.

More help: [English troubleshooting](../README.md#troubleshooting) · [Типові проблеми](../README_UK.md#типові-проблеми).
