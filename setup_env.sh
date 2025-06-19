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
if ! pip install -r requirements.txt; then
    if [ -d "wheels" ]; then
        echo "Network install failed. Attempting offline install from ./wheels" >&2
        pip install --no-index --find-links ./wheels -r requirements.txt
    else
        echo "Package installation failed and no ./wheels directory found" >&2
        exit 1
    fi
fi

echo "Python dependencies installed."

# Verify .NET SDK
if ! command -v dotnet > /dev/null 2>&1; then
    echo "dotnet CLI not found. Please install the .NET SDK for building C# mods."
else
    echo "dotnet CLI detected: $(dotnet --version)"
fi
