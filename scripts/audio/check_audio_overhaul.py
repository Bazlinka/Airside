#!/usr/bin/env python3
"""Bounded, package-free audio numeric checks. This does not compile Unity presentation.

Requires a preinstalled .NET 8 SDK; --dotnet accepts its path. Writes only ignored work/.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default=shutil.which('dotnet'))
    args = parser.parse_args()
    if not args.dotnet: raise SystemExit('No preinstalled dotnet SDK; audio pure checks unverified.')
    out = ROOT / 'work/audio-numeric-check'
    out.mkdir(parents=True, exist_ok=True)
    feed = out / 'empty-feed'
    feed.mkdir(exist_ok=True)
    files = [ROOT / 'game/Airside/Assets/Airside/Domain/**/*.cs',
             ROOT / 'game/Airside/Assets/Airside/Simulation/**/*.cs']
    files += [ROOT / 'game/Airside/Assets/Airside/Presentation' / name for name in
              ['AircraftAudioMix.cs', 'AircraftAudioProfiles.Generated.cs', 'EngineVoice.cs',
               'HudSounds.cs', 'AudioPeakLimiter.cs']]
    files += [ROOT / 'docs/testing/audio-overhaul-2026-10-08/PureAudioCheck.cs']
    project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>' \
              '<TargetFramework>net8.0</TargetFramework><ImplicitUsings>disable</ImplicitUsings>' \
              '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><NuGetAudit>false</NuGetAudit>' \
              '</PropertyGroup><ItemGroup>'
    project += ''.join(f'<Compile Include="{escape(str(p))}" />' for p in files)
    project += '</ItemGroup></Project>'
    csproj = out / 'AudioNumeric.csproj'
    csproj.write_text(project)
    subprocess.run([args.dotnet, 'restore', str(csproj), '--source', str(feed), '--verbosity', 'quiet'],
                   cwd=ROOT, check=True, timeout=20)
    subprocess.run([args.dotnet, 'run', '--project', str(csproj), '--no-restore', '--verbosity', 'quiet'],
                   cwd=ROOT, check=True, timeout=40)

if __name__ == '__main__': main()
