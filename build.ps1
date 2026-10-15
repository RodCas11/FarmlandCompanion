# Compatibility entry point. Every build uses the same safe latest publication flow.
& (Join-Path $PSScriptRoot 'build-latest.ps1') @args
if (!$?) { exit 1 }
