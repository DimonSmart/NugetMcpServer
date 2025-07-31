## Project Coding Standards

### Comments
- Write comments **only** when they add real value: explain **why** or clarify **non-trivial** behavior.
- Do **not** comment on what is already obvious from the code (e.g., simple assignments or straightforward condition checks).
- Do **not** write comments which repeat function name. 
- Avoid comments that describe **what** the code does; focus on **why** and on any non-standard implementation details.
- All comments must be written in English.  

### Control Flow
- Minimize use of `if…else`; prefer guard clauses and early `return`.
- Do not use `elseif'
- Avoid deep nesting of `if` statements; use early exits to keep methods flat.

### JSON Serialization
- Use `System.Text.Json` for all JSON (de)serialization.

### Try-Catch / Finally
- Avoid long `try…catch` or `try…finally` blocks (more than 3 lines) directly inside methods.
- For complex or multi-line error/finally logic, extract the `try…catch` and/or `finally` into a generic helper method.
- The helper should accept a delegate (`Action`, `Func<T>` or `Func<Task>`) and optional exception and/or finally handlers.
- In your methods, invoke this helper so that the method body remains focused on core logic.

### Variables
- Prefer var style. Example: `var users = new List<User>();`

### Code cleanup
Call `dotnet format -v d` for code formatting
