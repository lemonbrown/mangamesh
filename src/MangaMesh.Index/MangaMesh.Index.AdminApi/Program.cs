using MangaMesh.Shared.Services;
using MangaMesh.Shared.Stores;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// TODO: Register implementations for these interfaces if running standalone.
// Currently they are implemented in MangaMesh.Index.Api.
// builder.Services.AddSingleton<INodeRegistry, ...>();
// builder.Services.AddSingleton<IManifestEntryStore, ...>();
// builder.Services.AddSingleton<ISeriesRegistry, ...>();
// builder.Services.AddSingleton<IPublicKeyStore, ...>();
// builder.Services.AddSingleton<IApprovedKeyStore, ...>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
