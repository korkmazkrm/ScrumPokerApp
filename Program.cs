using ScrumPokerApp.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", b => b.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials());
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.MapHub<PokerHub>("/pokerHub");
app.Run();