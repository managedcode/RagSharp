# Setup .NET 10

## macOS

```bash
brew install --cask dotnet-sdk-preview

dotnet --info
dotnet --list-sdks
```

## Ubuntu/Debian

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

dotnet --info
dotnet --list-sdks
```

## Windows (PowerShell)

```powershell
winget install Microsoft.DotNet.SDK.Preview

dotnet --info
dotnet --list-sdks
```
