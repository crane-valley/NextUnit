using Microsoft.Testing.Platform.Builder;
using NextUnit.Platform;

var builder = await TestApplication.CreateBuilderAsync(args);
builder.AddNextUnit();
var app = await builder.BuildAsync();
return await app.RunAsync();
