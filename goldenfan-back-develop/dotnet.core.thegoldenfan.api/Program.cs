using dotnet.core.thegoldenfan;
using dotnet.core.utils.Helpers;
using dotnet.core.utils.server;


ProgramHelper.Init();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureWebServer();
builder.Services.AddTheGoldenFanService();
builder.Services.AddCorsService();
builder.Services.AddJwtService();
builder.Services.AddControllers().AddXmlSerializerFormatters();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerService();


var app = builder.Build();
app.UseSwaggerService();
app.UseCors(dotnet.core.utils.server.ConfigureService.CorsOrigins);
app.MapControllers();
app.Run();
