using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Platform.API.Clients;
using Platform.API.Extensions;

using YouVersion.UsfmReferences;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddYouVersionApiClients(builder.Configuration);

using var host = builder.Build();

var bibleClient = host.Services.GetRequiredService<IBibleClient>();
var passageClient = host.Services.GetRequiredService<IPassageClient>();

Console.WriteLine("YouVersion Platform SDK - Console Sample");
Console.WriteLine("=========================================");

var versions = await bibleClient.GetVersionsAsync();
Console.WriteLine($"\nFound {versions.Data.Count} Bible version(s) on this page:");
foreach (var version in versions.Data.Take(5))
{
    Console.WriteLine($"  {version.Id,-6} {version.Abbreviation,-8} {version.Title}");
}

var versionId = versions.Data[0].Id;

var books = await bibleClient.GetBooksAsync(versionId);
Console.WriteLine($"\nFirst 5 books in version {versionId}:");
foreach (var book in books.Take(5))
{
    Console.WriteLine($"  {book.Usfm,-6} {book.Human} ({book.ChapterCount} chapters)");
}

var reference = Reference.FromString("JHN.3.16");
var passage = await passageClient.GetPassageAsync(versionId, reference);
Console.WriteLine($"\n{passage.Reference}:");
Console.WriteLine(passage.Content);
