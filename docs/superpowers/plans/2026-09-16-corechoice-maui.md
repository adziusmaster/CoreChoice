# CoreChoice MAUI App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the CoreChoice Android app — a person takes the Big Five test on their phone, states a dilemma, and receives advice from the backend shaped by their personality.

**Architecture:** A .NET MAUI Android app referencing the existing `CoreChoice.Core` project, so the personality scoring is byte-identical on phone and server rather than reimplemented. Screens are MVVM; everything outside the view models sits behind the application ports Core already defines. Local SQLite holds the test answers and the profile; the backend is reached over HTTP and is told nothing it does not need.

**Tech Stack:** .NET 10 MAUI (`net10.0-android`), SQLite via `Microsoft.EntityFrameworkCore.Sqlite`, `Xamarin.Android.Google.BillingClient`, Android `SpeechRecognizer`, xUnit + NSubstitute + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-15-corechoice-vertical-slice-design.md`, plus the approved design canvas (ten screens; Lora for anything the app says, DM Sans for interface; Considered / Composed / Still palettes in dark and light).

**Backend:** complete, 162 tests, verified against the live Gemini API. Its contract is fixed — this app is a client of it and must not require changes to it, except where Task 12 says so explicitly.

## Global Constraints

- **Build environment.** The MAUI workload is NOT in the global dotnet SDK — `dotnet workload list` there is empty. Every MAUI build uses the user-local SDK:
  ```sh
  export DOTNET_ROOT=$HOME/dotnet-maui
  export PATH=$HOME/dotnet-maui:$PATH
  export JAVA_HOME=$HOME/Library/Java/JavaVirtualMachines/jdk-17.0.20+8/Contents/Home
  ```
  Android SDK is at `$HOME/android-sdk`. Pass `-p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME` to every build.
- **Build only the Android target framework:** `-f net10.0-android`. Do NOT pass `-p:TargetFrameworks=...` — that global property leaks into the referenced `CoreChoice.Core` project and breaks its `net10.0` assets with `NETSDK1005`.
- **`-s` is restore-only.** Restore first with `-s https://api.nuget.org/v3/index.json`, then build with `--no-restore`. Passing `-s` to `dotnet build` errors with `MSB1001`.
- Target framework `net10.0-android`; `Nullable` and `ImplicitUsings` enabled.
- **Async all the way.** Every IO-bound method takes a `CancellationToken` and passes it to the actual IO call. No `.Result`, no `.Wait()`.
- **A compensating action must never be cancellable by the failure that triggered it.** The backend build shipped two bugs of exactly this kind; the same rule applies to any client-side retry or rollback.
- `record` for value objects and DTOs. `internal` over `public` for implementation classes.
- Modern C#: file-scoped namespaces, primary constructors, collection expressions.
- Test naming `MethodName_StateUnderTest_ExpectedBehavior`, strict `// Arrange` / `// Act` / `// Assert`. Mock outbound ports only; never mock domain types.
- Commits authored by `adziusmaster <adzius.lech@gmail.com>` — already configured repo-locally. Never pass `--author`.
- **NO `Co-Authored-By` trailer and NO "Generated with Claude" line** in any commit message.
- Do NOT use `git add -A` or `git add .`. Stage files by explicit path. Never stage anything under `docs/`.
- The build must stay at **0 warnings**. Backend tests (Core 77, Server 85) must keep passing.

## Product invariants this app must not break

Carried from the spec. Each is a promise to a person, not a preference.

1. **The personality test and its full result are free, permanently.** No screen may check a balance before scoring or displaying a profile. `ProfileViewModel` takes no dependency on the coin ledger at all — the invariant is structural, not remembered.
2. **The dilemma and the profile are never persisted server-side.** They travel in the request body and nowhere else. The app stores them locally only.
3. **Coin prices are never hardcoded in shipped UI.** They come from Google Play's `FormattedPrice`, which is localised and tax-inclusive, so the figure shown matches what the user is actually charged in their country. The euro figures in the design are fallback labels shown only when the store cannot be reached.
4. **A purchase is consumed only after the backend has granted the coins.** Consuming first means a paid purchase can vanish if the grant call fails.

## File structure

```
src/CoreChoice/                                  net10.0-android
  CoreChoice.csproj
  MauiProgram.cs                                 composition root
  App.xaml(.cs)  AppShell.xaml(.cs)              shell + routes
  Resources/Fonts/                               Lora + DMSans ttf files
  Resources/Styles/
    Tokens.Considered.Dark.xaml                  six token dictionaries,
    Tokens.Considered.Light.xaml                 identical keys in each
    Tokens.Composed.Dark.xaml
    Tokens.Composed.Light.xaml
    Tokens.Still.Dark.xaml
    Tokens.Still.Light.xaml
    Styles.xaml                                  shared styles, DynamicResource only
  Services/
    AppearanceChoice.cs                          Palette + Mode enums
    ThemeService.cs                              palette x mode swapping
    SecureStorageDeviceIdentity.cs               IDeviceIdentity
    CoreChoiceApiClient.cs                       IDecisionAdvisor + ICoinLedgerClient + personas
    VerdictLabel.cs                              confidence -> the four labels
  Data/
    LocalDbContext.cs                            SQLite: answers, profile, history
    SqliteProfileRepository.cs                   IProfileRepository
  Platforms/Android/
    AndroidVoiceDictation.cs                     IVoiceDictation
    PlayBillingService.cs                        IBillingService
    MainActivity.cs  MainApplication.cs
  Presentation/
    TestIntroViewModel.cs   TestIntroPage.xaml(.cs)
    TestViewModel.cs        TestPage.xaml(.cs)
    ProfileViewModel.cs     ProfilePage.xaml(.cs)
    DilemmaViewModel.cs     DilemmaPage.xaml(.cs)
    PersonaViewModel.cs     PersonaPage.xaml(.cs)
    AnalysisViewModel.cs    AnalysisPage.xaml(.cs)
    CoinsViewModel.cs       CoinsPage.xaml(.cs)
    SettingsViewModel.cs    SettingsPage.xaml(.cs)

src/CoreChoice.Core/Application/                 additions to the existing project
  IProfileRepository.cs                          local answer + profile storage
  ICoinLedgerClient.cs                           balance, seed, profile grant
  IBillingService.cs                             store packs, purchase, consume

tests/CoreChoice.App.Tests/                      view models and services, no UI
```

## Checkpoints

Two hard stops, both requested:

- **After Task 11** — everything built and running on a device or emulator, before any `.aab` is produced. Signing, versioning and the store listing wait for explicit approval.
- **Before any Hetzner deploy.** This plan never deploys. DNS and the server rollout happen separately, after the app is seen working.

---

### Task 1: MAUI project scaffold, fonts, and a build that actually runs

**Files:**
- Create: `src/CoreChoice/CoreChoice.csproj`, `src/CoreChoice/MauiProgram.cs`, `src/CoreChoice/App.xaml(.cs)`, `src/CoreChoice/AppShell.xaml(.cs)`, `src/CoreChoice/Platforms/Android/MainActivity.cs`, `src/CoreChoice/Platforms/Android/MainApplication.cs`, `src/CoreChoice/Platforms/Android/AndroidManifest.xml`, `tests/CoreChoice.App.Tests/CoreChoice.App.Tests.csproj`
- Modify: `CoreChoice.slnx`

**Interfaces:**
- Consumes: the existing `CoreChoice.Core` project
- Produces: an installable debug APK that launches to a blank shell, and a test project that runs

**Why this task is mostly environment:** the MAUI workload is not in the global SDK. Every command below uses the user-local one. Getting this wrong produces `NETSDK1147: workloads must be installed: android` and wastes an hour.

- [ ] **Step 1: Confirm the toolchain**

```bash
export DOTNET_ROOT=$HOME/dotnet-maui
export PATH=$HOME/dotnet-maui:$PATH
export JAVA_HOME=$HOME/Library/Java/JavaVirtualMachines/jdk-17.0.20+8/Contents/Home
dotnet --version                 # expect 10.0.203, from ~/dotnet-maui
dotnet workload list             # must list maui-android or android
ls $HOME/android-sdk/platforms   # must contain at least one android-NN
```

If `dotnet workload list` is empty here, stop and report BLOCKED — the rest of the plan cannot proceed and installing a workload is not a decision to make unattended.

- [ ] **Step 2: Fetch the fonts**

Google's `/download?family=` zip endpoint returns the site's HTML shell to `curl`, not a zip — a
file saved from it has a `.ttf` name and is a webpage, which renders as system fallback and looks
like a font choice rather than a broken download. Use the JSON manifest instead, which gives direct
`fonts.gstatic.com` URLs:

```bash
mkdir -p src/CoreChoice/Resources/Fonts && cd src/CoreChoice/Resources/Fonts
for fam in "Lora" "DM+Sans"; do
  curl -sL "https://fonts.google.com/download/list?family=$fam" \
    | tail -c +6 \
    | python3 -c "import sys,json;[print(f['url'],f['filename']) for f in json.load(sys.stdin)['manifest']['fileRefs']]"
done | grep -Ei '(Lora|DMSans)-(Regular|Medium|SemiBold|Bold)\.ttf' \
  | while read url name; do curl -sL "$url" -o "$(basename "$name")"; done
ls -la
```

The `tail -c +6` strips Google's anti-JSON-hijacking prefix. Then VERIFY each file is a real font
rather than a saved error page — a check worth doing because the failure is silent:

```bash
file *.ttf                      # must say TrueType/sfnt, never HTML
ls -l *.ttf                     # DM Sans ~55K, Lora ~131K; a few KB means a webpage
```

Keep exactly these six and delete the rest: `Lora-Regular.ttf`, `Lora-Medium.ttf`, `Lora-SemiBold.ttf`, `DMSans-Regular.ttf`, `DMSans-Medium.ttf`, `DMSans-Bold.ttf`. If the archive ships only variable fonts (a single `Lora[wght].ttf`), keep the variable file and register it once per family instead — note which you did in your report, because the weights available to XAML differ.

- [ ] **Step 3: Write the csproj**

`src/CoreChoice/CoreChoice.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <RootNamespace>CoreChoice</RootNamespace>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <MauiXamlInflator>SourceGen</MauiXamlInflator>

    <ApplicationTitle>CoreChoice</ApplicationTitle>
    <ApplicationId>com.adziusmaster.corechoice</ApplicationId>
    <ApplicationDisplayVersion>0.1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>

    <SupportedOSPlatformVersion>24.0</SupportedOSPlatformVersion>
  </PropertyGroup>

  <!--
    Release hardening, mirroring PurePrep. R8 emits the mapping file Play needs for readable
    crash reports; managed symbols make Release stack traces legible; native libs stay unstripped
    so a native-symbols archive can be produced from them later.
  -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <AndroidLinkTool>r8</AndroidLinkTool>
    <AndroidManagedSymbols>true</AndroidManagedSymbols>
    <AndroidStripNativeLibraries>false</AndroidStripNativeLibraries>
  </PropertyGroup>

  <ItemGroup>
    <MauiFont Include="Resources\Fonts\*" />
    <MauiImage Include="Resources\Images\*" />
    <MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
    <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.13" />
  </ItemGroup>

  <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
    <PackageReference Include="Xamarin.Android.Google.BillingClient" Version="8.3.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CoreChoice.Core\CoreChoice.Core.csproj" />
  </ItemGroup>
</Project>
```

The `SQLitePCLRaw.bundle_e_sqlite3` pin is not optional — EF Core 10 pulls a transitive `lib.e_sqlite3` 2.1.11 carrying a high-severity advisory (GHSA-2m69-gcr7-jv3q). Both server projects pin it for the same reason.

- [ ] **Step 4: Write the app shell**

`src/CoreChoice/App.xaml.cs`:

```csharp
namespace CoreChoice;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell());
}
```

`src/CoreChoice/App.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Application xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="CoreChoice.App">
  <Application.Resources>
    <ResourceDictionary />
  </Application.Resources>
</Application>
```

`src/CoreChoice/AppShell.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       x:Class="CoreChoice.AppShell"
       FlyoutBehavior="Disabled"
       Shell.NavBarIsVisible="False" />
```

`src/CoreChoice/AppShell.xaml.cs`:

```csharp
namespace CoreChoice;

public partial class AppShell : Shell
{
    public AppShell() => InitializeComponent();
}
```

`src/CoreChoice/MauiProgram.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace CoreChoice;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Lora-Regular.ttf", "Lora");
                fonts.AddFont("Lora-Medium.ttf", "LoraMedium");
                fonts.AddFont("Lora-SemiBold.ttf", "LoraSemiBold");
                fonts.AddFont("DMSans-Regular.ttf", "DMSans");
                fonts.AddFont("DMSans-Medium.ttf", "DMSansMedium");
                fonts.AddFont("DMSans-Bold.ttf", "DMSansBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
```

- [ ] **Step 5: Android platform files**

`src/CoreChoice/Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content.PM;

namespace CoreChoice;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
        | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity;
```

`src/CoreChoice/Platforms/Android/MainApplication.cs`:

```csharp
using Android.App;
using Android.Runtime;

namespace CoreChoice;

[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
```

`src/CoreChoice/Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <application android:allowBackup="true" android:supportsRtl="true" />
  <uses-permission android:name="android.permission.INTERNET" />
  <!-- Voice dictation for the dilemma fields. Requested at the moment the mic is tapped,
       never at launch: an app that asks for the microphone on first open gets denied. -->
  <uses-permission android:name="android.permission.RECORD_AUDIO" />
  <queries>
    <intent>
      <action android:name="android.speech.RecognitionService" />
    </intent>
  </queries>
</manifest>
```

- [ ] **Step 6: Test project**

`tests/CoreChoice.App.Tests/CoreChoice.App.Tests.csproj` targets `net10.0` and needs MORE than a copy
of the Core test project: later tasks compile linked source files that use EF Core and the MVVM
toolkit, so those packages belong here from the start rather than being discovered missing mid-task.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NSubstitute" Version="6.2.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <!-- Task 4 links the SQLite repository source; Task 5 stubs HTTP. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
    <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.13" />
    <!-- Task 7 onward links view models, which derive from ObservableObject. -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\CoreChoice.Core\CoreChoice.Core.csproj" />
  </ItemGroup>
</Project>
```

It references **Core only**, never the MAUI project — a `net10.0-android` project cannot be referenced
from a plain test host. Later tasks therefore compile individual app source files as LINKED files
(`<Compile Include="..\..\src\CoreChoice\..." Link="Linked\..." />`), which only works while those
files avoid MAUI types.

**Every view model derives from `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`** and uses
`[ObservableProperty]` / `[RelayCommand]`. The toolkit targets netstandard, so a linked view model
compiles in a plain test host; MAUI's own `BindableObject` would not. Hand-rolling
`INotifyPropertyChanged` across eight view models is the alternative and is not worth it.

Add both new projects to `CoreChoice.slnx` alongside the existing four.

- [ ] **Step 7: Restore and build**

```bash
dotnet restore src/CoreChoice/CoreChoice.csproj -s https://api.nuget.org/v3/index.json
dotnet build src/CoreChoice/CoreChoice.csproj -c Debug -f net10.0-android --no-restore \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
dotnet test tests/CoreChoice.Core.Tests --no-restore   # 77, still green
dotnet test tests/CoreChoice.Server.Tests --no-restore # 85, still green
```

Expected: build succeeds, 0 warnings. If restore trips over the iOS workload, you passed a `TargetFrameworks` property somewhere — remove it.

- [ ] **Step 8: Commit**

```bash
git add src/CoreChoice tests/CoreChoice.App.Tests CoreChoice.slnx
git commit -m "Scaffold the MAUI Android app

Android-only target framework, Lora and DM Sans registered, and the
SQLitePCLRaw pin the server projects already carry for the same advisory.
The test project references Core rather than the app: a net10.0-android
project cannot be referenced from a plain test host, which is what keeps
the view model logic testable."
```

---

### Task 2: The app-side application ports

**Files:**
- Create: `src/CoreChoice.Core/Application/IProfileRepository.cs`, `src/CoreChoice.Core/Application/ICoinLedgerClient.cs`, `src/CoreChoice.Core/Application/IBillingService.cs`
- Test: `tests/CoreChoice.App.Tests/Application/PortContractTests.cs`

**Interfaces:**
- Consumes: `OceanProfile`, `PersonaId` (existing domain types)
- Produces:
  - `public sealed record StoredAnswers(IReadOnlyDictionary<int,int> Responses, DateTimeOffset UpdatedAt)`
  - `public interface IProfileRepository` — `LoadAnswersAsync`, `SaveAnswerAsync(int itemNumber, int response, ct)`, `ClearAnswersAsync`, `LoadProfileAsync`, `SaveProfileAsync(OceanProfile, ct)`
  - `public sealed record CoinBalance(int Balance)`, `public sealed record GrantResult(bool Granted, int Balance, string? Reason)`
  - `public interface ICoinLedgerClient` — `GetBalanceAsync`, `EnsureSeededAsync`, `ClaimProfileGrantAsync`
  - `public sealed record AnalysisPack(string ProductId, int Analyses, string DisplayPrice)`
  - `public sealed record PurchaseTicket(string ProductId, string PurchaseToken)`
  - `public interface IBillingService` — `bool IsSupported`, `IReadOnlyList<AnalysisPack> FallbackPacks`, `GetPacksAsync`, `BuyAsync`, `ConsumeAsync`

**The billing contract is the load-bearing part.** Mirror PurePrep's shape exactly, including the comments explaining WHY, because both rules are easy to "simplify" into bugs:
- `GetPacksAsync` returns Google Play's `FormattedPrice` — localised and tax-inclusive, the exact string the user is charged. VAT differs per country, so a hardcoded price is wrong everywhere but one.
- `BuyAsync` leaves the purchase **un-consumed**. The caller grants coins on the backend first and calls `ConsumeAsync` only after that succeeds. Consuming first means a paid purchase can disappear when the grant call fails.

- [ ] **Step 1: Write the failing test**

`tests/CoreChoice.App.Tests/Application/PortContractTests.cs`:

```csharp
using CoreChoice.Application;
using CoreChoice.Domain;
using FluentAssertions;

namespace CoreChoice.App.Tests.Application;

public class PortContractTests
{
    [Fact]
    public void AnalysisPack_ShouldCarryAStorePriceString()
    {
        // Arrange — the display price is a string, not a decimal, because Play returns a
        // formatted, localised, tax-inclusive label and the app must show exactly that.
        var pack = new AnalysisPack("corechoice.analyses.30", 30, "€5,99");

        // Act
        var price = pack.DisplayPrice;

        // Assert
        price.Should().Be("€5,99");
        pack.Analyses.Should().Be(30);
    }

    [Fact]
    public void GrantResult_WhenRefused_ShouldStillCarryTheCurrentBalance()
    {
        // Arrange — a refused grant must never look like an error to the UI; the person
        // still needs to be told what they have.
        var outcome = new GrantResult(Granted: false, Balance: 10, Reason: "already-granted");

        // Act & Assert
        outcome.Granted.Should().BeFalse();
        outcome.Balance.Should().Be(10);
        outcome.Reason.Should().Be("already-granted");
    }

    [Fact]
    public void StoredAnswers_ShouldExposeResponsesByItemNumber()
    {
        // Arrange — answers are keyed by IPIP item number, which is a storage contract:
        // renumbering the bank silently re-keys every saved profile.
        var answers = new StoredAnswers(new Dictionary<int, int> { [1] = 4, [17] = 2 }, DateTimeOffset.UtcNow);

        // Act
        var seventeen = answers.Responses[17];

        // Assert
        seventeen.Should().Be(2);
        answers.Responses.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test tests/CoreChoice.App.Tests --no-restore
```

Expected: FAIL — the types do not exist.

- [ ] **Step 3: Write the ports**

`src/CoreChoice.Core/Application/IProfileRepository.cs`:

```csharp
using CoreChoice.Domain;

namespace CoreChoice.Application;

/// <summary>Answers saved so far, keyed by IPIP item number.</summary>
public sealed record StoredAnswers(IReadOnlyDictionary<int, int> Responses, DateTimeOffset UpdatedAt);

/// <summary>
/// Local, on-device storage for the personality test. Answers persist per item rather than on
/// submit, because a fifty-item questionnaire is abandoned mid-way by exactly the person this
/// product is for — low conscientiousness is, definitionally, difficulty finishing tasks.
///
/// Nothing here ever reaches the network. The profile leaves the device only inside a decision
/// request, and is not stored at the other end.
/// </summary>
public interface IProfileRepository
{
    Task<StoredAnswers> LoadAnswersAsync(CancellationToken ct = default);

    /// <summary>Persists one answer immediately. Called on every tap, not on page change.</summary>
    Task SaveAnswerAsync(int itemNumber, int response, CancellationToken ct = default);

    Task ClearAnswersAsync(CancellationToken ct = default);

    /// <summary>The scored profile, or <see cref="OceanProfile.None"/> when the test is unfinished.</summary>
    Task<OceanProfile> LoadProfileAsync(CancellationToken ct = default);

    Task SaveProfileAsync(OceanProfile profile, CancellationToken ct = default);
}
```

`src/CoreChoice.Core/Application/ICoinLedgerClient.cs`:

```csharp
namespace CoreChoice.Application;

public sealed record CoinBalance(int Balance);

/// <summary>Outcome of a grant attempt. <paramref name="Balance"/> is always the current balance.</summary>
public sealed record GrantResult(bool Granted, int Balance, string? Reason);

/// <summary>The app's view of the coin ledger, which lives on the server.</summary>
public interface ICoinLedgerClient
{
    Task<CoinBalance> GetBalanceAsync(CancellationToken ct = default);

    /// <summary>First contact. Seeds the free coins; safe to call on every launch.</summary>
    Task<CoinBalance> EnsureSeededAsync(CancellationToken ct = default);

    /// <summary>
    /// Claims the coins granted for finishing the personality test. Idempotent on the server:
    /// a retry after a dropped connection returns the unchanged balance rather than granting twice
    /// or costing the person their grant.
    /// </summary>
    Task<GrantResult> ClaimProfileGrantAsync(CancellationToken ct = default);
}
```

`src/CoreChoice.Core/Application/IBillingService.cs`:

```csharp
namespace CoreChoice.Application;

/// <summary>A purchasable pack of analyses, mapped to a Google Play product id.</summary>
public sealed record AnalysisPack(string ProductId, int Analyses, string DisplayPrice);

/// <summary>A completed, deliberately UN-CONSUMED purchase, to be redeemed with the backend.</summary>
public sealed record PurchaseTicket(string ProductId, string PurchaseToken);

/// <summary>
/// The platform store. Behind an interface so the view model and redemption logic stay
/// platform-agnostic and testable.
/// </summary>
public interface IBillingService
{
    /// <summary>False on a build or device where billing is unavailable. The UI hides buying entirely.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Packs with placeholder price labels, used only when the store cannot be reached — offline,
    /// billing unavailable, or the products are not live in Play yet.
    /// </summary>
    IReadOnlyList<AnalysisPack> FallbackPacks { get; }

    /// <summary>
    /// Packs with prices resolved from the store: Google Play's localised, tax-inclusive
    /// FormattedPrice, i.e. the exact string the user is charged at checkout. VAT rates differ by
    /// country, so this is the only way a displayed price can be correct everywhere. Falls back to
    /// the matching <see cref="FallbackPacks"/> label per pack whose price cannot be fetched.
    /// </summary>
    Task<IReadOnlyList<AnalysisPack>> GetPacksAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs the purchase flow. Returns null when the person cancelled. The purchase is left
    /// UN-CONSUMED on purpose: the caller redeems it with the backend first and calls
    /// <see cref="ConsumeAsync"/> only once the coins are actually granted. Consuming first means a
    /// paid purchase can vanish if the grant call fails.
    /// </summary>
    Task<PurchaseTicket?> BuyAsync(string productId, CancellationToken ct = default);

    /// <summary>Consumes a redeemed purchase so the product can be bought again.</summary>
    Task ConsumeAsync(string purchaseToken, CancellationToken ct = default);
}
```

- [ ] **Step 4: Run it green**

```bash
dotnet test tests/CoreChoice.App.Tests --no-restore
dotnet test tests/CoreChoice.Core.Tests --no-restore   # still 77
```

- [ ] **Step 5: Commit**

```bash
git add src/CoreChoice.Core/Application tests/CoreChoice.App.Tests
git commit -m "Add the app-side application ports

Local profile storage, the coin ledger client, and the store. The billing
contract keeps PurePrep's two rules: prices come from Play's localised
FormattedPrice, and a purchase stays un-consumed until the backend has
actually granted the coins."
```

---

### Task 3: Theme tokens and the palette switcher

**Files:**
- Create: six `src/CoreChoice/Resources/Styles/Tokens.<Palette>.<Mode>.xaml`, `src/CoreChoice/Resources/Styles/Styles.xaml`, `src/CoreChoice/Services/AppearanceChoice.cs`, `src/CoreChoice/Services/ThemeService.cs`
- Modify: `src/CoreChoice/App.xaml`, `src/CoreChoice/MauiProgram.cs`
- Test: `tests/CoreChoice.App.Tests/Services/AppearanceChoiceTests.cs`

**Interfaces:**
- Produces:
  - `public enum Palette { Considered, Composed, Still }`
  - `public enum ThemeMode { System, Light, Dark }`
  - `public sealed record AppearanceChoice(Palette Palette, ThemeMode Mode)` with `static AppearanceChoice Default => new(Palette.Considered, ThemeMode.Dark)`, `string DictionaryName` returning e.g. `"Tokens.Considered.Dark"`
  - `public sealed class ThemeService` — `AppearanceChoice Current`, `void Set(AppearanceChoice)`, `void Apply()`

**Every dictionary defines the same keys.** A palette that omits one renders a screen half-styled, and it will be a screen nobody opened during testing. The complete key set, taken from the design:

`Bg` `BgElevated` `Surface` `SurfaceSunken` `Line` `LineSoft` `Accent` `AccentBright` `AccentDim` `AccentInk` `AccentLine` `AccentWash` `Ink` `InkSoft` `Muted` `Faint`

Considered dark, as the reference: `Bg #0A0D0D`, `BgElevated #0E1313`, `Surface #121818`, `SurfaceSunken #0E1313`, `Line #1F2A29`, `LineSoft #161E1D`, `Accent #4FD1C5`, `AccentBright #7FE3D9`, `AccentDim #7FB8B2`, `AccentInk #04100F`, `AccentLine #2A514C`, `AccentWash #132422`, `Ink #EAF2F1`, `InkSoft #C6D5D3`, `Muted #879795`, `Faint #5A6968`.

Considered light: `Bg #F3F7F6`, `BgElevated #FFFFFF`, `Surface #FFFFFF`, `SurfaceSunken #ECF3F1`, `Line #DDE8E6`, `LineSoft #E8F0EE`, `Accent #0D7A71`, `AccentBright #0A5F58`, `AccentDim #3E8F88`, `AccentInk #FFFFFF`, `AccentLine #B8DAD5`, `AccentWash #E2F2EF`, `Ink #0F1615`, `InkSoft #26332F`, `Muted #546260`, `Faint #879895`.

Composed dark: `Bg #0E1114`, `Surface #161B21`, `Line #242C34`, `Accent #82A9DC`, `AccentInk #0B1219`, `AccentWash #1B2732`, `Ink #E9EEF4`, `InkSoft #CBD5DF`, `Muted #8D99A6`, `Faint #5E6975`. Composed light: `Bg #F6F7F9`, `Surface #FFFFFF`, `Line #E2E7EC`, `Accent #2C5D9B`, `AccentInk #FFFFFF`, `AccentWash #EAF0F8`, `Ink #121820`, `InkSoft #2C3540`, `Muted #5A6673`, `Faint #8C96A2`.

Still dark: `Bg #101017`, `Surface #1A1A25`, `Line #282836`, `Accent #B7A6E9`, `AccentInk #12101C`, `AccentWash #211F31`, `Ink #ECE9F5`, `InkSoft #CFC9E2`, `Muted #8F8AA4`, `Faint #625D77`. Still light: `Bg #F6F5FA`, `Surface #FFFFFF`, `Line #E6E3F0`, `Accent #5B4B9E`, `AccentInk #FFFFFF`, `AccentWash #EEEBF8`, `Ink #15131E`, `InkSoft #302B40`, `Muted #605A73`, `Faint #918BA6`.

Derive the four keys not listed for Composed and Still the way Considered does: `BgElevated` and `SurfaceSunken` one step from `Bg`, `LineSoft` one step softer than `Line`, `AccentBright`/`AccentDim`/`AccentLine` from `Accent`. Keep them in the same relationship so the three palettes feel like one family.

- [ ] **Step 1: Write the failing test**

`tests/CoreChoice.App.Tests/Services/AppearanceChoiceTests.cs`:

```csharp
using CoreChoice.Services;
using FluentAssertions;

namespace CoreChoice.App.Tests.Services;

public class AppearanceChoiceTests
{
    [Fact]
    public void Default_ShouldBeConsideredDark()
    {
        // Arrange & Act
        var choice = AppearanceChoice.Default;

        // Assert
        choice.Palette.Should().Be(Palette.Considered);
        choice.Mode.Should().Be(ThemeMode.Dark);
    }

    [Theory]
    [InlineData(Palette.Considered, ThemeMode.Dark,  "Tokens.Considered.Dark")]
    [InlineData(Palette.Composed,   ThemeMode.Light, "Tokens.Composed.Light")]
    [InlineData(Palette.Still,      ThemeMode.Dark,  "Tokens.Still.Dark")]
    public void DictionaryName_ShouldNameTheResourceFile(Palette p, ThemeMode m, string expected)
    {
        // Arrange
        var choice = new AppearanceChoice(p, m);

        // Act
        var name = choice.DictionaryName;

        // Assert — this string IS the filename; a mismatch renders an unstyled screen.
        name.Should().Be(expected);
    }

    [Fact]
    public void DictionaryName_ForSystemMode_ShouldRequireAResolvedMode()
    {
        // Arrange — System is a preference, not a dictionary. Resolving it needs the OS,
        // so asking for its filename directly is a programming error rather than a default.
        var choice = new AppearanceChoice(Palette.Considered, ThemeMode.System);

        // Act
        var act = () => choice.DictionaryName;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run it and watch it fail.** `dotnet test tests/CoreChoice.App.Tests --no-restore`

- [ ] **Step 3: Implement the choice type**

`src/CoreChoice/Services/AppearanceChoice.cs` — note this file lives in the MAUI project but has NO MAUI dependency, so the test project can reach it by linking the file:

```csharp
namespace CoreChoice.Services;

public enum Palette { Considered, Composed, Still }

public enum ThemeMode { System, Light, Dark }

/// <summary>
/// What the person chose in Settings: a palette and a mode. The pair names exactly one token
/// dictionary, which is what makes adding a fourth palette a new file and no code.
/// </summary>
public sealed record AppearanceChoice(Palette Palette, ThemeMode Mode)
{
    public static AppearanceChoice Default => new(Palette.Considered, ThemeMode.Dark);

    /// <summary>
    /// The resource dictionary this choice resolves to. Throws for <see cref="ThemeMode.System"/>:
    /// that is a preference, not a dictionary, and resolving it requires asking the OS.
    /// </summary>
    public string DictionaryName => Mode switch
    {
        ThemeMode.Light => $"Tokens.{Palette}.Light",
        ThemeMode.Dark => $"Tokens.{Palette}.Dark",
        _ => throw new InvalidOperationException(
            "System mode must be resolved to Light or Dark before naming a dictionary."),
    };

    public AppearanceChoice Resolve(bool systemIsDark) =>
        Mode == ThemeMode.System
            ? this with { Mode = systemIsDark ? ThemeMode.Dark : ThemeMode.Light }
            : this;
}
```

Add this to `tests/CoreChoice.App.Tests/CoreChoice.App.Tests.csproj` so the test project compiles the file without referencing the MAUI project:

```xml
  <ItemGroup>
    <Compile Include="..\..\src\CoreChoice\Services\AppearanceChoice.cs" Link="Linked\AppearanceChoice.cs" />
  </ItemGroup>
```

- [ ] **Step 4: Write the six token dictionaries**

One file per palette and mode, all with the sixteen keys above. Shape, using Considered dark:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                    x:Class="CoreChoice.Resources.Styles.TokensConsideredDark">
  <Color x:Key="Bg">#0A0D0D</Color>
  <Color x:Key="BgElevated">#0E1313</Color>
  <Color x:Key="Surface">#121818</Color>
  <Color x:Key="SurfaceSunken">#0E1313</Color>
  <Color x:Key="Line">#1F2A29</Color>
  <Color x:Key="LineSoft">#161E1D</Color>
  <Color x:Key="Accent">#4FD1C5</Color>
  <Color x:Key="AccentBright">#7FE3D9</Color>
  <Color x:Key="AccentDim">#7FB8B2</Color>
  <Color x:Key="AccentInk">#04100F</Color>
  <Color x:Key="AccentLine">#2A514C</Color>
  <Color x:Key="AccentWash">#132422</Color>
  <Color x:Key="Ink">#EAF2F1</Color>
  <Color x:Key="InkSoft">#C6D5D3</Color>
  <Color x:Key="Muted">#879795</Color>
  <Color x:Key="Faint">#5A6968</Color>
</ResourceDictionary>
```

Each needs a matching `.xaml.cs` with `public partial class TokensConsideredDark : ResourceDictionary { public TokensConsideredDark() => InitializeComponent(); }`.

- [ ] **Step 5: Write shared styles**

`src/CoreChoice/Resources/Styles/Styles.xaml` — every colour is `DynamicResource`, never a literal, or switching the palette will leave elements behind. Define at minimum: `PageStyle` (background `Bg`), `TitleLabel` (FontFamily `LoraMedium`, 28, `Ink`), `BodyLabel` (`DMSans`, 15, `Muted`), `AdviceLabel` (`Lora`, 16, line height 1.6, `InkSoft`), `MicroLabel` (`DMSansMedium`, 11, letter-spaced, `Faint`), `PrimaryButton` (background `Accent`, text `AccentInk`, height 52, corner radius 10), `SecondaryButton` (border `Line`, text `Muted`, same metrics), `CardBorder` (background `Surface`, stroke `Line`, radius 10), `AccentCardBorder` (background `AccentWash`, stroke `AccentLine`).

- [ ] **Step 6: Implement ThemeService**

`src/CoreChoice/Services/ThemeService.cs`:

```csharp
using CoreChoice.Resources.Styles;

namespace CoreChoice.Services;

/// <summary>
/// Owns appearance. Swaps the single token dictionary every screen references through
/// DynamicResource, and remembers the choice across launches.
/// </summary>
public sealed class ThemeService
{
    private const string PaletteKey = "corechoice_palette";
    private const string ModeKey = "corechoice_mode";

    private ResourceDictionary? _active;

    public AppearanceChoice Current { get; private set; }

    public ThemeService()
    {
        var palette = (Palette)Preferences.Default.Get(PaletteKey, (int)Palette.Considered);
        var mode = (ThemeMode)Preferences.Default.Get(ModeKey, (int)ThemeMode.Dark);
        Current = new AppearanceChoice(palette, mode);

        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.RequestedThemeChanged += (_, _) => { if (Current.Mode == ThemeMode.System) Apply(); };
    }

    public void Set(AppearanceChoice choice)
    {
        Current = choice;
        Preferences.Default.Set(PaletteKey, (int)choice.Palette);
        Preferences.Default.Set(ModeKey, (int)choice.Mode);
        Apply();
    }

    public void Apply()
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;

        app.UserAppTheme = Current.Mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        var systemIsDark = app.RequestedTheme == AppTheme.Dark;
        var wanted = Build(Current.Resolve(systemIsDark));

        if (_active is not null)
            app.Resources.MergedDictionaries.Remove(_active);
        app.Resources.MergedDictionaries.Add(wanted);
        _active = wanted;
    }

    private static ResourceDictionary Build(AppearanceChoice resolved) => resolved.DictionaryName switch
    {
        "Tokens.Considered.Dark" => new TokensConsideredDark(),
        "Tokens.Considered.Light" => new TokensConsideredLight(),
        "Tokens.Composed.Dark" => new TokensComposedDark(),
        "Tokens.Composed.Light" => new TokensComposedLight(),
        "Tokens.Still.Dark" => new TokensStillDark(),
        "Tokens.Still.Light" => new TokensStillLight(),
        _ => new TokensConsideredDark(),
    };
}
```

Register it in `MauiProgram` as a singleton and call `Apply()` once during startup, before the first page is shown.

- [ ] **Step 7: Verify**

```bash
dotnet test tests/CoreChoice.App.Tests --no-restore
dotnet build src/CoreChoice/CoreChoice.csproj -c Debug -f net10.0-android --no-restore \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

**Prove the switch works before moving on:** temporarily set `Palette.Still` as the default in `AppearanceChoice.Default`, rebuild, deploy to the emulator, confirm the app renders lavender rather than teal, then revert. A theme system that only ever runs its default is one you will discover is broken when a user switches.

- [ ] **Step 8: Commit**

```bash
git add src/CoreChoice/Resources/Styles src/CoreChoice/Services tests/CoreChoice.App.Tests
git commit -m "Add the three palettes and the theme switcher

Six dictionaries with identical keys, swapped at runtime. Every screen
reads tokens through DynamicResource, so adding a fourth palette is one
file and no code."
```

---

### Task 4: Local storage

**Files:**
- Create: `src/CoreChoice/Data/LocalDbContext.cs`, `src/CoreChoice/Data/SqliteProfileRepository.cs`
- Test: `tests/CoreChoice.App.Tests/Data/ProfileRepositoryTests.cs` (linking the two files, as with `AppearanceChoice`)

**Interfaces:**
- Consumes: `IProfileRepository`, `StoredAnswers`, `OceanProfile`, `IpipScoring`
- Produces: `internal sealed class LocalDbContext : DbContext` with `DbSet<AnswerRow>` and `DbSet<ProfileRow>`; `internal sealed class SqliteProfileRepository(IDbContextFactory<LocalDbContext>) : IProfileRepository`

**Entities:** `AnswerRow(int ItemNumber PK, int Response, DateTimeOffset AnsweredAt)` and `ProfileRow(int Id PK = 1, int Openness, int Conscientiousness, int Extraversion, int Agreeableness, int Neuroticism, DateTimeOffset ScoredAt)` — a single-row table, because there is one person per phone.

Persist the two date columns as UTC ticks through a value conversion, exactly as the server does: EF Core's SQLite provider cannot translate `DateTimeOffset` comparisons, and a filter written against one silently evaluates client-side.

- [ ] **Step 1: Write the failing tests**

```csharp
using CoreChoice.Application;
using CoreChoice.Data;
using CoreChoice.Domain;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreChoice.App.Tests.Data;

public class ProfileRepositoryTests
{
    private static (IProfileRepository Repo, SqliteConnection Conn) Build()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<LocalDbContext>().UseSqlite(conn).Options;
        var factory = new TestFactory(options);
        using (var db = factory.CreateDbContext()) db.Database.EnsureCreated();
        return (new SqliteProfileRepository(factory), conn);
    }

    private sealed class TestFactory(DbContextOptions<LocalDbContext> o) : IDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext() => new(o);
    }

    [Fact]
    public async Task SaveAnswerAsync_ThenLoad_ShouldReturnTheAnswer()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        await repo.SaveAnswerAsync(17, 4);
        var answers = await repo.LoadAnswersAsync();

        // Assert
        answers.Responses.Should().ContainKey(17).WhoseValue.Should().Be(4);
        conn.Dispose();
    }

    [Fact]
    public async Task SaveAnswerAsync_WhenTheSameItemIsAnsweredTwice_ShouldKeepTheLatest()
    {
        // Arrange — people change their mind mid-test and tap a different number.
        var (repo, conn) = Build();
        await repo.SaveAnswerAsync(17, 4);

        // Act
        await repo.SaveAnswerAsync(17, 2);

        // Assert
        (await repo.LoadAnswersAsync()).Responses[17].Should().Be(2);
        conn.Dispose();
    }

    [Fact]
    public async Task LoadProfileAsync_BeforeTheTestIsScored_ShouldReturnNone()
    {
        // Arrange
        var (repo, conn) = Build();

        // Act
        var profile = await repo.LoadProfileAsync();

        // Assert — the absent case is a domain value, never a null for callers to remember.
        profile.IsPresent.Should().BeFalse();
        conn.Dispose();
    }

    [Fact]
    public async Task SaveProfileAsync_ThenLoad_ShouldRoundTripEveryTrait()
    {
        // Arrange
        var (repo, conn) = Build();
        var saved = new OceanProfile(
            TraitScore.From(88), TraitScore.From(45), TraitScore.From(62),
            TraitScore.From(70), TraitScore.From(58));

        // Act
        await repo.SaveProfileAsync(saved);
        var loaded = await repo.LoadProfileAsync();

        // Assert
        loaded.IsPresent.Should().BeTrue();
        loaded.Openness.Value.Should().Be(88);
        loaded.Neuroticism.Value.Should().Be(58);
        conn.Dispose();
    }

    [Fact]
    public async Task SaveProfileAsync_Twice_ShouldReplaceRatherThanAccumulate()
    {
        // Arrange — retaking the test must not leave two profiles on the phone.
        var (repo, conn) = Build();
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(10), TraitScore.From(10), TraitScore.From(10),
            TraitScore.From(10), TraitScore.From(10)));

        // Act
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(90), TraitScore.From(90), TraitScore.From(90),
            TraitScore.From(90), TraitScore.From(90)));

        // Assert
        (await repo.LoadProfileAsync()).Openness.Value.Should().Be(90);
        conn.Dispose();
    }

    [Fact]
    public async Task ClearAnswersAsync_ShouldRemoveEveryAnswerButLeaveTheProfile()
    {
        // Arrange — retaking clears answers; the old profile stays until the new one is scored,
        // so a person who abandons a retake is not left with nothing.
        var (repo, conn) = Build();
        await repo.SaveAnswerAsync(1, 3);
        await repo.SaveProfileAsync(new OceanProfile(
            TraitScore.From(50), TraitScore.From(50), TraitScore.From(50),
            TraitScore.From(50), TraitScore.From(50)));

        // Act
        await repo.ClearAnswersAsync();

        // Assert
        (await repo.LoadAnswersAsync()).Responses.Should().BeEmpty();
        (await repo.LoadProfileAsync()).IsPresent.Should().BeTrue();
        conn.Dispose();
    }
}
```

- [ ] **Step 2: Run and watch it fail.**
- [ ] **Step 3: Implement `LocalDbContext` and `SqliteProfileRepository`** to satisfy exactly those behaviours. `SaveAnswerAsync` upserts on `ItemNumber`; `SaveProfileAsync` upserts the single row with `Id = 1`; `LoadProfileAsync` returns `OceanProfile.None` when the row is absent.
- [ ] **Step 4: Run green**, and confirm the backend suites are untouched.
- [ ] **Step 5: Commit** — `"Add local storage for answers and the profile"`, mentioning that answers persist per item so an abandoned test resumes, and that dates use the ticks conversion for the same reason the server does.

---

### Task 5: The backend client

**Files:**
- Create: `src/CoreChoice/Services/SecureStorageDeviceIdentity.cs`, `src/CoreChoice/Services/CoreChoiceApiClient.cs`, `src/CoreChoice/Services/ApiOptions.cs`
- Test: `tests/CoreChoice.App.Tests/Services/ApiClientTests.cs`

**Interfaces:**
- Consumes: `IDeviceIdentity`, `IDecisionAdvisor`, `ICoinLedgerClient`, `DecisionRequest`, `DecisionResult`, `OceanProfile`, `Dilemma`, `DecisionWeight`, `PersonaId`
- Produces: `internal sealed class CoreChoiceApiClient(HttpClient, IDeviceIdentity, IOptions<ApiOptions>) : IDecisionAdvisor, ICoinLedgerClient` plus `Task<IReadOnlyList<PersonaSummary>> GetPersonasAsync(ct)` and `public sealed record PersonaSummary(string Id, string DisplayName, string Description)`
- `SecureStorageDeviceIdentity` is PurePrep's implementation with the storage key changed to `corechoice_device_id` — an anonymous GUID in secure storage, falling back to `Preferences` where secure storage is unavailable, cached in memory behind a semaphore.

**The wire contract, fixed by the backend:**

| Call | Shape |
|---|---|
| `POST /api/coins/ensure` | `{deviceId}` → `{deviceId, balance}` |
| `GET /api/coins/{deviceId}` | → `{deviceId, balance}` |
| `POST /api/coins/profile-grant` | `{deviceId}` → `{deviceId, balance, granted, reason}` |
| `GET /api/personas` | → `[{id, displayName, description}]` |
| `POST /api/decisions` | `{deviceId, optionA, optionB, context, persona, weight, profile:{openness,conscientiousness,extraversion,agreeableness,neuroticism}}` → `{recommendation, confidence, reasoning[], optionA{strengths[],risks[]}, optionB{strengths[],risks[]}, personalityNote, personalized, balance}` |

Failures the client must translate rather than leak: **402** → `InsufficientCoinsException`, **502** → `MalformedAdvisorResponseException`, **503** → `DecisionUnavailableException`, **429** → `DecisionUnavailableException` with a rate-limit message. All three exception types already exist in `CoreChoice.Application`.

`profile` is `null` in the body when `OceanProfile.None` — that is what makes the answer unpersonalised, and the server decides, not the client.

- [ ] **Step 1: Write the failing tests** using the `StubHttpMessageHandler` already in `tests/CoreChoice.Core.Tests/TestSupport/` — copy it into the app test project rather than referencing across test projects. Cover: a successful decision maps every field including `Personalized` and `Balance`; a 402 throws `InsufficientCoinsException`; a 503 throws `DecisionUnavailableException`; a 502 throws `MalformedAdvisorResponseException`; `OceanProfile.None` sends a null `profile`; a scored profile sends five integers; `EnsureSeededAsync` posts the device id and reads the balance back; `ClaimProfileGrantAsync` maps `granted` and `reason`.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement** `ApiOptions` (a `BaseUrl`, defaulting to `https://api.corechoice.lechdigital.nl/`), `SecureStorageDeviceIdentity`, and `CoreChoiceApiClient`. Register the client as a typed `HttpClient` with a 60-second timeout — the decision endpoint calls a language model and 30 seconds is not always enough.
- [ ] **Step 4: Run green.**
- [ ] **Step 5: Prove the failure mapping bites.** Change the 402 branch to throw a plain `HttpRequestException`, run the tests, confirm the insufficient-coins test fails, revert. A client that leaks transport exceptions makes every screen show the same unhelpful error.
- [ ] **Step 6: Commit** — `"Add the backend client"`, noting that HTTP failures are translated into the application exceptions the view models already understand.

---

### Task 6: Verdict labels

**Files:**
- Create: `src/CoreChoice/Services/VerdictLabel.cs`
- Test: `tests/CoreChoice.App.Tests/Services/VerdictLabelTests.cs` (link the source file)

**Interfaces:**
- Produces: `public static class VerdictLabel` with `static string For(int confidence)` and `static bool IsStrong(int confidence)`

**This is where a product decision lives in code.** The backend returns a confidence integer. The user never sees it. Percentages and progress bars imply something is missing from a hundred — to someone already prone to over-deciding, "75%" reads as a one-in-four chance of ruining their life, and sends them back to searching. Four labels replace it:

| Confidence | Label | Meaning |
|---|---|---|
| 80 and above | `Clear direction` | One option is well ahead |
| 65–79 | `Strong case` | A solid recommendation with real reasons |
| 50–64 | `Slight edge` | Close; this one is ahead, not by much |
| below 50 | `Genuinely close` | Too near to separate; either is defensible |

"Strong case" describes the argument the advisor made, deliberately **not** a match with the person's profile — nothing here measures alignment, and the wording must not imply it does.

- [ ] **Step 1: Write the failing test** covering every boundary: 100, 80, 79, 65, 64, 50, 49, 0 — and that no returned label contains a digit or a percent sign.
- [ ] **Step 2: Run and watch it fail.**
- [ ] **Step 3: Implement** with a switch expression over the bands.
- [ ] **Step 4: Run green.**
- [ ] **Step 5: Commit** — `"Add the four verdict labels"`, with the reasoning in the message.

---

### Task 7: The personality test

**Files:**
- Create: `src/CoreChoice/Presentation/TestIntroViewModel.cs`, `TestIntroPage.xaml(.cs)`, `TestViewModel.cs`, `TestPage.xaml(.cs)`, `ProfileViewModel.cs`, `ProfilePage.xaml(.cs)`
- Test: `tests/CoreChoice.App.Tests/Presentation/TestViewModelTests.cs`, `ProfileViewModelTests.cs`

**Interfaces:**
- Consumes: `IProfileRepository`, `ICoinLedgerClient`, `IpipItemBank`, `IpipScoring`, `OceanProfile`, `IncompleteProfileException`
- Produces:
  - `TestViewModel` — `IReadOnlyList<TestItem> CurrentPage`, `int AnsweredCount`, `int TotalCount`, `bool CanAdvance`, `Task LoadAsync(ct)`, `Task AnswerAsync(int itemNumber, int response, ct)`, `Task<bool> AdvanceAsync(ct)` returning true when the test is complete
  - `public sealed record TestItem(int Number, string Text, int? Response)`
  - `ProfileViewModel` — `OceanProfile Profile`, `string Summary`, `int Balance`, `string? GrantMessage`, `Task LoadAsync(ct)`

**Three items per page, not five.** At 390px with 44px targets, five Likert rows means scrolling mid-question, and scrolling is where people abandon. `CurrentPage` returns the first three unanswered items in bank order.

**`ProfileViewModel` must not depend on `ICoinLedgerClient` for anything the result needs.** It takes the ledger only to claim the grant and show the new balance; scoring and display happen regardless of whether that call succeeds, because the test result is free and must render on a phone with no signal. If the grant call throws, the profile still shows and `GrantMessage` stays null.

- [ ] **Step 1: Write the failing tests.** Cover: `LoadAsync` on a fresh install returns the first three items with null responses; answering one persists it through the repository; `CurrentPage` skips answered items; `AnsweredCount` reflects storage; `AdvanceAsync` returns false until all fifty are answered and true at fifty; scoring an incomplete set throws `IncompleteProfileException` rather than scoring what it has; `ProfileViewModel.LoadAsync` renders the profile when the grant call throws; and the free-result invariant — with a ledger stub that throws on every call, the profile is still scored, saved and exposed.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement the view models.** They must not reference MAUI types, so the test project can compile them as linked files.
- [ ] **Step 4: Implement the three pages in XAML**, matching artboards `1 · Before the test`, `2 · The 50 items` and `3 · Your profile` on the published canvas. Every colour through `DynamicResource`; Lora for what the app says, DM Sans for chrome; the Likert buttons at 46px, the "Pause" control at 44px. Traits render as a dot positioned on a line, never a filled bar — a fill reads as "how much you have out of the maximum", which is wrong for personality and reintroduces the same "something is missing" feeling the verdict labels removed.
- [ ] **Step 5: Run green**, build, and deploy to a device.
- [ ] **Step 6: Prove the resume works, by hand.** Answer a dozen items, force-stop the app from Android settings, reopen it. It must return to item thirteen with the previous answers intact. This is the single behaviour that decides whether a low-conscientiousness user ever finishes, and it cannot be verified from a unit test alone.
- [ ] **Step 7: Commit.**

---

### Task 8: Asking a question

**Files:**
- Create: `src/CoreChoice/Presentation/DilemmaViewModel.cs`, `DilemmaPage.xaml(.cs)`, `PersonaViewModel.cs`, `PersonaPage.xaml(.cs)`, `src/CoreChoice/Platforms/Android/AndroidVoiceDictation.cs`
- Test: `tests/CoreChoice.App.Tests/Presentation/DilemmaViewModelTests.cs`, `PersonaViewModelTests.cs`

**Interfaces:**
- Consumes: `IVoiceDictation`, `Dilemma`, `DecisionWeight`, `PersonaId`, the API client's `GetPersonasAsync`
- Produces:
  - `DilemmaViewModel` — `string OptionA/OptionB/Context`, `int Weight`, `PersonaSummary? Persona`, `bool CanSubmit`, `bool IsDictationAvailable`, `Task DictateAsync(DilemmaField field, ct)`, `Task<DecisionRequest?> BuildRequestAsync(ct)`
  - `PersonaViewModel` — `PersonaSummary? Suggested`, `string SuggestionReason`, `IReadOnlyList<PersonaSummary> Others`, `Task LoadAsync(int weight, ct)`

**The persona screen surfaces one suggestion, never a grid of six.** Choice overload produces anxiety and deferral — the exact state this product exists to end — and putting six options in front of someone who came here *because* of too many options is the app undermining itself. The other five are listed below, one tap away.

The suggestion carries its reason (`"you weighted this five out of five, and you are asking about years"`). A recommendation that explains itself gets accepted; one that simply appears gets re-decided.

Suggestion rule, deterministic and local — no server call: weight 5 → `the-long-view`; weight 4 → `pure-logic`; weight 3 → `the-pragmatist`; weight 1–2 → `gut-check`. If the person has taken the test and scores high on neuroticism (≥ 67), prefer `warm-support` at weights 1–3.

**Microphone permission is requested when the mic is tapped, never at launch.** An app that asks for the microphone on first open gets refused, and the refusal is permanent in practice.

- [ ] **Step 1: Write the failing tests.** Cover: `CanSubmit` is false until both options are non-empty; an option over `Dilemma.MaxOptionLength` blocks submission rather than throwing at build time; `BuildRequestAsync` maps the stored profile and returns `OceanProfile.None` when the test was never taken; whitespace-only context becomes null; each weight maps to the documented persona; the neuroticism override applies only at weights 1–3; `IsDictationAvailable` is false when the port reports unsupported, and `DictateAsync` is a no-op in that case rather than throwing.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement both view models** (no MAUI types).
- [ ] **Step 4: Implement `AndroidVoiceDictation`** against Android's `SpeechRecognizer`, following `PurePrep/src/PurePrep/Platforms/Android/VoiceCommandListener.cs`: check `SpeechRecognizer.IsRecognitionAvailable`, request `RECORD_AUDIO` at the point of use, recognise free-form speech, return the best result or null on cancel or error. Never throw at the view model — an unavailable microphone hides the button.
- [ ] **Step 5: Implement the two pages**, matching artboards `4 · The dilemma` and `5 · Who answers`.
- [ ] **Step 6: Run green**, build, deploy.
- [ ] **Step 7: Test dictation on a real device.** The emulator's microphone does not represent a phone. Confirm the permission prompt appears on first tap and that denying it hides the button rather than breaking the screen.
- [ ] **Step 8: Commit.**

---

### Task 9: The answer

**Files:**
- Create: `src/CoreChoice/Presentation/AnalysisViewModel.cs`, `AnalysisPage.xaml(.cs)`, `LoadingView.xaml(.cs)`
- Test: `tests/CoreChoice.App.Tests/Presentation/AnalysisViewModelTests.cs`

**Interfaces:**
- Consumes: `IDecisionAdvisor`, `VerdictLabel`, `DecisionAnalysis`, `InsufficientCoinsException`, `DecisionUnavailableException`, `MalformedAdvisorResponseException`
- Produces: `AnalysisViewModel` — `bool IsWorking`, `string WaitingLine`, `DecisionAnalysis? Analysis`, `string Verdict`, `int Balance`, `string? ErrorMessage`, `bool CanRetry`, `Task AskAsync(DecisionRequest, ct)`

**The waiting line is built from the local profile, not the server.** It names the trait actually being weighed — *"Weighing your openness against the pull of a steady income"* — which is why it can appear the instant the request is sent. With no profile it falls back to a neutral line and never invents a trait.

**Error handling maps to what the person should do, not to a status code:**

| Failure | Message | Retry offered |
|---|---|---|
| `InsufficientCoinsException` | "You are out of analyses." plus a route to Coins | No |
| `DecisionUnavailableException` | "The advisor could not be reached. Your coin was not spent." | Yes |
| `MalformedAdvisorResponseException` | "That came back unusable. Your coin was not spent." | Yes |
| anything else | "Something went wrong. Your coin was not spent." | Yes |

Say the coin is safe **explicitly**, in the message. The backend refunds on every failure after the spend, and a person who cannot see that happened assumes the worst — the assumption costs more trust than the error does.

- [ ] **Step 1: Write the failing tests.** Cover: a successful call exposes the analysis, the balance, and `Verdict == "Strong case"` for a confidence of 75; `IsWorking` is true during the call and false after, including when it throws; each exception produces its documented message and retry flag; `WaitingLine` names a trait when a profile is present and does not when it is absent; `Verdict` never contains a digit.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement the view model.**
- [ ] **Step 4: Implement the pages**, matching artboards `6 · While it thinks` and `7 · The answer`. No percentage, no progress bar for the verdict — the label badge only. The closing line, *"You can sit with this. It will be here in the morning,"* stays: it is the permission to stop that a maximiser does not give themselves.
- [ ] **Step 5: Run green**, build, deploy, and ask a real question end to end against the running backend.
- [ ] **Step 6: Prove the coin message is honest.** Point the app at a backend URL that refuses connections, ask a question, and confirm the error says the coin was not spent — then check the balance through the real backend and confirm it genuinely was not. A reassuring message that is false is worse than no message.
- [ ] **Step 7: Commit.**

---

### Task 10: Analyses and the store

**Files:**
- Create: `src/CoreChoice/Presentation/CoinsViewModel.cs`, `CoinsPage.xaml(.cs)`, `src/CoreChoice/Platforms/Android/PlayBillingService.cs`
- Test: `tests/CoreChoice.App.Tests/Presentation/CoinsViewModelTests.cs`

**Interfaces:**
- Consumes: `IBillingService`, `ICoinLedgerClient`, `AnalysisPack`, `PurchaseTicket`
- Produces: `CoinsViewModel` — `int Balance`, `IReadOnlyList<AnalysisPack> Packs`, `bool CanBuy`, `bool PricesAreFromStore`, `string? Message`, `Task LoadAsync(ct)`, `Task BuyAsync(string productId, ct)`

**Product ids** (these are what you create in the Play Console; the app never invents them):
`corechoice.analyses.10`, `corechoice.analyses.30`, `corechoice.analyses.100`.

**Prices come from Google Play, always.** `GetPacksAsync` queries product details and reads `OneTimePurchaseOfferDetailsList[0].FormattedPrice` — localised and tax-inclusive, the exact string charged at checkout. VAT differs per country, so a hardcoded figure is wrong everywhere except one. The euro labels in `FallbackPacks` appear only when the store cannot answer: offline, billing unavailable, or the products not yet live. Surface that state with `PricesAreFromStore` so the UI can be honest rather than showing a stale number as though it were real.

**Purchase order, which is not negotiable:**
1. `BuyAsync` runs the Play flow and returns an **un-consumed** ticket.
2. The backend redeems it and grants the coins.
3. Only then `ConsumeAsync` releases it for repurchase.

Consuming before the grant means a person pays and receives nothing when the network drops between the two — and the purchase is gone, because a consumed product cannot be re-redeemed.

**This task needs a backend endpoint that does not exist yet.** The vertical-slice backend deliberately deferred Play validation, so there is no `POST /api/billing/redeem`. Two ways forward, and this is a decision for the repo owner rather than the implementer:
- **(a) Ship without purchasing.** Build the screen, show real store prices, and disable the buy buttons behind `IsSupported`. The app is fully usable on the ten free analyses. Billing becomes its own plan alongside the server-side Play validator.
- **(b) Add the endpoint now.** Port PurePrep's `BillingEndpoint`, `AndroidPublisherPlayValidator`, `PlayOptions` and the `ProcessedPurchase` table, including its fail-closed rule: in Production the server refuses to start without a readable Google service-account key, because running without one makes any forged purchase token worth real coins. That is a meaningful piece of work with its own review.

**The repo owner has chosen (a).** Build the screen with real Play prices and the buy buttons
disabled; do NOT stop to ask, and do not build any part of (b). Server-side purchase validation is
the next round of work, after internal testing and before closed testing — which is also when the
Play Console products get their prices set. Until those products are live, `GetPacksAsync` falls
back to placeholder labels, and that is expected rather than a defect.

- [ ] **Step 1: Write the failing tests.** Cover: `LoadAsync` uses store prices when the billing port returns them and sets `PricesAreFromStore` true; it falls back to `FallbackPacks` and sets that flag false when the port throws; `CanBuy` is false when `IsSupported` is false; a cancelled purchase (null ticket) leaves the balance untouched and shows no error; and — the important one — **a purchase is never consumed when the redemption step fails**, verified with a billing stub that records whether `ConsumeAsync` was called.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement `CoinsViewModel`.**
- [ ] **Step 4: Implement `PlayBillingService`** following `PurePrep/src/PurePrep/Platforms/Android/PlayBillingService.cs`: connect a `BillingClient`, `QueryProductDetailsAsync` per product id, read `FormattedPrice`, launch the flow, and leave purchases un-consumed.
- [ ] **Step 5: Implement the page**, matching artboard `8 · Analyses`. The "No subscription, nothing renews" line stays at the bottom — it answers the suspicion the whole category has earned.
- [ ] **Step 6: Run green**, build, deploy.
- [ ] **Step 7: Confirm the fallback is visible, not silent.** Run with the device offline and check the screen shows placeholder prices while making clear they are not live figures. A stale price presented as real is the one thing that turns a pricing bug into a complaint.
- [ ] **Step 8: Commit.**

---

### Task 11: Appearance, navigation, and the first full run

**Files:**
- Create: `src/CoreChoice/Presentation/SettingsViewModel.cs`, `SettingsPage.xaml(.cs)`
- Modify: `src/CoreChoice/AppShell.xaml(.cs)`, `src/CoreChoice/MauiProgram.cs`
- Test: `tests/CoreChoice.App.Tests/Presentation/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `ThemeService`, `AppearanceChoice`
- Produces: `SettingsViewModel` — `Palette SelectedPalette`, `ThemeMode SelectedMode`, `IReadOnlyList<PaletteOption> Palettes`, `void Select(Palette)`, `void Select(ThemeMode)`; `public sealed record PaletteOption(Palette Value, string Name, string Description, string GroundHex, string AccentHex)`

Palette copy: Considered — "Clear and awake"; Composed — "Cool and quiet"; Still — "Soft and low-contrast".

**The picker lives in Settings, never in onboarding.** A person who came here because choice exhausts them should not meet a six-way appearance decision before they have used the app once.

**Routing.** First launch goes to the test intro; a returning person with a profile goes to the dilemma screen. The test is never a gate — the intro screen offers "Not now, ask a question first", and an unprofiled person can still ask, receives a generic answer, and is offered the test afterwards, when the contrast makes the argument for it.

Register in `MauiProgram`: `ThemeService`, `IDeviceIdentity`, `IProfileRepository` with its `IDbContextFactory<LocalDbContext>`, the typed `CoreChoiceApiClient` as both `IDecisionAdvisor` and `ICoinLedgerClient`, `IVoiceDictation`, `IBillingService`, and every view model and page as transient.

- [ ] **Step 1: Write the failing tests** — selecting a palette persists it through `ThemeService`; selecting a mode persists it; the three options carry distinct swatch colours; `SelectedPalette` reflects what the service already holds on construction.
- [ ] **Step 2: Run and watch them fail.**
- [ ] **Step 3: Implement the view model and the page**, matching artboard `9 · Appearance`.
- [ ] **Step 4: Wire the shell and DI. Create the database on first launch** and call `ThemeService.Apply()` before the first page renders, or the app flashes unstyled.
- [ ] **Step 5: Run the full suite** — app tests green, Core 77, Server 85, build at 0 warnings.
- [ ] **Step 6: Build a debug APK and hand it to the repo owner.**

```bash
dotnet build src/CoreChoice/CoreChoice.csproj -c Debug -f net10.0-android --no-restore \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
ls -la src/CoreChoice/bin/Debug/net10.0-android/*.apk
```

There is no emulator or device on this machine — by the owner's decision, nothing was downloaded.
Report the APK's full path and STOP. The walk below is theirs to perform, on their own phone, and
the checkpoint is not met until they have done it.

- [ ] **Step 7: The walk, on the owner's device, against the real backend.** Fresh install through to an answer:
  1. First launch lands on the test intro and states the free-result promise before any question.
  2. Answer all fifty. Force-stop somewhere in the middle and confirm it resumes.
  3. The profile appears with five traits and the grant message; the balance reads ten.
  4. Ask a real question. The waiting line names one of your traits.
  5. The answer shows a verdict label and no percentage anywhere.
  6. The balance reads nine.
  7. Switch palette in Settings to Still, then Composed. Every screen follows, including ones already visited.
  8. Switch to Light. Every screen is legible; nothing is a dark-on-dark ghost.
- [ ] **Step 8: Commit.**

> ## CHECKPOINT — stop here
>
> The app is complete and running. Do not build a signed `.aab`, do not touch versioning, do not deploy the backend. Report what works, what does not, and anything discovered on the device that the plan did not anticipate. The next two steps are the repo owner's call.

---

### Task 12 (only on explicit approval): the signed release

Not to be started until the checkpoint above has been reviewed and the go-ahead given.

**The keystore.** Created once, kept outside the repository, never committed:

```bash
mkdir -p ~/keystores
"$JAVA_HOME/bin/keytool" -genkeypair -v \
  -keystore ~/keystores/corechoice-upload.jks \
  -alias corechoice -keyalg RSA -keysize 2048 -validity 10000 \
  -dname "CN=adziusmaster, OU=CoreChoice, O=CoreChoice, L=Warsaw, C=PL"
```

Store the password in `~/keystores/corechoice-upload.pass.txt` with mode 600. **Losing this file means never being able to update the app under this listing again** — Play accepts uploads signed by it and nothing else. Back it up somewhere that is not this machine.

**csproj signing block**, mirroring PurePrep: active only for Release Android builds when `COINCHOICE_KEYSTORE_PASS` is set, with the keystore path and alias overridable and no secret in source.

**Building the bundle:**

```bash
export CORECHOICE_KEYSTORE_PASS=$(cat ~/keystores/corechoice-upload.pass.txt)
dotnet build src/CoreChoice/CoreChoice.csproj -c Release -f net10.0-android --no-restore \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidPackageFormat=aab \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

`UseDefaultPublishRuntimeIdentifier=false` is required — without it a Release build appends the host RID and fails with `XA0035 osx-arm64`.

Verify before uploading:

```bash
"$JAVA_HOME/bin/jarsigner" -verify \
  src/CoreChoice/bin/Release/net10.0-android/com.adziusmaster.corechoice-Signed.aab
```

**Before every upload** bump `<ApplicationVersion>` — Play rejects a version code it has already seen. `<ApplicationDisplayVersion>` changes only for a real release.

**Products must exist in the Play Console before prices appear in the app.** Create `corechoice.analyses.10`, `.30` and `.100` as consumable in-app products and set their prices there. Until they are live and the app has been through a review track, `GetPacksAsync` will fall back to placeholder labels — which is expected, not a bug.

---

## Done when

- The app installs, runs, and completes a real decision against the live backend.
- The test resumes after a force-stop.
- No percentage or progress bar appears anywhere in the verdict.
- All three palettes render correctly in both modes on every screen.
- Coin prices come from Play when it answers, and are visibly marked as placeholders when it does not.
- Core 77 and Server 85 still pass; the build has 0 warnings.

## Deliberately not in this plan

- Deploying the backend to Hetzner, and the DNS record it needs.
- Non-root hardening of the container.
- The rate-limit partitioning decision for mobile clients behind carrier-grade NAT.
- Server-side Play purchase validation, unless Task 10 option (b) is approved.
