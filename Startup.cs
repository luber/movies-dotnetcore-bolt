using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MoviesDotNetCore.Repositories;
using Neo4j.Driver;
using Neo4jClient;

namespace MoviesDotNetCore;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddScoped<IMovieRepository, MovieRepository>();
        services.AddSingleton<IGraphClient>(sp =>
        {
            var graphClient = new BoltGraphClient(
                Environment.GetEnvironmentVariable("NEO4J_URI") ?? "neo4j+s://demo.neo4jlabs.com",
                Environment.GetEnvironmentVariable("NEO4J_USER") ?? "movies",
                Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "movies"
            );

            graphClient.DefaultDatabase = "movies";
            
            var connectTask = graphClient.ConnectAsync();
            connectTask.Wait();

            return graphClient;
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}