#!/bin/bash
# ─────────────────────────────────────────────────────────────
#  dev.sh — Quick development runner for XKode
#  Usage: ./dev.sh [command] [args...]
#  Examples:
#    ./dev.sh                    → Start interactive chat
#    ./dev.sh chat --path ~/myproject
#    ./dev.sh run "Explain this codebase"
#    ./dev.sh review src/Program.cs
#    ./dev.sh models
# ─────────────────────────────────────────────────────────────

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/src/XKode"

# Check prerequisites
if ! command -v dotnet &>/dev/null; then
    echo "❌ .NET SDK not found. Install from https://dot.net/download"
    exit 1
fi

if ! curl -s http://localhost:11434/api/tags &>/dev/null; then
    echo "⚠️  Ollama not running. Starting check..."
    echo "   Run: ollama serve"
    echo "   Then: ollama pull qwen2.5-coder:7b"
fi

# Run the CLI
cd "$PROJECT"
dotnet run -- "$@"
