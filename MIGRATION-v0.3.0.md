# Migration Guide: v0.2.x → v0.3.0

## 🔄 Command Changes

XKode v0.3.0 removes three redundant commands, renames one for clarity, and consolidates everything into the improved interactive modes.

---

## ✏️ Renamed Commands

### `xkode chat` → `xkode ask`

**Before (v0.2.x):**
```bash
xkode chat
xkode chat --path /my/project
```

**After (v0.3.0):**
```bash
xkode ask
xkode ask --path /my/project
```

**What Changed:**
- More intuitive name for Q&A interaction
- "Ask" better describes the conversational nature
- Same functionality, just clearer naming

---

## ❌ Removed Commands

### 1. `xkode run` → Use `xkode agent`

**Before (v0.2.x):**
```bash
xkode run "Add error handling to UserService"
```

**After (v0.3.0):**
```bash
xkode agent "Add error handling to UserService"
```

**What Changed:**
- Agent mode is now always interactive (REPL)
- Executes the task immediately
- Stays in REPL for follow-up tasks
- More powerful with planning and review

---

### 2. `xkode review` → Built into agent workflow

**Before (v0.2.x):**
```bash
xkode review src/MyClass.cs
xkode review --path .
```

**After (v0.3.0):**
```bash
# Code review happens automatically in agent mode
xkode agent "Review the code in src/MyClass.cs"

# Or disable review if not needed
xkode agent "Fix bugs" --no-review
```

**What Changed:**
- Review is integrated into multi-agent workflow
- ReviewerAgent reviews code after each step
- More contextual reviews (knows what was changed)
- Results saved to `docs/reviews/`
- Can be disabled with `--no-review` flag

---

### 3. `xkode models` → Use Ollama or config

**Before (v0.2.x):**
```bash
xkode models
```

**After (v0.3.0):**
```bash
# Check available models directly with Ollama
ollama list

# Or view current model in config
xkode config get model

# Change model
xkode config set model qwen2.5-coder:32b
```

**What Changed:**
- Removed duplicate of Ollama's functionality
- Use Ollama CLI directly for model management
- XKode config commands for current settings

---

## ✅ Recommended Workflow Updates

### Old Workflow (v0.2.x)
```bash
# Review code first
xkode review src/

# Then make changes
xkode run "Add validation"

# Check models
xkode models
```

### New Workflow (v0.3.0)
```bash
# Everything in one interactive session
xkode agent

Agent [[planner]] > Add validation to the API
[Creates plan, executes, reviews automatically]

Agent [[planner]] > Also add logging
[Continues working...]

Agent [[planner]] > /status
[See what's been done]

Agent [[planner]] > /exit
```

---

## 🎯 Benefits of New Approach

### Consolidated
- ✅ One command for everything: `xkode agent`
- ✅ Interactive by default
- ✅ No context switching

### More Powerful
- ✅ Multi-agent planning
- ✅ Automatic code review
- ✅ Session continuity
- ✅ Auto-saved documentation

### Better UX
- ✅ Stay in flow
- ✅ No retyping commands
- ✅ Switch agents mid-session
- ✅ Track history

---

## 📋 Quick Reference

| Old Command | New Command |
|-------------|-------------|
| `xkode chat` | `xkode ask` |
| `xkode run "task"` | `xkode agent "task"` |
| `xkode review file.cs` | `xkode agent "Review file.cs"` |
| `xkode review --path .` | Automatic in agent mode |
| `xkode models` | `ollama list` |

---

## 🚀 New Features You Gain

### 1. Interactive REPL
```bash
xkode agent

Agent [[planner]] > Add auth
Agent [[planner]] > Add tests
Agent [[planner]] > /exit
```

### 2. Auto-Save Documentation
- Plans saved to `docs/plans/`
- Reviews saved to `docs/reviews/`

### 3. Session Management
- `/status` - See session info
- `/history` - See conversation
- `/plan` - See current plan

### 4. Agent Switching
```bash
Agent [[planner]] > /switch executor
Agent [[executor]] > Implement step 1
Agent [[executor]] > /switch reviewer
Agent [[reviewer]] > Review the code
```

---

## 💡 Migration Tips

### Tip 1: Update Scripts
If you have scripts using old commands:

**Before:**
```bash
#!/bin/bash
xkode run "Add tests"
xkode review src/
```

**After:**
```bash
#!/bin/bash
xkode agent "Add tests"
# Review happens automatically
```

### Tip 2: CI/CD Integration
For automation, use the non-interactive flags:

```bash
# Old
xkode run "Fix linting issues"

# New
xkode agent "Fix linting issues" --yes --no-review
```

### Tip 3: Model Selection
**Before:**
```bash
xkode models
xkode run "task" --model qwen:32b
```

**After:**
```bash
ollama list
xkode agent "task" --model qwen:32b
```

---

## ⚠️ Breaking Changes

### 1. No Standalone Review Command
If you relied on `xkode review` as a standalone tool:
- Use agent mode: `xkode agent "Review the code"`
- Reviews are now part of the workflow, not standalone

### 2. No Models Command
If you used `xkode models` in scripts:
- Use `ollama list` instead
- Or `xkode config get model` for current model

### 3. Run Command Removed
All `xkode run` usage should change to `xkode agent`:
- Same functionality, but interactive
- Can exit immediately after task completes

---

## 📞 Need Help?

### Still Using Old Commands?
Update your workflow using this guide.

### Feature Missing?
If you relied on specific functionality from removed commands, please open an issue.

### Questions?
Check the updated documentation:
- [README.md](README.md) - Overview
- [MULTI-AGENT.md](MULTI-AGENT.md) - Agent mode guide
- [DOCS-AUTO-SAVE.md](DOCS-AUTO-SAVE.md) - Auto-save docs

---

## ✅ Checklist

- [ ] Update scripts that use `xkode run`
- [ ] Replace `xkode review` with agent mode
- [ ] Use `ollama list` instead of `xkode models`
- [ ] Try new interactive agent mode
- [ ] Check out auto-save documentation feature
- [ ] Review CHANGELOG for all changes

---

**Welcome to XKode v0.3.0!** 🚀

The simplified command structure and interactive REPL make development faster and more intuitive.
