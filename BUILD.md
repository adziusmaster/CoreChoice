# Building CoreChoice locally (Android)

This documents the **verified** local recipe for building the MAUI Android app. The
Core/Server projects build with a plain `dotnet build`; only the MAUI app needs the
extra setup below.

## Prerequisites (maintainer machine)

The maui workload is **not** in the global dotnet SDK at `/usr/local/share/dotnet`
(`dotnet workload list` there is empty). A user-local SDK with the workload installed
lives at `~/dotnet-maui`, plus a separate Android SDK and JDK 17 — the same layout
PurePrep uses:

```sh
export DOTNET_ROOT=$HOME/dotnet-maui
export PATH=$HOME/dotnet-maui:$PATH
export JAVA_HOME=$HOME/Library/Java/JavaVirtualMachines/jdk-17.0.20+8/Contents/Home
# Android SDK: ~/android-sdk
```

Verify before building — this is the check that tells you which SDK you are on:

```sh
dotnet workload list   # must show: maui-android
```

## Gotchas

1. **Using the global SDK fails** with `NETSDK1147: ... workloads must be installed:
   maui-android`, pointing you at `dotnet workload restore`. Do not run that against
   the global SDK — export `DOTNET_ROOT`/`PATH` above instead.
2. **Always pass `-f net10.0-android`.** CoreChoice single-targets Android today, so
   PurePrep's iOS/Mac Catalyst restore problem does not apply here — but do **not**
   pass `-p:TargetFrameworks=net10.0-android`, because that global property leaks into
   the referenced `CoreChoice.Core` project and clobbers its `net10.0` assets
   (`NETSDK1005`). This bites the moment another TFM is added.
3. **`-s` is a restore-only switch** — restore first, then build; passing `-s` to
   `dotnet build` errors with `MSB1001: Unknown switch`.
4. **Release `PublishTrimmed=true` appends the host RID** (`XA0035 osx-arm64`). Fix
   with `-p:UseDefaultPublishRuntimeIdentifier=false` — passed below on every build so
   Debug and Release behave the same.

## Running the tests

There is no solution file, so `dotnet test` from the repo root tries to build the MAUI project too
and fails with `NETSDK1147` on the global SDK. Run each test project by path instead — they are all
`net10.0` and need no workload:

```sh
dotnet test tests/CoreChoice.Core.Tests/CoreChoice.Core.Tests.csproj
dotnet test tests/CoreChoice.Server.Tests/CoreChoice.Server.Tests.csproj
dotnet test tests/CoreChoice.App.Tests/CoreChoice.App.Tests.csproj
```

## Debug build (compile check) — verified

```sh
dotnet build src/CoreChoice/CoreChoice.csproj -c Debug -f net10.0-android \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

Output: `src/CoreChoice/bin/Debug/net10.0-android/com.adziusmaster.corechoice-Signed.apk`
(debug-signed — sideloadable, not a Play artifact).

### Two traps that produce a green build and a broken APK

**1. A plain Debug APK contains no application code.** Android fast deployment is the SDK default for
Debug: the APK is a shell and `adb` pushes the assemblies separately during `-t:Install`. Installed by
hand it crashes on launch. For anything you hand someone to sideload, pass:

```sh
-p:EmbedAssembliesIntoApk=true
```

The APK grows from ~18 MB to ~91 MB, which is the code arriving. Verify rather than assume — the
assemblies are packaged as `lib/<abi>/lib_*.dll.so`, not under `assets/`:

```sh
unzip -l <apk> | grep -c 'lib_CoreChoice.dll.so'   # must be >= 1
```

**2. `ProcessMauiFonts` can go stale and silently ship no fonts.** If the build log says

```
Skipping target "ProcessMauiFonts" because all output files are up-to-date
```

while `obj/Debug/net10.0-android/assets/` holds no `.ttf`, the incremental stamp is lying: every build
then skips font processing and the app falls back to system fonts, losing Lora and DM Sans entirely.
Deleting `obj/.../resizetizer` is NOT enough — the stamp lives elsewhere. Delete `obj` and `bin`:

```sh
rm -rf src/CoreChoice/obj src/CoreChoice/bin
```

**Always check the artifact's contents before handing it over**, because both faults build cleanly:

```sh
unzip -l <apk> | grep -E '\.ttf$'                  # must list all six fonts
unzip -l <apk> | grep -E 'appicon'                 # icon present
```


The 27 warnings are all NU1608 (9 distinct, each repeated by the Android build path).
They are workload version-pinning noise, identical in PurePrep. No `NoWarn` is set.

## Branding assets

`MauiIcon` and `MauiSplashScreen` are registered in the csproj against
`Resources/AppIcon/appicon.svg` + `appiconfg.svg` and `Resources/Splash/splash.svg`,
all on `#0A0D0D`. A build regenerates the launcher mipmaps and `Maui.SplashTheme`; to
force it after changing an SVG, delete `obj/Debug/net10.0-android/resizetizer` first
(the resizetizer's up-to-date check is stamp-based and will otherwise skip).

Play Store listing graphics are generated separately — see `store-assets/README.md`.

## Signed release AAB — not yet set up

**No CoreChoice keystore exists yet** (`~/keystores/` holds only the PurePrep one), and
the csproj carries no signing block. Both are part of the pre-`.aab` checkpoint, not
something to improvise. When that checkpoint is reached, mirror PurePrep: a keystore at
`~/keystores/corechoice-upload.jks` outside the repo, signing activated only for Release
Android builds when `CORECHOICE_KEYSTORE_PASS` is set, and no secrets committed.

The release command will then be:

```sh
export CORECHOICE_KEYSTORE_PASS=$(cat ~/keystores/corechoice-upload.pass.txt)
dotnet build src/CoreChoice/CoreChoice.csproj -c Release -f net10.0-android \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidPackageFormat=aab \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

Verify the signature with `"$JAVA_HOME/bin/jarsigner" -verify <the .aab>`.

## Before each Play upload

Bump `<ApplicationVersion>` in `src/CoreChoice/CoreChoice.csproj` — it must be unique
and higher than any previously uploaded build. `<ApplicationDisplayVersion>` is the
user-facing version name and only needs bumping for a real release.
ApplicationId: `com.adziusmaster.corechoice`.
