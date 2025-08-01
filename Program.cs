﻿using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using FreshServiceTools.Models;
using FreshServiceTools.Services;

#pragma warning disable SKEXP0070 // Google AI connector is experimental

var builder = WebApplication.CreateBuilder(args);

// --- Configure services for Dependency Injection ---

// Add user secrets and configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                     .AddUserSecrets<Program>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowChatApp",
        policy =>
        {
            // Allow the origin of your chat app
            policy.AllowAnyOrigin() //WithOrigins("https://localhost:7003")
                  .AllowAnyHeader()
                  .AllowAnyMethod();

            // If you are just running the HTML file directly, 
            // you might need to allow any origin for simplicity during testing.
            // policy.AllowAnyOrigin()
            //       .AllowAnyHeader()
            //       .AllowAnyMethod();
        });
});
// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register your custom configuration settings
builder.Services.Configure<FreshServiceSettings>(
    builder.Configuration.GetSection("FreshService"));
builder.Services.Configure<GeminiSettings>(
    builder.Configuration.GetSection("ApiKeys"));

// Register your custom services                    
builder.Services.AddHttpClient<IFreshServiceClient, FreshServiceClient>();
//builder.Services.AddSingleton<IFreshServiceRAGService, FreshServiceRAGService>();

// Register Semantic Kernel services
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var geminiApiKey = builder.Configuration["ApiKeys:Gemini"];
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.Services.AddGoogleAIGeminiChatCompletion("gemini-2.5-flash", geminiApiKey);
    return kernelBuilder.Build();
});
// --- Build the application ---
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowChatApp");
app.UseAuthorization();
app.MapControllers();
app.Run("https://localhost:7003");
