# XKode Changelog

## [v0.3.0] - Interactive Agent Mode - 2026-02-24

### 🚀 Major Features

#### ✨ Interactive Agent REPL
Agent mode is now ALWAYS interactive - no more complex flags!

**Usage:**
```bash
# Start interactive mode
xkode agent

# Start with initial task
xkode agent "Add authentication"
```

**Interactive Commands:**
- `/switch <agent>` - Switch between planner/executor/reviewer
- `/agents` - List available agents
- `/plan` - Show current execution plan
- `/export <file>` - Export plan to markdown
- `/status` - Show session status
- `/config` - Show configuration
- `/history` - Show conversation history
- `/help` - Show all commands
- `/exit` - Exit agent mode

**Benefits:**
- 🎯 Simpler UX - no flags to remember
- 🔄 Continue working without retyping commands
- 🔀 Switch agents mid-session
- 📊 Track session history and status
- 💾 Export plans anytime

#### 📄 Auto-Save Documentation (NEW!)
All plans and reviews automatically saved for project documentation!

**Auto-saved to:**
- `docs/plans/` - Every execution plan
- `docs/reviews/` - Every code review

**What gets saved:**
- Complete execution plans with all steps
- Code reviews with issues and suggestions
- Timestamped filenames for tracking
- Markdown format for easy viewing

**Benefits:**
- 📚 Automatic project documentation
- 🤝 Team collaboration and transparency
- 🔍 Audit trail of AI decisions
- 📈 Track improvements over time

**See:** [DOCS-AUTO-SAVE.md](DOCS-AUTO-SAVE.md) for full details

### 🔧 Improvements

- Renamed `xkode chat` to `xkode ask` for clarity
- Removed `--interactive` flag (always interactive now)
- Task argument is optional (starts empty REPL)
- Session tracks history, duration, completed steps
- Cleaner exit with session summary
- All flags (--yes, --no-review) shown in welcome screen
- Status display shows all configuration
- Fixed markup color error in prompts

### 🗑️ Removed (Deprecated)

- **`xkode run`** - Use `xkode agent` instead (interactive REPL)
- **`xkode review`** - Code review now built into agent workflow
- **`xkode models`** - Use `xkode config get model` or check Ollama directly

**Command Renamed:**
- `xkode chat` → `xkode ask` (more intuitive for Q&A interaction)

**Rationale:** These commands were redundant with the new multi-agent system.
Agent mode now handles all use cases in an interactive, more powerful way.

---

## [v0.2.0] - Multi-Agent System - 2026-02-20

### 🚀 Major Features

#### ✨ Multi-Agent Workflow
Complete implementation of 3-agent system for complex tasks:

**New Agents:**
- **PlannerAgent** - Breaks tasks into ordered execution steps
- **ExecutorAgent** - Implements code changes step-by-step  
- **ReviewerAgent** - Reviews code quality, finds issues

**AgentOrchestrator** - Coordinates all agents through Plan → Execute → Review workflow

#### 📝 Plan Export/Import Workflow (NEW!)
Export execution plans to markdown, edit manually, then execute:

**New Commands:**
```bash
xkode agent "task" --export-plan plan.md    # Export plan
xkode agent --plan plan.md                  # Execute plan
```

**Features:**
- Export plans to human-readable Markdown
- Edit plans manually (add/remove/reorder steps)
- Reuse plans across projects
- Create plan templates
- Team collaboration on plans
- Full control over execution

**Files:**
- `Models/ExecutionPlanExtensions.cs` - Export/import logic
- `PLAN-WORKFLOW.md` - Complete workflow guide
- `example-plan.md` - Example plan template

#### 📁 New Files Created

**Models:**
- `Models/ExecutionPlan.cs` - Plan data structures
- `Models/ReviewResult.cs` - Review output models

**Agents:**
- `Agents/IAgent.cs` - Base interface + AgentBase
- `Agents/PlannerAgent.cs` - Planning specialist
- `Agents/ExecutorAgent.cs` - Code execution specialist
- `Agents/ReviewerAgent.cs` - Quality review specialist
- `Agents/AgentOrchestrator.cs` - Workflow coordinator

**Commands:**
- `Commands/AgentCommand.cs` - CLI interface for multi-agent mode

**Documentation:**
- `MULTI-AGENT.md` - Comprehensive feature documentation

---

### 🎯 New CLI Command

```bash
xkode agent "Add authentication to my app"
```

**Options:**
- `--yes` - Auto-approve all steps
- `--no-review` - Skip code review
- `--planner-model <model>` - Specific planner model
- `--executor-model <model>` - Specific executor model
- `--reviewer-model <model>` - Specific reviewer model

---

### 🔧 Changes

#### Program.cs
- Registered PlannerAgent, ExecutorAgent, ReviewerAgent in DI
- Added AgentCommand to CLI
- Updated version to 0.2.0

#### XKode.csproj
- Version bumped to 0.2.0

#### README.md
- Added Multi-Agent feature to features list
- Updated usage section with agent examples
- Added link to MULTI-AGENT.md

---

### 📊 Architecture

```
User → Planner → Executor → Reviewer → Result
         ↓          ↓           ↓
      (Plan)    (Execute)   (Verify)
```

---

### 💡 When to Use Multi-Agent

**Use Multi-Agent for:**
- Complex, multi-step tasks
- Adding new features
- Refactoring large codebases
- Production code that needs review

**Use Single-Agent (chat/run) for:**
- Simple, single-file changes
- Quick fixes
- Explanations
- Interactive exploration

---

### 🎨 User Experience

**Interactive Flow:**
1. **Planning Phase** - Shows execution plan, user approves
2. **Execution Phase** - Executes each step, shows progress
3. **Review Phase** - Reviews after each step, shows issues
4. **Final Review** - Overall assessment with score

**Example Session:**
```
🤖 Phase 1: Planning...
╔══════════════════════════╗
║ Execution Plan (5 steps) ║
╚══════════════════════════╝

Step 1: Create User model
Step 2: Add JWT service
...

Proceed? ✅ Yes

🤖 Phase 2: Executing...
Step 1/5: Create User model
  ✓ file_write: Models/User.cs

💡 Review score: 8/10

🤖 Phase 3: Final review...
✓ Task completed successfully!
Final score: 8/10
```

---

### ⚙️ Configuration

Can be configured in `~/.config/xkode/config.json`:

```json
{
  "MultiAgent": {
    "PlannerModel": "qwen2.5-coder:32b",
    "ExecutorModel": "qwen2.5-coder:7b",
    "ReviewerModel": "qwen2.5-coder:32b",
    "AutoReview": true,
    "MaxSteps": 10
  }
}
```

---

### 📈 Statistics

**Code Added:**
- 7 new files
- ~1,200 lines of code
- 3 specialized agents
- 1 orchestrator
- Full test coverage structure ready

**Features:**
- Automatic task planning
- Step-by-step execution
- Real-time code review
- Issue detection (security, bugs, performance, style)
- User approval gates
- Graceful cancellation

---

### 🔄 Breaking Changes

None. All existing commands work as before.

Multi-agent is a new, optional workflow mode.

---

### 🐛 Bug Fixes

- Fixed streaming delay consistency across all commands
- Improved error handling in agent execution
- Better JSON parsing for agent responses

---

### 🎯 Next Steps (v0.3)

Planned features:
- [ ] Parallel step execution
- [ ] Step retry with automatic fixes
- [ ] Custom agent system prompts
- [ ] Agent learning from feedback
- [ ] Integration test generation
- [ ] RAG code search with ChromaDB

---

### 📝 Migration Guide

**No migration needed!** This is a purely additive feature.

Existing commands work exactly as before:
- `xkode chat` - Interactive mode
- `xkode run` - Single task
- `xkode review` - Code review

New multi-agent mode is opt-in:
- `xkode agent "task"` - Multi-agent workflow

---

### 👥 Contributors

Built with ❤️ in Vietnam 🇻🇳

---

### 📚 Documentation

- Main README: [README.md](README.md)
- Multi-Agent Guide: [MULTI-AGENT.md](MULTI-AGENT.md)
- Product Plan: [XKode_Product_Plan_v1.0.docx](XKode_Product_Plan_v1.0.docx)

---

## [v0.1.1] - 2026-02-19

### Changes
- .NET 10 upgrade
- Unit tests added
- Solution file added
- Code refactoring improvements
- Streaming delay improvements

See previous changelog for details.
