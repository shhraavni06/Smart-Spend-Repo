using Microsoft.Data.Sqlite;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

Database.Init();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "SmartSpend API" }));

// ---------- AUTH ----------
app.MapPost("/api/auth/login", (LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { message = "Please enter your username/email and password." });

    using var db = Database.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = """
        SELECT Id, Username, Email, FullName, Role
        FROM Users
        WHERE (lower(Username)=lower($id) OR lower(Email)=lower($id))
          AND Password=$password
        LIMIT 1
    """;
    cmd.Parameters.AddWithValue("$id", request.Identifier.Trim());
    cmd.Parameters.AddWithValue("$password", request.Password);

    using var reader = cmd.ExecuteReader();
    if (!reader.Read())
        return Results.Unauthorized();

    return Results.Ok(new UserDto(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4)
    ));
});

app.MapPost("/api/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.FullName) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Username) ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { message = "Please complete all required fields." });

    if (request.Password.Length < 6)
        return Results.BadRequest(new { message = "Password must contain at least 6 characters." });

    using var db = Database.Open();
    using var check = db.CreateCommand();
    check.CommandText = "SELECT COUNT(*) FROM Users WHERE lower(Username)=lower($u) OR lower(Email)=lower($e)";
    check.Parameters.AddWithValue("$u", request.Username.Trim());
    check.Parameters.AddWithValue("$e", request.Email.Trim());

    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
        return Results.Conflict(new { message = "That username or email is already registered. Try another one." });

    using var cmd = db.CreateCommand();
    cmd.CommandText = """
        INSERT INTO Users(Username,Password,Email,FullName,Role)
        VALUES($u,$p,$e,$f,'User')
    """;
    cmd.Parameters.AddWithValue("$u", request.Username.Trim());
    cmd.Parameters.AddWithValue("$p", request.Password);
    cmd.Parameters.AddWithValue("$e", request.Email.Trim());
    cmd.Parameters.AddWithValue("$f", request.FullName.Trim());
    cmd.ExecuteNonQuery();

    return Results.Ok(new { message = "Account created successfully. You can now sign in." });
});

// ---------- ADMIN ----------
app.MapGet("/api/admin/users", (int adminId) =>
{
    if (!Database.IsAdmin(adminId))
        return Results.Forbid();

    using var db = Database.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = "SELECT Id,Username,Email,FullName,Role FROM Users ORDER BY Id DESC";

    using var r = cmd.ExecuteReader();
    var users = new List<UserDto>();
    while (r.Read())
        users.Add(new UserDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));

    return Results.Ok(users);
});

// ---------- EXPENSES ----------
app.MapGet("/api/expenses/{userId:int}", (int userId) =>
{
    using var db = Database.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = """
        SELECT Id,UserId,Title,Category,Amount,ExpenseDate,Notes
        FROM Expenses WHERE UserId=$u ORDER BY ExpenseDate DESC,Id DESC
    """;
    cmd.Parameters.AddWithValue("$u", userId);

    using var r = cmd.ExecuteReader();
    var list = new List<ExpenseDto>();
    while (r.Read())
        list.Add(new ExpenseDto(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetString(3),
            r.GetDecimal(4),r.GetString(5),r.IsDBNull(6) ? "" : r.GetString(6)));

    return Results.Ok(list);
});

app.MapPost("/api/expenses", (ExpenseCreate x) =>
{
    using var db = Database.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = """
        INSERT INTO Expenses(UserId,Title,Category,Amount,ExpenseDate,Notes)
        VALUES($u,$t,$c,$a,$d,$n)
    """;
    cmd.Parameters.AddWithValue("$u",x.UserId);
    cmd.Parameters.AddWithValue("$t",x.Title.Trim());
    cmd.Parameters.AddWithValue("$c",x.Category);
    cmd.Parameters.AddWithValue("$a",x.Amount);
    cmd.Parameters.AddWithValue("$d",x.ExpenseDate);
    cmd.Parameters.AddWithValue("$n",x.Notes ?? "");
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message="Expense added successfully." });
});

app.MapDelete("/api/expenses/{id:int}", (int id) =>
{
    using var db = Database.Open();
    using var cmd = db.CreateCommand();
    cmd.CommandText = "DELETE FROM Expenses WHERE Id=$id";
    cmd.Parameters.AddWithValue("$id",id);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message="Expense deleted." });
});

// ---------- BUDGETS ----------
app.MapGet("/api/budgets/{userId:int}", (int userId) =>
{
    using var db=Database.Open();
    using var cmd=db.CreateCommand();
    cmd.CommandText="SELECT Id,UserId,Category,Amount,MonthYear FROM Budgets WHERE UserId=$u ORDER BY Id DESC";
    cmd.Parameters.AddWithValue("$u",userId);
    using var r=cmd.ExecuteReader();
    var list=new List<BudgetDto>();
    while(r.Read()) list.Add(new BudgetDto(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetDecimal(3),r.GetString(4)));
    return Results.Ok(list);
});

app.MapPost("/api/budgets",(BudgetCreate x)=>{
    using var db=Database.Open();
    using var cmd=db.CreateCommand();
    cmd.CommandText="INSERT INTO Budgets(UserId,Category,Amount,MonthYear) VALUES($u,$c,$a,$m)";
    cmd.Parameters.AddWithValue("$u",x.UserId);
    cmd.Parameters.AddWithValue("$c",x.Category);
    cmd.Parameters.AddWithValue("$a",x.Amount);
    cmd.Parameters.AddWithValue("$m",x.MonthYear);
    cmd.ExecuteNonQuery();
    return Results.Ok(new {message="Budget saved successfully."});
});

// ---------- GOALS ----------
app.MapGet("/api/goals/{userId:int}",(int userId)=>{
    using var db=Database.Open();
    using var cmd=db.CreateCommand();
    cmd.CommandText="SELECT Id,UserId,Name,TargetAmount,SavedAmount,Deadline FROM Goals WHERE UserId=$u ORDER BY Id DESC";
    cmd.Parameters.AddWithValue("$u",userId);
    using var r=cmd.ExecuteReader();
    var list=new List<GoalDto>();
    while(r.Read()) list.Add(new GoalDto(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.IsDBNull(5)?"":r.GetString(5)));
    return Results.Ok(list);
});

app.MapPost("/api/goals",(GoalCreate x)=>{
    using var db=Database.Open();
    using var cmd=db.CreateCommand();
    cmd.CommandText="INSERT INTO Goals(UserId,Name,TargetAmount,SavedAmount,Deadline) VALUES($u,$n,$t,$s,$d)";
    cmd.Parameters.AddWithValue("$u",x.UserId);
    cmd.Parameters.AddWithValue("$n",x.Name.Trim());
    cmd.Parameters.AddWithValue("$t",x.TargetAmount);
    cmd.Parameters.AddWithValue("$s",x.SavedAmount);
    cmd.Parameters.AddWithValue("$d",x.Deadline ?? "");
    cmd.ExecuteNonQuery();
    return Results.Ok(new {message="Goal created successfully."});
});

// ---------- ANALYTICS ----------
app.MapGet("/api/analytics/{userId:int}",(int userId,string? category,string? from,string? to)=>{
    using var db=Database.Open();
    using var cmd=db.CreateCommand();
    cmd.CommandText="""
        SELECT Category,SUM(Amount) Total,COUNT(*) Count
        FROM Expenses
        WHERE UserId=$u
          AND ($c='' OR Category=$c)
          AND ($f='' OR ExpenseDate >= $f)
          AND ($t='' OR ExpenseDate <= $t)
        GROUP BY Category ORDER BY Total DESC
    """;
    cmd.Parameters.AddWithValue("$u",userId);
    cmd.Parameters.AddWithValue("$c",category ?? "");
    cmd.Parameters.AddWithValue("$f",from ?? "");
    cmd.Parameters.AddWithValue("$t",to ?? "");

    using var r=cmd.ExecuteReader();
    var cats=new List<object>();
    decimal total=0; int count=0;
    while(r.Read()){
        var amount=r.GetDecimal(1);
        total+=amount; count+=r.GetInt32(2);
        cats.Add(new {category=r.GetString(0),total=amount,count=r.GetInt32(2)});
    }
    return Results.Ok(new {total,count,categories=cats});
});

// ---------- AI ----------
app.MapPost("/api/advisor", async (AdvisorRequest x)=>{
    var summary=new List<string>();
    decimal total=0;

    using(var db=Database.Open())
    using(var cmd=db.CreateCommand()){
        cmd.CommandText="SELECT Category,SUM(Amount) FROM Expenses WHERE UserId=$u GROUP BY Category ORDER BY SUM(Amount) DESC";
        cmd.Parameters.AddWithValue("$u",x.UserId);
        using var r=cmd.ExecuteReader();
        while(r.Read()){
            var amount=r.GetDecimal(1); total+=amount;
            summary.Add($"{r.GetString(0)}: ₹{amount:0.00}");
        }
    }

    var expenseText=summary.Count==0 ? "No expenses recorded." : string.Join(", ",summary);
    var prompt=$"""
        You are SmartSpend, a helpful student personal finance adviser.
        Expense data: {expenseText}
        Total recorded spending: ₹{total:0.00}
        Question: {x.Question}
        Give concise practical advice based only on the supplied data.
    """;

    try{
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};
        var body=JsonSerializer.Serialize(new {model="llama3.2",prompt,stream=false});
        using var content=new StringContent(body,System.Text.Encoding.UTF8,"application/json");
        var response=await client.PostAsync("http://localhost:11434/api/generate",content);
        if(response.IsSuccessStatusCode){
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var answer=doc.RootElement.GetProperty("response").GetString() ?? "";
            return Results.Ok(new {source="Ollama · llama3.2",answer});
        }
    }catch{}

    var fallback=summary.Count==0
        ? "Start by adding a few expenses. Once you have some data, I can identify your biggest spending areas and suggest a realistic budget."
        : $"You have recorded ₹{total:0.00}. Your biggest categories are {expenseText}. Consider setting a limit for your highest category and reviewing your spending weekly.";
    return Results.Ok(new {source="SmartSpend Adviser",answer=fallback});
});

app.MapFallbackToFile("index.html");
app.Run();

record LoginRequest(string Identifier,string Password);
record RegisterRequest(string FullName,string Email,string Username,string Password);
record UserDto(int Id,string Username,string Email,string FullName,string Role);
record ExpenseCreate(int UserId,string Title,string Category,decimal Amount,string ExpenseDate,string? Notes);
record ExpenseDto(int Id,int UserId,string Title,string Category,decimal Amount,string ExpenseDate,string Notes);
record BudgetCreate(int UserId,string Category,decimal Amount,string MonthYear);
record BudgetDto(int Id,int UserId,string Category,decimal Amount,string MonthYear);
record GoalCreate(int UserId,string Name,decimal TargetAmount,decimal SavedAmount,string? Deadline);
record GoalDto(int Id,int UserId,string Name,decimal TargetAmount,decimal SavedAmount,string Deadline);
record AdvisorRequest(int UserId,string Question);

static class Database
{
    const string ConnectionString="Data Source=smartspend.db";

    public static SqliteConnection Open(){var c=new SqliteConnection(ConnectionString);c.Open();return c;}

    public static bool IsAdmin(int id)
    {
        using var db=Open(); using var cmd=db.CreateCommand();
        cmd.CommandText="SELECT Role FROM Users WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id",id);
        return string.Equals(cmd.ExecuteScalar()?.ToString(),"Admin",StringComparison.OrdinalIgnoreCase);
    }

    public static void Init()
    {
        using var db=Open(); using var cmd=db.CreateCommand();
        cmd.CommandText="""
        CREATE TABLE IF NOT EXISTS Users(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL UNIQUE,
            Password TEXT NOT NULL,
            Email TEXT NOT NULL UNIQUE,
            FullName TEXT NOT NULL,
            Role TEXT NOT NULL DEFAULT 'User'
        );
        CREATE TABLE IF NOT EXISTS Expenses(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Category TEXT NOT NULL,
            Amount REAL NOT NULL,
            ExpenseDate TEXT NOT NULL,
            Notes TEXT
        );
        CREATE TABLE IF NOT EXISTS Budgets(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Category TEXT NOT NULL,
            Amount REAL NOT NULL,
            MonthYear TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Goals(
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Name TEXT NOT NULL,
            TargetAmount REAL NOT NULL,
            SavedAmount REAL NOT NULL DEFAULT 0,
            Deadline TEXT
        );
        """;
        cmd.ExecuteNonQuery();

        Upsert("admin","Admin@123","admin@smartspend.local","SmartSpend Admin","Admin");
        Upsert("student1","Student@123","student1@smartspend.local","Student One","User");
        Upsert("student2","Student@123","student2@smartspend.local","Student Two","User");

        SeedExpenses();
    }

    static void Upsert(string username,string password,string email,string name,string role)
    {
        using var db=Open(); using var cmd=db.CreateCommand();
        cmd.CommandText="""
        INSERT INTO Users(Username,Password,Email,FullName,Role)
        VALUES($u,$p,$e,$n,$r)
        ON CONFLICT(Username) DO UPDATE SET
            Password=excluded.Password,
            Email=excluded.Email,
            FullName=excluded.FullName,
            Role=excluded.Role
        """;
        cmd.Parameters.AddWithValue("$u",username);
        cmd.Parameters.AddWithValue("$p",password);
        cmd.Parameters.AddWithValue("$e",email);
        cmd.Parameters.AddWithValue("$n",name);
        cmd.Parameters.AddWithValue("$r",role);
        cmd.ExecuteNonQuery();
    }

    static void SeedExpenses()
    {
        using var db=Open(); using var count=db.CreateCommand();
        count.CommandText="SELECT COUNT(*) FROM Expenses WHERE UserId=(SELECT Id FROM Users WHERE Username='student1')";
        if(Convert.ToInt32(count.ExecuteScalar())>0) return;

        using var cmd=db.CreateCommand();
        cmd.CommandText="""
        INSERT INTO Expenses(UserId,Title,Category,Amount,ExpenseDate,Notes)
        SELECT Id,'Lunch','Food',120,$d,'College canteen' FROM Users WHERE Username='student1';
        INSERT INTO Expenses(UserId,Title,Category,Amount,ExpenseDate,Notes)
        SELECT Id,'Bus Pass','Transport',80,$d2,'Daily travel' FROM Users WHERE Username='student1';
        INSERT INTO Expenses(UserId,Title,Category,Amount,ExpenseDate,Notes)
        SELECT Id,'Notebook','Education',150,$d3,'Semester notes' FROM Users WHERE Username='student1';
        """;
        cmd.Parameters.AddWithValue("$d",DateTime.Today.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$d2",DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$d3",DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"));
        cmd.ExecuteNonQuery();
    }
}
