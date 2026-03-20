# Install .NET 8 on Linux and macOS

This page is for building `mdz` from source. You need the .NET 8 SDK for development and local builds.
If you install from prebuilt release binaries, you do not need to install .NET.

## Official Microsoft docs

- Linux overview: https://learn.microsoft.com/en-us/dotnet/core/install/linux
- macOS: https://learn.microsoft.com/en-us/dotnet/core/install/macos
- Main install landing page: https://learn.microsoft.com/en-us/dotnet/core/install/

## Linux

Recommended: use your distro-specific Microsoft instructions from the Linux overview page above.

After install, verify:

```bash
dotnet --info
dotnet --version
```

You should see an SDK version starting with `8.`.

## macOS

Option 1 (recommended): install from Microsoft official installer pages:

- https://dotnet.microsoft.com/download/dotnet/8.0

Choose the SDK for your architecture:

- Apple Silicon: Arm64
- Intel: x64

Option 2: install with Homebrew:

```bash
brew install --cask dotnet-sdk
```

Then verify:

```bash
dotnet --info
dotnet --version
```

If `dotnet` is not found after install, open a new terminal session and try again.
