using Reveal.Sdk;
using Reveal.Sdk.Dom;
using System.Text;
using RevalSdk.Server;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddReveal(builder => { });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
      builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    );
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "Images")),
    RequestPath = "/Images"
});

app.MapGet("/dashboards/{id}/thumbnail", async (string id) =>
{
    var path = $"Dashboards/{id}.rdash";
    if (File.Exists(path))
    {
        var dashboard = new Dashboard(path);
        var info = await dashboard.GetInfoAsync(Path.GetFileNameWithoutExtension(path));
        return TypedResults.Ok(info);
    }
    else
    {
        return Results.NotFound();
    }
});

app.MapGet("/isduplicatename/{name}", (string name) =>
{
    var filePath = Path.Combine(Environment.CurrentDirectory, "Dashboards");
    return File.Exists($"{filePath}/{name}.rdash");
});

string GetImageUrl(string input)
{
    const string visualizationSuffix = "Visualization";
    if (input.EndsWith(visualizationSuffix, StringComparison.OrdinalIgnoreCase))
    {
        input = input[..^visualizationSuffix.Length].TrimEnd();
    }
    return $"{input}.png";
}

string GetDisplayName(string input)
{
    const string visualizationSuffix = "Visualization";
    if (input.EndsWith(visualizationSuffix, StringComparison.OrdinalIgnoreCase))
    {
        input = input[..^visualizationSuffix.Length].TrimEnd();
    }

    StringBuilder friendlyNameBuilder = new(input.Length);
    foreach (char currentChar in input)
    {
        if (friendlyNameBuilder.Length > 0 && char.IsUpper(currentChar))
        {
            friendlyNameBuilder.Append(' ');
        }

        friendlyNameBuilder.Append(currentChar);
    }
    return friendlyNameBuilder.ToString().Trim();
}

app.MapGet("/dashboards/infos/all", async () =>
{
    var dashboardsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Dashboards");
    var dashboardFiles = Directory.GetFiles(dashboardsDirectory, "*.rdash");
    var dashboardInfos = new List<DashboardInfo>();

    foreach (var filePath in dashboardFiles)
    {
        var dashboard = new Dashboard(filePath);
        var info = await dashboard.GetInfoAsync(Path.GetFileNameWithoutExtension(filePath));
        dashboardInfos.Add(info);
    }

    return TypedResults.Ok(dashboardInfos);
});

app.MapGet("dashboards/visualizations/all", () =>
{
    try
    {
        var allVisualizationChartInfos = new List<VisualizationChartInfo>();
        var dashboardFiles = Directory.GetFiles("Dashboards", "*.rdash");

        foreach (var filePath in dashboardFiles)
        {
            try
            {
                var document = RdashDocument.Load(filePath);
                foreach (var viz in document.Visualizations)
                {
                    try
                    {
                        var vizType = viz.GetType();
                        var chartInfo = new VisualizationChartInfo
                        {
                            DashboardFileName = Path.GetFileNameWithoutExtension(filePath),
                            DashboardTitle = document.Title,
                            VizId = viz.Id,
                            VizTitle = viz.Title,
                            VizChartType = GetDisplayName(vizType.Name),
                            VizImageUrl = GetImageUrl(vizType.Name),
                        };
                        allVisualizationChartInfos.Add(chartInfo);
                    }
                    catch (Exception vizEx)
                    {
                        Console.WriteLine($"Error processing visualization {viz.Id} in file {filePath}: {vizEx.Message}");
                    }
                }
            }
            catch (Exception fileEx)
            {
                Console.WriteLine($"Error processing file {filePath}: {fileEx.Message}");
            }
        }
        return Results.Ok(allVisualizationChartInfos);
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}");
    }

}).Produces<IEnumerable<VisualizationChartInfo>>(StatusCodes.Status200OK)
  .Produces(StatusCodes.Status500InternalServerError);

app.MapGet("/dashboards/names", () =>
{
    try
    {
        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Dashboards");
        var files = Directory.GetFiles(folderPath);
        Random rand = new();

        var fileNames = files.Select(file =>
        {
            try
            {
                return new DashboardNames
                {
                    DashboardFileName = Path.GetFileNameWithoutExtension(file),
                    DashboardTitle = RdashDocument.Load(file).Title
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Reading FileData {file}: {ex.Message}");
                return null;
            }
        }).Where(fileData => fileData != null).ToList();

        return Results.Ok(fileNames);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error Reading Directory : {ex.Message}");
        return Results.Problem("An unexpected error occurred while processing the request.");
    }

}).Produces<IEnumerable<DashboardNames>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
