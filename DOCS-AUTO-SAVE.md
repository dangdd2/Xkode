# Auto-Save Documentation Feature

## 📄 Overview

XKode automatically saves all execution plans and code reviews to your project's `docs/` folder for better documentation and tracking.

---

## 📂 Folder Structure

```
your-project/
├── docs/
│   ├── plans/                    ← Execution plans
│   │   ├── add-authentication-20260224-143022.md
│   │   ├── add-rate-limiting-20260224-145511.md
│   │   └── refactor-services-20260224-151203.md
│   └── reviews/                  ← Code reviews
│       ├── create-auth-service-20260224-143055.md
│       ├── add-jwt-middleware-20260224-143142.md
│       └── implement-login-20260224-143228.md
└── src/
    └── ...
```

---

## 📋 Plans (docs/plans/)

### What Gets Saved
Every execution plan created by the PlannerAgent is automatically saved.

### Filename Format
```
{sanitized-goal}-{timestamp}.md
```

Examples:
- `add-authentication-system-20260224-143022.md`
- `refactor-services-folder-20260224-145511.md`
- `create-react-typescript-app-20260224-151203.md`

### Content
Full plan in markdown format including:
- Goal and context
- Complexity and estimated time
- All steps with descriptions
- File paths
- Dependencies

### When Saved
- Immediately after planner creates the plan
- Before execution starts
- Even if plan is cancelled

---

## 📝 Reviews (docs/reviews/)

### What Gets Saved
Every code review performed by the ReviewerAgent is automatically saved.

### Filename Format
```
{sanitized-step-description}-{timestamp}.md
```

Examples:
- `create-auth-service-20260224-143055.md`
- `add-jwt-middleware-20260224-143142.md`
- `implement-login-endpoint-20260224-143228.md`

### Content
Complete review report including:
- Date and status (Approved/Needs Work)
- Score (0-10)
- Summary
- Issues found (with severity, category, file, line)
- General suggestions

### When Saved
- Immediately after each step review
- Even if review has no issues
- Only when review is enabled (not with --no-review)

---

## 🎯 Benefits

### 1. Project Documentation
- Every AI-generated plan is documented
- Code reviews are saved for future reference
- Track what was done and why

### 2. Team Collaboration
- Share plans with team members
- Review history of decisions
- Understand AI's reasoning

### 3. Audit Trail
- See what changes were planned
- Review what issues were found
- Track improvements over time

### 4. Learning & Improvement
- Review past plans to improve future ones
- Learn from review feedback
- Identify common issues

---

## 💻 Examples

### Plan File Example
```markdown
# Execution Plan

**Goal:** Add JWT authentication to the API
**Complexity:** medium
**Estimated Time:** 45 minutes

## Context
.NET 9 Web API project requiring secure authentication...

## Steps

### Step 1: Create User model
- **Type:** code
- **Estimated Time:** 10 minutes
- **Files:**
  - `Models/User.cs`

### Step 2: Add JWT service
...
```

### Review File Example
```markdown
# Code Review: Create authentication service

**Date:** 2026-02-24 14:30:55
**Status:** ❌ Needs Work
**Score:** 6/10

## Summary
The service implementation is functional but has security concerns...

## Issues Found

### 🔴 CRITICAL: Password stored in plain text
**Category:** security
**File:** `Services/AuthService.cs`
**Line:** 42
**Suggestion:** Use BCrypt or PBKDF2 for password hashing

### 🟡 WARNING: Missing input validation
...

## General Suggestions
- Add unit tests for authentication methods
- Implement rate limiting for login attempts
- Add logging for security events
```

---

## ⚙️ Configuration

### Enable/Disable Auto-Save
Currently always enabled. Plans and reviews are automatically saved.

### Custom Docs Folder
Plans and reviews are always saved to:
- `{project-root}/docs/plans/`
- `{project-root}/docs/reviews/`

### Filename Sanitization
- Invalid characters removed
- Spaces replaced with hyphens
- Lowercase
- Truncated to 50 characters
- Timestamp added for uniqueness

---

## 🔍 Usage Examples

### Example 1: View Saved Plans
```bash
# After running agent
xkode agent "Add authentication"

# Check saved plan
ls docs/plans/
# add-authentication-20260224-143022.md

cat docs/plans/add-authentication-20260224-143022.md
```

### Example 2: Review History
```bash
# After multiple tasks
ls docs/reviews/
# Lists all reviews in chronological order

# Find reviews for specific step
ls docs/reviews/ | grep "auth"
# Shows all auth-related reviews
```

### Example 3: Share with Team
```bash
# Commit docs to git
git add docs/
git commit -m "AI-generated plans and reviews"
git push

# Team members can review:
# - What was planned
# - What issues were found
# - What suggestions were made
```

---

## 🎨 Console Output

When plans and reviews are saved, you'll see:
```
Processing with planner agent...

[Plan creation output...]

📄 Plan saved: docs/plans/add-authentication-20260224-143022.md

[Execution starts...]

Step 1/5: Create User model
✓ Success

📝 Review saved: docs/reviews/create-user-model-20260224-143055.md

[Continues...]
```

---

## 🚫 Error Handling

### Silent Failures
If save fails (permissions, disk space, etc.), XKode:
- Continues execution (non-blocking)
- Shows warning in console
- Doesn't interrupt workflow

Example:
```
⚠️  Could not save plan: Access denied
```

### Debug Mode
When debugging, errors are logged but don't stop execution:
```
[DEBUG] Could not save review: Disk full
```

---

## 📊 Integration with Flags

### --no-review Flag
```bash
xkode agent --no-review "Add feature"
# Plans saved ✓
# Reviews NOT saved (review skipped)
```

### --yes Flag
```bash
xkode agent --yes "Add feature"
# Plans saved ✓
# Reviews saved ✓ (if review enabled)
# Auto-approve doesn't affect saving
```

---

## 🎯 Best Practices

### 1. Version Control
Add docs/ folder to git:
```bash
git add docs/
git commit -m "Add AI-generated documentation"
```

### 2. Review Regularly
Periodically review saved plans and reviews:
```bash
# Recent plans
ls -lt docs/plans/ | head -10

# Recent reviews
ls -lt docs/reviews/ | head -10
```

### 3. Clean Up Old Files
Archive or remove old plans/reviews:
```bash
# Move old plans to archive
mkdir docs/plans/archive-2026-01
mv docs/plans/*-202601-*.md docs/plans/archive-2026-01/
```

### 4. Use for Documentation
Reference plans in your project docs:
```markdown
## Recent Changes

See execution plan: [Add Authentication](docs/plans/add-authentication-20260224-143022.md)

Code review results: [Auth Service](docs/reviews/create-auth-service-20260224-143055.md)
```

---

## ✅ Summary

**Plans:**
- ✅ Saved to `docs/plans/`
- ✅ Full execution plan in markdown
- ✅ Saved before execution
- ✅ Always enabled

**Reviews:**
- ✅ Saved to `docs/reviews/`
- ✅ Complete review report
- ✅ Saved after each step
- ✅ Only when review enabled

**Benefits:**
- ✅ Project documentation
- ✅ Team collaboration
- ✅ Audit trail
- ✅ Learning resource

**Auto-save documentation keeps your project organized and transparent!** 📄
