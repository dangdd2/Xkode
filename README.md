# ⚡ XKode

> **Local AI Coding Agent — Free, private, powerful**
>
> Terminal tool chạy **100% trên máy bạn** — không cloud, không API cost, không data leak.

[![.NET](https://img.shields.io/badge/.NET-9.0+-blue)](https://dot.net)
[![Ollama](https://img.shields.io/badge/Ollama-compatible-green)](https://ollama.ai)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Privacy](https://img.shields.io/badge/Privacy-100%25%20Local-brightgreen)](#)

```
__  ____  __          __   
\ \/ / //_/___  ____/ /__ 
 \  / ,< / __ \/ __  / _ \
 / / /| / /_/ / /_/ /  __/
/_/_/ |_\____/\__,_/\___/ 

Local AI Coding Agent v0.1.0
```

---

## ✨ Features

| | Feature | Description |
|---|---|---|
| 🤖 | **Multi-Agent Mode** | Interactive REPL: Plan → Execute → Review ✨ NEW |
| 🗣️ | **Interactive Ask** | REPL with streaming, markdown rendering |
| 📁 | **Codebase Context** | Indexes entire project automatically |
| ✏️ | **File Editing** | AI edits files with diff preview |
| 💻 | **Shell Execution** | Safe command execution |
| 📄 | **SKILL/Docs Reader** | Load `.md` files into context |
| 📝 | **Auto-Documentation** | Plans & reviews saved to docs/ ✨ NEW |
| 🔒 | **100% Local** | Zero API cost |

---

## 🚀 Quick Start

```bash
# 1. Install Ollama + model
ollama pull qwen2.5-coder:7b

# 2. Install XKode
git clone https://github.com/yourname/xkode
cd xkode/src/XKode
dotnet pack -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg XKode

# 3. Run
xkode ask
```

---

## 📖 Usage

```bash
# 🤖 Multi-Agent Mode (NEW v0.3 - Interactive!)
# Start interactive mode
xkode agent

# Execute task then stay interactive
xkode agent "Add authentication to my app"

# 📄 Auto-saved documentation:
# - Plans saved to: docs/plans/
# - Reviews saved to: docs/reviews/

# Inside interactive mode:
# Agent [[planner]] > Add rate limiting
# Agent [[planner]] > /switch executor
# Agent [[executor]] > Implement login endpoint
# Agent [[executor]] > /help
# Agent [[executor]] > /exit

# 📝 Plan workflow (export → edit → execute)
xkode agent "Add auth" --export-plan plan.md    # Export plan
# Edit plan.md manually
xkode agent --plan plan.md                      # Execute edited plan

# Interactive chat
xkode ask
xkode ask --path /my/project

# Slash commands in ask mode
/docs README.md     # AI reads & summarizes
/skill SKILL.md     # Load instructions
/model qwen:32b     # Switch model
/help               # All commands
```

**Available Modes:**
- **Agent Mode** (Interactive REPL) - Multi-agent planning and execution
- **Ask Mode** (Interactive) - Conversational coding assistant

See [MULTI-AGENT.md](MULTI-AGENT.md) for detailed documentation.

---

## 📄 SKILL.md — Auto-instructions

Create `.xkode/SKILL.md`:

```markdown
# Coding Rules

- Use TypeScript strict mode
- Follow Airbnb style
- Add JSDoc to functions
```

XKode auto-loads this on startup → AI follows your rules!

---

## 🧠 Recommended Models

| Model | RAM | Best For |
|---|---|---|
| `qwen2.5-coder:7b` | 8GB | ⭐ Default |
| `qwen2.5-coder:32b` | 24GB | 🚀 Best quality |
| `deepseek-coder-v2:16b` | 16GB | 🎯 Reasoning |
| `llama3.2:3b` | 4GB | ⚡ Fast/low RAM |

---

## 🛡️ Safety

✅ File edits → Diff + confirm
✅ Shell → Show + confirm  
⛔ Dangerous commands → Blocked
⚠️ High-risk → Extra warning

---

## 🔄 Update

```bash
cd xkode/src/XKode
dotnet pack -c Release -o ./nupkg
dotnet tool update --global --add-source ./nupkg XKode
```

---

## 🤝 Contributing

```bash
git clone https://github.com/yourname/xkode
cd xkode
./dev.sh chat
```

---

## 📄 License

MIT — Free for all use.

---

*Built with ❤️ in Vietnam 🇻🇳*
