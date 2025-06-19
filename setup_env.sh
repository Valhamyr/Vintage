#!/usr/bin/env bash
set -euo pipefail

# Create Python virtual environment if it doesn't exist
if [ ! -d "env" ]; then
    python3 -m venv env
fi

# Activate the virtual environment
source env/bin/activate

# Upgrade pip and install Python dependencies
pip install --upgrade pip
pip install -r requirements.txt

echo "Python dependencies installed."

# Verify .NET SDK
if ! command -v dotnet > /dev/null 2>&1; then
    echo "dotnet CLI not found. Please install the .NET SDK for building C# mods."
else
    echo "dotnet CLI detected: $(dotnet --version)"
fi
