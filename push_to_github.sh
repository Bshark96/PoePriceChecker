#!/usr/bin/env bash
set -e

# Load from .env if present
if [ -f .env ]; then
  set -a
  source .env
  set +a
fi

if [ -z "$GITHUB_PAT" ]; then
  echo "Error: GITHUB_PAT is not set. Please set GITHUB_PAT in your .env file or environment."
  exit 1
fi

REPO_URL="${GITHUB_REPO_URL:-https://github.com/Bshark96/PoePriceChecker.git}"
# Strip existing auth if present, then inject PAT
CLEAN_URL=$(echo "$REPO_URL" | sed -E 's#https://[^@]+@#https://#')
AUTH_REPO_URL=$(echo "$CLEAN_URL" | sed -E "s#https://#https://${GITHUB_PAT}@#")

if [ ! -d .git ]; then
  echo "Initializing git repository..."
  git init
  git config user.name "Bshark96"
  git config user.email "richardflax6@gmail.com"
fi

git remote remove origin 2>/dev/null || true
git remote add origin "$AUTH_REPO_URL"

echo "Staging files..."
git add -A
git commit -m "Add ALT+A Live Search hotkey, Paste link button, 10-item history limit, and Clear history" || echo "Nothing to commit"

CURRENT_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
if [ -z "$CURRENT_BRANCH" ] || [ "$CURRENT_BRANCH" = "master" ]; then
  git checkout -B main
  CURRENT_BRANCH="main"
fi

echo "Force pushing to origin $CURRENT_BRANCH..."
git push -u origin "$CURRENT_BRANCH" --force
echo "Successfully pushed to $REPO_URL"
