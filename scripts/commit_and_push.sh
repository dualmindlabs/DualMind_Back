#!/usr/bin/env bash
set -euo pipefail

MESSAGE="add .gitignore to ignore build/temp/IDE noise; recent work touched deploy workflow and Program.cs"

echo "==> commit-and-push: starting"
branch=$(git rev-parse --abbrev-ref HEAD)
echo "On branch: $branch"

echo "==> staging all changes"
git add .

echo "==> remove cached files so .gitignore will apply (may show warnings)"
git rm -r --cached . || true

echo "==> staging after untracking"
git add .

echo "==> committing changes"
git commit -m "$MESSAGE" || {
  echo "No changes to commit or commit failed.";
}

echo "==> pushing to origin/$branch"
git push origin "$branch"

echo "==> done"
