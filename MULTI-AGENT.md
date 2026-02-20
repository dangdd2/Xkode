# 🤖 Multi-Agent Feature Documentation

## Overview

XKode v0.2.0 introduces a revolutionary **multi-agent system** that breaks down complex tasks into manageable steps with built-in quality control.

---

## Architecture

```
User Request
     ↓
┌─────────────────┐
│ PlannerAgent    │ → Breaks task into ordered steps
└────────┬────────┘
         ↓
   [Execution Plan]
         ↓
┌─────────────────┐
│ ExecutorAgent   │ → Implements each step
└────────┬────────┘
         ↓
   [Code Changes]
         ↓
┌─────────────────┐
│ ReviewerAgent   │ → Reviews quality, finds issues
└────────┬────────┘
         ↓
   [Review Result]
         ↓
     User
```

---

## Three Specialized Agents

### 1. 🧠 PlannerAgent
**Role:** Strategic planning
**Input:** User request + codebase context
**Output:** Structured execution plan (JSON)

**Example:**
```
Input: "Add authentication to my web app"

Output:
{
  "goal": "Add JWT authentication",
  "steps": [
    {
      "order": 1,
      "description": "Create User model with password hashing",
      "type": "code",
      "files": ["Models/User.cs"]
    },
    {
      "order": 2,
      "description": "Add JWT authentication service",
      "type": "code",
      "files": ["Services/AuthService.cs"]
    }
    // ... more steps
  ]
}
```

### 2. ⚡ ExecutorAgent
**Role:** Implementation
**Input:** Single execution step
**Output:** Code changes using ```write:path syntax

**Example:**
```
Step: "Create User model with password hashing"

Output:
```write:Models/User.cs
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
```
```

### 3. 🔍 ReviewerAgent
**Role:** Quality assurance
**Input:** Code changes
**Output:** Review result with issues + score (JSON)

**Example:**
```json
{
  "approved": false,
  "score": 6,
  "issues": [
    {
      "severity": "warning",
      "category": "security",
      "message": "Password hashing not implemented",
      "file": "Models/User.cs",
      "suggestion": "Use BCrypt or PBKDF2"
    }
  ]
}
```

---

## Usage

### Basic Command

```bash
xkode agent "Add authentication to my web app"
```

### With Options

```bash
# Specify project path
xkode agent "Refactor Services folder" --path ./src

# Auto-approve all steps (no prompts)
xkode agent "Add logging" --yes

# Skip code review (faster)
xkode agent "Quick fix" --no-review

# Use specific models
xkode agent "Complex task" \
  --planner-model qwen2.5-coder:32b \
  --executor-model qwen2.5-coder:7b \
  --reviewer-model qwen2.5-coder:32b
```

---

## Interactive Flow

### Phase 1: Planning

```
🤖 Phase 1: Planning...

╔══════════════════════════════════════════╗
║ Execution Plan (5 steps)                 ║
╚══════════════════════════════════════════╝

#  Description                 Type   Files
1  Create User model          code   Models/User.cs
2  Add JWT service            code   Services/AuthService.cs
3  Create auth endpoints      code   Controllers/AuthController.cs
4  Add auth middleware        code   Middleware/AuthMiddleware.cs
5  Write unit tests           test   Tests/AuthTests.cs

Goal: Add JWT authentication to web app
Complexity: medium
Estimated time: 45 minutes

Proceed with this plan?
  ✅ Yes, execute plan
  ❌ No, cancel
```

### Phase 2: Execution

```
🤖 Phase 2: Executing steps...

Step 1/5: Create User model
  ✓ file_write: Models/User.cs

Step 2/5: Add JWT service
  ✓ file_write: Services/AuthService.cs
  
💡 Review score: 8/10
Suggestions:
  • Consider adding token expiration configuration
  • Add logging for authentication failures
```

### Phase 3: Final Review

```
🤖 Phase 3: Final review...

Review Score: 8/10

Issues Found:
  ⚠️  Missing email validation in User model
     in Models/User.cs line 12
     → Add [EmailAddress] attribute

  💡 Consider adding rate limiting to login endpoint
     → Use ASP.NET rate limiting middleware

All JWT functionality implemented correctly.
Authentication flow follows security best practices.

✅ Task completed successfully!
Completed 5/5 steps
Final score: 8/10
```

---

## Configuration

Add to `~/.config/xkode/config.json`:

```json
{
  "DefaultModel": "qwen2.5-coder:7b",
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

## When to Use Multi-Agent vs Single-Agent

| Use Multi-Agent | Use Single-Agent (chat/run) |
|---|---|
| Complex, multi-step tasks | Simple, single-file changes |
| Adding new features | Quick fixes |
| Refactoring large codebases | Explanations |
| Need quality review | Interactive exploration |
| Production code | Prototyping |

**Examples:**

**Multi-Agent:**
- "Add authentication system"
- "Refactor to use dependency injection"
- "Add comprehensive error handling"
- "Migrate from SQL to MongoDB"

**Single-Agent:**
- "Fix this bug"
- "Explain this function"
- "Add comments to this file"
- "Format this code"

---

## Advanced Features

### Cancellation

Press `Ctrl+C` at any time to cancel. The orchestrator will stop gracefully after the current step.

### Step Dependencies

The planner automatically identifies dependencies:

```json
{
  "order": 3,
  "description": "Add auth endpoints",
  "dependencies": [1, 2]  // Requires steps 1 and 2 first
}
```

### Error Handling

If a step fails:
1. Execution stops
2. Error details shown
3. User can fix manually and retry
4. Or cancel entire task

---

## Limitations

- Maximum 10 steps per plan
- Context size limited to 30,000 characters
- Requires Ollama to be running
- Uses more tokens than single-agent mode
- Slower than single-agent for simple tasks

---

## Troubleshooting

**"Planner returned invalid JSON"**
- Planner model may be too small
- Try: `--planner-model qwen2.5-coder:32b`

**"Step execution failed"**
- Check file permissions
- Verify project structure
- Review error message for details

**"Review taking too long"**
- Skip with `--no-review`
- Or use faster model: `--reviewer-model qwen2.5-coder:7b`

---

## Performance Tips

1. **Use appropriate models:**
   - Planner: Larger model (32b) for better planning
   - Executor: Balanced model (7b) for speed
   - Reviewer: Larger model (32b) for thorough review

2. **Auto-approve for trusted tasks:**
   ```bash
   xkode agent "Add logging" --yes
   ```

3. **Skip review for simple tasks:**
   ```bash
   xkode agent "Quick refactor" --no-review
   ```

4. **Break large tasks into phases:**
   Instead of: "Rewrite entire app"
   Do: "Phase 1: Refactor models" → "Phase 2: Update controllers"

---

## Examples

### Example 1: Add Feature
```bash
xkode agent "Add user profile page with avatar upload"
```

### Example 2: Refactoring
```bash
xkode agent "Refactor Services to use interfaces and DI" --path ./src
```

### Example 3: Testing
```bash
xkode agent "Write comprehensive unit tests for UserService"
```

### Example 4: Fast Mode
```bash
xkode agent "Add XML documentation to all public methods" --yes --no-review
```

---

## Comparison: Single vs Multi-Agent

| Feature | Single Agent (chat/run) | Multi-Agent |
|---|---|---|
| **Speed** | ⚡⚡⚡⚡ Fast | ⚡⚡ Slower |
| **Planning** | Manual | Automatic |
| **Quality Review** | Manual | Automatic |
| **Complex Tasks** | Difficult | Easy |
| **Cost (tokens)** | Low | Higher |
| **Best for** | Quick tasks | Production code |

---

## Future Enhancements (v0.3)

- [ ] Parallel step execution
- [ ] Step retry with fixes
- [ ] Custom agent prompts
- [ ] Agent learning from feedback
- [ ] Integration test generation
- [ ] Automatic documentation

---

*Multi-Agent mode makes XKode the most powerful local AI coding assistant.* 🚀
