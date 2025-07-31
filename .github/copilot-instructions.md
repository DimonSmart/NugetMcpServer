## Instructions
Follow these standards when writing or reviewing C# code for this project.
Adhering to these guidelines ensures code consistency, maintainability, and clarity across the team.

## Project Coding Standards

### Comments
- Write comments **only** when they add real value: explain **why** or clarify **non-trivial** behavior.
- Do **not** comment on code that is already obvious (e.g., simple assignments or straightforward condition checks).
- Do **not** write comments that simply repeat the function name.
- Avoid comments that describe **what** the code does; focus on **why** and on any non-standard implementation details.
- Always write comments in English.

### Control Flow
- Minimize the use of `if…else`; use guard clauses and early `return` statements instead.
- Do not use `elseif`.
- Avoid deep nesting of `if` statements; use early exits to keep methods flat.

### JSON Serialization
- Use `System.Text.Json` for all JSON (de)serialization.

### Try-Catch / Finally
- Avoid placing long `try…catch` or `try…finally` blocks (more than 3 lines) directly inside methods.
- Extract complex or multi-line error/finally logic into a generic helper method.
- Design the helper to accept a delegate (`Action`, `Func<T>`, or `Func<Task>`) and optional exception and/or finally handlers.
- Call this helper from your methods to keep the method body focused on core logic.

### Variables
- Use `var` when declaring variables. Example: `var users = new List<User>();`

### Code Cleanup
- Run `dotnet format -v d` to format the code.