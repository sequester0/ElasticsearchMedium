using ElasticsearchMedium.Core.Interfaces;
using ElasticsearchMedium.Core.Models;
using ElasticsearchMedium.Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<ElasticsearchSettings>(
    builder.Configuration.GetSection("ElasticsearchSettings"));

builder.Services.AddHttpClient<IElasticsearchService, ElasticsearchService>();
builder.Services.AddScoped<IElasticsearchService, ElasticsearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();