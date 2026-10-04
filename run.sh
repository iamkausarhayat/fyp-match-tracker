#!/bin/bash
echo "==================================================================="
echo "    National University of Modern Languages (NUML) Islamabad"
echo "              FYP Match Tracker - Web Application"
echo "==================================================================="
echo ""

if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET SDK is not installed."
    echo "Please install .NET 10 SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "Starting application server on http://localhost:5242 ..."
dotnet run
