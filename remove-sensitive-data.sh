#!/bin/bash

# IMPORTANT: This script will rewrite Git history to remove sensitive data
# Make sure you have a backup before running this!

echo "⚠️  WARNING: This will rewrite Git history!"
echo "Make sure you have a backup of your repository."
echo "Press Ctrl+C to cancel, or Enter to continue..."
read

# Remove PasswordHasher and HashGenerator from all history
echo "Removing sensitive directories from Git history..."

# Using git filter-branch (native Git command)
git filter-branch --force --index-filter \
  'git rm -r --cached --ignore-unmatch PasswordHasher/ HashGenerator/' \
  --prune-empty --tag-name-filter cat -- --all

# Alternative: Using BFG (if installed - faster and simpler)
# java -jar bfg.jar --delete-folders "{PasswordHasher,HashGenerator}" .

echo "Cleaning up..."
# Remove refs/original backup
rm -rf .git/refs/original/

# Force garbage collection
git reflog expire --expire=now --all
git gc --prune=now --aggressive

echo "✅ Sensitive data removed from history"
echo ""
echo "⚠️  IMPORTANT NEXT STEPS:"
echo "1. Change ALL passwords that were in these files immediately!"
echo "2. Force push to GitHub: git push origin --force --all"
echo "3. Force push tags: git push origin --force --tags"
echo "4. Contact GitHub to purge cached views: https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository"
echo "5. Rotate all credentials and secrets"
echo "6. Consider the repository compromised - audit for any other sensitive data"