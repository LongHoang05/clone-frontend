var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<FrontendClonerApi.Services.IClonerService, FrontendClonerApi.Services.ClonerService>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<FrontendClonerApi.Services.CleanupHostedService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder => builder
        .WithOrigins("http://localhost:5173", "http://localhost:3000")
        .AllowCredentials()
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthorization();
app.MapControllers();
app.MapHub<FrontendClonerApi.Hubs.CloneProgressHub>("/hubs/cloneProgress");

app.Run();
