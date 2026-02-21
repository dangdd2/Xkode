# 📝 Plan Workflow Guide

## Overview

XKode v0.2.0 introduces a powerful **Plan Export/Import** workflow that gives you full control over execution plans before they run.

---

## 🎯 Three Execution Modes

### Mode 1: Direct Execution (Default)
```bash
xkode agent "Add authentication to my app"
```

**Flow:**
1. AI creates plan automatically
2. Shows plan to you
3. You approve
4. Executes immediately

**Best for:** Quick tasks, when you trust AI's plan

---

### Mode 2: Export Plan (Review Mode)
```bash
xkode agent "Add authentication" --export-plan auth-plan.md
```

**Flow:**
1. AI creates plan
2. Saves to `auth-plan.md`
3. **STOPS** (does not execute)
4. You review and edit the plan
5. Run separately with `--plan` flag

**Best for:** Complex tasks, team review, template creation

---

### Mode 3: Execute Custom Plan
```bash
xkode agent --plan auth-plan.md
```

**Flow:**
1. Loads plan from markdown file
2. Shows plan to you
3. You approve
4. Executes your custom plan

**Best for:** Edited plans, reusable plans, templates

---

## 📄 Plan Format

Plans are stored as **human-readable Markdown** files:

```markdown
# Execution Plan

**Goal:** Add JWT authentication to web API
**Complexity:** medium
**Estimated Time:** 45 minutes

## Context
.NET 9 Web API project with Controllers and Services folders.
Currently no authentication implemented.

## Steps

### Step 1: Create User model with password hashing

- **Type:** code
- **Estimated Time:** 10 minutes
- **Files:**
  - `Models/User.cs`
  - `Models/UserDto.cs`

### Step 2: Add JWT authentication service

- **Type:** code
- **Estimated Time:** 15 minutes
- **Files:**
  - `Services/AuthService.cs`
  - `Services/IAuthService.cs`
- **Dependencies:** Steps 1

### Step 3: Create authentication endpoints

- **Type:** code
- **Estimated Time:** 10 minutes
- **Files:**
  - `Controllers/AuthController.cs`
- **Dependencies:** Steps 1, 2

### Step 4: Configure JWT middleware

- **Type:** config
- **Estimated Time:** 5 minutes
- **Files:**
  - `Program.cs`
  - `appsettings.json`

### Step 5: Write unit tests

- **Type:** test
- **Estimated Time:** 5 minutes
- **Files:**
  - `Tests/AuthServiceTests.cs`
- **Dependencies:** Steps 2

---

## Instructions

- Edit steps as needed (add, remove, reorder)
- Keep the markdown structure intact
- Step numbers will be reassigned automatically
- Save and run: `xkode agent --plan <this-file>`
```

---

## ✏️ Editing Plans

You can freely edit:

### ✅ Safe to Edit

| What | How | Example |
|------|-----|---------|
| **Goal** | Change description | "Add JWT auth" → "Add OAuth2 auth" |
| **Complexity** | low / medium / high | medium → high |
| **Estimated Time** | Any duration | "45 minutes" → "2 hours" |
| **Step Description** | Rewrite freely | "Create model" → "Create User entity with validation" |
| **Step Type** | code/test/doc/config | code → test |
| **Estimated Time (step)** | Any number | 10 minutes → 20 minutes |
| **Files list** | Add/remove files | Add `UserRepository.cs` |
| **Dependencies** | Add/remove step numbers | Dependencies: Steps 1 → Steps 1, 2 |

### ⚠️ Keep Structure

- Keep `### Step N:` headers (numbers will auto-renumber)
- Keep `- **Type:**` format
- Keep `- **Files:**` format with indented `- \`filename\``
- Keep markdown structure intact

### 🚫 Don't Change

- Section headers: `# Execution Plan`, `## Steps`, `## Instructions`
- Metadata format: `**Goal:**`, `**Complexity:**`
- File list indentation (must be `  - \`file\``)

---

## 🔄 Complete Workflow Examples

### Example 1: Review Before Execution

```bash
# Step 1: Generate plan
xkode agent "Add authentication system" --export-plan auth.md

# Output:
# ✓ Plan exported to: auth.md
# Edit the plan, then run: xkode agent --plan auth.md

# Step 2: Review plan
cat auth.md
# [AI created 8 steps]

# Step 3: Edit (optional)
nano auth.md
# - Remove step 7 (not needed)
# - Combine steps 2 and 3
# - Add step 8 (rate limiting)

# Step 4: Execute edited plan
xkode agent --plan auth.md

# Output:
# 🤖 Loading plan from: auth.md
# ✓ Loaded plan with 7 steps
# 
# ╔═══════════════════════════╗
# ║ Execution Plan (7 steps)  ║
# ╚═══════════════════════════╝
# [shows your edited plan]
#
# Proceed? ✅ Yes
# [executes...]
```

---

### Example 2: Team Collaboration

```bash
# Developer 1: Create plan
xkode agent "Refactor Services layer" --export-plan refactor.md

# Developer 1: Share plan
git add refactor.md
git commit -m "Plan: Refactor Services"
git push

# Developer 2: Review on GitHub
# - Comments on PR
# - Suggests changes

# Developer 1: Update plan
nano refactor.md
# [apply feedback]

# Developer 1: Execute
xkode agent --plan refactor.md --yes
```

---

### Example 3: Reusable Templates

```bash
# Create template once
mkdir -p templates
xkode agent "Setup new CRUD API" --export-plan templates/crud-setup.md

# Edit template to be generic
nano templates/crud-setup.md
# Change specific names to placeholders

# Reuse for multiple entities
xkode agent --plan templates/crud-setup.md --path ./UserAPI
xkode agent --plan templates/crud-setup.md --path ./ProductAPI
xkode agent --plan templates/crud-setup.md --path ./OrderAPI
```

---

### Example 4: Break Down Large Tasks

```bash
# AI might create too many steps
xkode agent "Migrate entire app to microservices" --export-plan migrate.md

# Output: 25 steps (too much!)

# Break into phases
cp migrate.md phase1-users.md
nano phase1-users.md
# Keep only steps 1-8 (user service)

cp migrate.md phase2-products.md
nano phase2-products.md
# Keep only steps 9-16 (product service)

# Execute in phases
xkode agent --plan phase1-users.md
# [test phase 1]
xkode agent --plan phase2-products.md
# [test phase 2]
```

---

## 🎨 Advanced Editing

### Adding Steps

```markdown
### Step 3: New step I want to add

- **Type:** code
- **Estimated Time:** 15 minutes
- **Files:**
  - `Services/NewService.cs`
- **Dependencies:** Steps 1, 2
```

Numbers will auto-renumber (Step 3 becomes 3, old 3 becomes 4, etc.)

---

### Removing Steps

Just delete the entire step section:

```markdown
### Step 5: Don't need this
[DELETE THIS ENTIRE SECTION]
```

Remaining steps auto-renumber.

---

### Changing Dependencies

```markdown
### Step 4: This step now depends on steps 1, 2, and 3

- **Dependencies:** Steps 1, 2, 3
```

Or remove dependencies:

```markdown
### Step 2: This step has no dependencies now

- **Type:** code
- **Estimated Time:** 10 minutes
```

---

### Adding Files

```markdown
### Step 2: Add more files

- **Files:**
  - `Models/User.cs`
  - `Models/UserDto.cs`
  - `Models/UserValidator.cs`     ← NEW
  - `Models/IUserRepository.cs`   ← NEW
```

---

## 🐛 Troubleshooting

### "No steps found in plan file"

**Cause:** Missing `## Steps` section or all steps deleted

**Fix:** Ensure at least one step exists with proper format:
```markdown
## Steps

### Step 1: Do something

- **Type:** code
- **Files:**
  - `file.cs`
```

---

### "Failed to parse plan file"

**Cause:** Markdown structure broken

**Fix:** Check:
- Step headers start with `### Step N:`
- Metadata uses `**Key:**` format
- Files are indented with `  - \`filename\``

---

### Steps execute in wrong order

**Cause:** Dependencies not specified

**Fix:** Add dependencies:
```markdown
### Step 3: Depends on 1 and 2

- **Dependencies:** Steps 1, 2
```

---

## 💡 Best Practices

### ✅ DO

- **Review AI plans** before execution
- **Save plans** for similar future tasks
- **Version control** plans in Git
- **Share plans** with team for review
- **Break large tasks** into multiple plans
- **Add notes** in Context section
- **Test plans** on small projects first

### ❌ DON'T

- Don't break markdown structure
- Don't remove section headers
- Don't execute without review (on important code)
- Don't create circular dependencies (Step 2 depends on 3, Step 3 depends on 2)
- Don't make steps too large (split into smaller steps)

---

## 📊 Plan vs Direct Execution

| Aspect | Direct (`xkode agent "task"`) | Plan (`--export-plan` + `--plan`) |
|--------|-------------------------------|-----------------------------------|
| **Speed** | ⚡⚡⚡ Fastest | ⚡⚡ Slower (requires review) |
| **Control** | ⭐⭐ Low | ⭐⭐⭐⭐⭐ Full control |
| **Review** | Quick approval | Deep review possible |
| **Reusability** | ❌ One-time | ✅ Save and reuse |
| **Team Work** | ❌ Solo only | ✅ Collaborative |
| **Complexity** | ⭐ Simple | ⭐⭐⭐ Advanced |

---

## 🎯 When to Use Each Mode

### Use Direct Execution When:
- ✅ Quick, simple tasks
- ✅ You trust AI completely
- ✅ Working solo
- ✅ Prototyping
- ✅ Low-risk changes

### Use Plan Workflow When:
- ✅ Complex, multi-step tasks
- ✅ Production code
- ✅ Team collaboration
- ✅ Need to review before execution
- ✅ Want to reuse the plan
- ✅ Breaking down large tasks
- ✅ Creating templates

---

## 🚀 Quick Reference

```bash
# Generate and execute (default)
xkode agent "task"

# Export plan only (don't execute)
xkode agent "task" --export-plan plan.md

# Execute custom plan
xkode agent --plan plan.md

# Execute without prompts
xkode agent --plan plan.md --yes

# Execute without review
xkode agent --plan plan.md --no-review

# Combine options
xkode agent --plan plan.md --yes --no-review --path ./src
```

---

## 📚 See Also

- [MULTI-AGENT.md](MULTI-AGENT.md) - Complete multi-agent documentation
- [example-plan.md](example-plan.md) - Example plan template
- [README.md](README.md) - Main documentation

---

**Happy planning! 🎉**
