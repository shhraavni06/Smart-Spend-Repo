# SmartSpend — Final Student Finance Adviser

## Included
- Attractive glassmorphism landing page
- Student/Admin role selection
- User-friendly login with username OR email
- Demo credential buttons
- Registration
- SQLite backend
- Expense CRUD
- Budgets
- Savings goals
- Analytics with category/date filters
- AI Adviser using Ollama `llama3.2`, with a local fallback
- Admin user console
- Responsive UI

## Demo login
Student:
student1 / Student@123

Admin:
admin / Admin@123

The backend re-seeds/updates these demo accounts at startup, so old credentials in an existing SQLite database do not cause the earlier "invalid credentials" problem.

## Run
Open the folder in VS Code:

```powershell
dotnet restore
dotnet run
```

Open the URL printed by the terminal. If it prints `http://localhost:5000`, open that.

## Clean database
Normally you do NOT need to delete `smartspend.db`. The startup code updates the demo accounts automatically.

For a completely fresh database, stop the app and delete:
`smartspend.db`

Then run `dotnet run` again.

## Ollama
Optional:
```powershell
ollama list
```
Make sure `llama3.2` exists and Ollama is running on port 11434.
If it is not available, SmartSpend uses its built-in local fallback adviser.

## Important
This is an academic/demo implementation. Production deployment should use password hashing, ASP.NET Identity/JWT or secure session authentication, HTTPS, validation, and proper authorization middleware.
