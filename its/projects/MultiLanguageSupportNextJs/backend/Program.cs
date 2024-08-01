var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var i = 0;

app.MapGet("/", () => "Hello World!");

app.Run();
