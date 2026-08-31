var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona política de CORS para permitir acesso do front-end
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/asp.html/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 2. ATENÇÃO: O CORS deve ser ativado antes de UseAuthorization e MapControllers
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();