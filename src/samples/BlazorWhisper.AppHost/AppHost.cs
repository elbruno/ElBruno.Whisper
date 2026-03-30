var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.BlazorWhisper>("blazor-whisper");

builder.Build().Run();
