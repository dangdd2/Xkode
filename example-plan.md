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
