#!/usr/bin/env bash
# Syncs Claude Code memory files between the repo and the local Claude memory directory.
#
# Usage:
#   ./claude-sync.sh pull   After git pull — copies repo memory into Claude so it takes effect
#   ./claude-sync.sh push   Before git commit — copies Claude memory back into repo to capture updates
#
# Claude stores project memory at: ~/.claude/projects/<derived-id>/memory/
# The ID is derived from the project's absolute path with non-alphanumeric chars replaced by dashes.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_MEMORY="$SCRIPT_DIR/.claude/memory"

# On Windows (git bash), pwd -W returns the Windows-style path that Claude uses for its ID.
# On Linux/Mac, pwd -W fails and we fall back to the Unix path.
WIN_PATH=$(pwd -W 2>/dev/null || pwd)
PROJECT_ID=$(echo "$WIN_PATH" | sed 's/[^a-zA-Z0-9]/-/g')
CLAUDE_MEMORY="$HOME/.claude/projects/$PROJECT_ID/memory"

case "${1:-}" in
  pull)
    echo "Copying memory from repo -> Claude ($CLAUDE_MEMORY)..."
    mkdir -p "$CLAUDE_MEMORY"
    cp "$REPO_MEMORY"/*.md "$CLAUDE_MEMORY/"
    echo "Done. Restart your Claude Code session for changes to take effect."
    ;;
  push)
    echo "Copying memory from Claude -> repo ($REPO_MEMORY)..."
    mkdir -p "$REPO_MEMORY"
    cp "$CLAUDE_MEMORY"/*.md "$REPO_MEMORY/"
    echo "Done. Review changes then commit."
    ;;
  *)
    echo "Usage: $0 [pull|push]"
    echo ""
    echo "  pull   Copy memory from repo into Claude memory dir (run after git pull)"
    echo "  push   Copy memory from Claude memory dir into repo  (run before git commit)"
    exit 1
    ;;
esac
