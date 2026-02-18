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

| | Feature | Mô tả |
|---|---|---|
| 🗣️ | **Interactive Chat** | REPL với streaming, markdown rendering |
| 📁 | **Codebase Context** | Index toàn bộ project |
| ✏️ | **File Editing** | AI sửa file với diff preview |
| 💻 | **Shell Execution** | Chạy commands an toàn |
| 🔍 | **Code Review** | AI review với severity rating |
| 📄 | **SKILL/Docs Reader** | Load `.md` vào context |
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
xkode chat
```

---

## 📖 Usage

```bash
# Interactive chat
xkode chat
xkode chat --path /my/project

# Slash commands in chat
/docs README.md     # AI đọc & tóm tắt
/skill SKILL.md     # Load instructions
/model qwen:32b     # Switch model
/review             # Code review
/help               # All commands

# Single task
xkode run "Add error handling to UserService"

# Code review
xkode review --focus security

# List models
xkode models
```

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
