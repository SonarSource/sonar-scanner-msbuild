var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// This method's Async version is supported for >= .NET 6
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.webapplication.runasync?view=aspnetcore-8.0
#pragma warning disable S6966 // Awaitable method should be used
app.Run();
#pragma warning disable S6966 // Awaitable method should be used
