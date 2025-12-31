# GitHub Copilot Chat Cheat Sheet (Visual Studio)

## 🔢 `#` — Reference Code Elements

Use `#` to refer to files, methods, classes, or symbols in your project.

**Examples:**

* `Explain #CalculateTotal`
* `Write unit tests for all methods in #InvoiceManager`
* `Suggest improvements for #file`

---

## 🧭 `/` — Run Built-In Commands

Use `/` to trigger Copilot actions.

**Common Commands:**

* `/help` → Lists available commands
* `/explain` → Explains selected code
* `/test` → Generates unit tests
* `/fix` → Suggests bug fixes
* `/optimize` → Refactors code for performance
* `/docs` → Adds documentation

**Examples:**

* `/explain #CalculateTotal`
* `/test #UserService`
* `/optimize #file`

---

## 🔌 `@` — Invoke Extensions

Use `@` to interact with installed extensions or tools.

**Examples:**

* `@CopilotSecurity scan #UserService`
* `@GitHubIssues create issue for #bug`
* `@CopilotDocs document #InvoiceManager`

---

## 🧰 Pro Tips (Visual Studio)

* Type `#`, `/`, or `@` to get autocomplete suggestions from your project.
* Copilot Chat is available in **Solution Explorer**, **Git Changes**, and **Editor Tabs**.
* Combine commands for workflows, e.g.:

```plaintext
/optimize #DataProcessor
@CopilotSecurity scan #DataProcessor
/write tests for #DataProcessor

