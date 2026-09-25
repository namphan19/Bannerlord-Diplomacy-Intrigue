#!/usr/bin/env bash
# Compile the module without a Bannerlord install - for a Linux box or a cloud session, where
# build.ps1 cannot run because there is no game folder to reference.
#
# It fetches the v1.4.8 reference assemblies (BUTR's Bannerlord.ReferenceAssemblies.Core) and the
# four framework assemblies at the versions SubModule.xml depends on, lays them out as a stand-in
# game folder, and builds against it into a temporary directory. Nothing in module/ is touched.
#
# What it proves: the code compiles against the real v1.4.8 public API. What it does not: that the
# game loads the assembly (tools/LoadProbe, which needs the real install) or that anything works
# in game. A clean compile here is not a verification.
#
# Needs: dotnet SDK 8+, curl, unzip. Usage: scripts/compile-check.sh [cache dir]
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
CACHE="${1:-${TMPDIR:-/tmp}/di-compile-check}"
GAME="$CACHE/game"
BIN="$GAME/bin/Win64_Shipping_Client"
MODS="$GAME/Modules"
OUT="$CACHE/out"

# Versions pinned to module/DiplomacyIntrigue/SubModule.xml. The reference-assembly build number is
# the one BUTR published for v1.4.8.
REFS_VERSION="1.4.8.119303"
HARMONY_VERSION="2.4.2"
BUTTERLIB_VERSION="2.10.4"
UIEXTENDEREX_VERSION="2.13.2"
MCM_VERSION="5.11.4"

fetch() { # id version -> unpacked into $CACHE/pkgs/id
  local id="$1" version="$2" dir="$CACHE/pkgs/$1"
  [ -d "$dir" ] && return 0
  mkdir -p "$dir"
  curl -fsSL -o "$dir.nupkg" "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
  unzip -oq "$dir.nupkg" -d "$dir"
}

fetch bannerlord.referenceassemblies.core "$REFS_VERSION"
fetch lib.harmony "$HARMONY_VERSION"
fetch bannerlord.butterlib "$BUTTERLIB_VERSION"
fetch bannerlord.uiextenderex "$UIEXTENDEREX_VERSION"
fetch bannerlord.mcm "$MCM_VERSION"

mkdir -p "$BIN" \
  "$MODS/Bannerlord.Harmony/bin/Win64_Shipping_Client" \
  "$MODS/Bannerlord.ButterLib/bin/Win64_Shipping_Client" \
  "$MODS/Bannerlord.UIExtenderEx/bin/Win64_Shipping_Client" \
  "$MODS/Bannerlord.MBOptionScreen/bin/Win64_Shipping_Client"
cp "$CACHE"/pkgs/bannerlord.referenceassemblies.core/ref/net472/*.dll "$BIN/"
cp "$CACHE"/pkgs/lib.harmony/lib/net472/0Harmony.dll "$MODS/Bannerlord.Harmony/bin/Win64_Shipping_Client/"
cp "$CACHE"/pkgs/bannerlord.butterlib/lib/net472/*.dll "$MODS/Bannerlord.ButterLib/bin/Win64_Shipping_Client/"
cp "$CACHE"/pkgs/bannerlord.uiextenderex/lib/netstandard2.0/*.dll "$MODS/Bannerlord.UIExtenderEx/bin/Win64_Shipping_Client/"
cp "$CACHE"/pkgs/bannerlord.mcm/lib/netstandard2.0/*.dll "$MODS/Bannerlord.MBOptionScreen/bin/Win64_Shipping_Client/"

dotnet build "$REPO/src/DiplomacyIntrigue/DiplomacyIntrigue.csproj" -c Release \
  -p:GameFolder="$GAME" -p:OutputPath="$OUT/" -p:BaseOutputPath="$OUT/"
